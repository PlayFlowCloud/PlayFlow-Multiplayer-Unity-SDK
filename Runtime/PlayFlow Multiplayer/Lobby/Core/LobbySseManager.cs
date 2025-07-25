using System;
using System.Collections;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayFlow
{
    /// <summary>
    /// Internal SSE manager for real-time lobby updates.
    /// This is completely transparent to game developers and handles SSE connections behind the scenes.
    /// </summary>
    internal class LobbySseManager : MonoBehaviour
    {
        private string _playerId;
        private string _apiKey;
        private string _lobbyConfigName;
        private string _baseUrl;
        private string _currentLobbyId;
        private bool _isConnecting = false;
        private bool _shouldReconnect = true;
        private float _reconnectDelay = 1f;
        private float _maxReconnectDelay = 30f;
        private int _reconnectAttempts = 0;
        private int _maxReconnectAttempts = 10;
        private Coroutine _sseCoroutine;
        private Coroutine _reconnectCoroutine;
        private Coroutine _heartbeatCoroutine;
        private float _lastDataReceived;
        private bool _isPaused = false;
        
        // Events for internal use
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<Lobby> OnLobbyUpdated;
        public event Action<string> OnLobbyDeleted;
        public event Action<string> OnError;
        
        // Connection state
        public bool IsConnected { get; private set; } = false;
        
        private static LobbySseManager _instance;
        public static LobbySseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("LobbySseManager");
                    go.hideFlags = HideFlags.HideInHierarchy;
                    _instance = go.AddComponent<LobbySseManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }
        
        /// <summary>
        /// Initialize SSE manager with connection parameters
        /// </summary>
        public void Initialize(string playerId, string apiKey, string lobbyConfigName, string baseUrl)
        {
            _playerId = playerId;
            _apiKey = apiKey;
            _lobbyConfigName = lobbyConfigName;
            _baseUrl = baseUrl?.TrimEnd('/') ?? "https://backend.computeflow.cloud";
            
            Debug.Log($"[LobbySseManager] Initialized - PlayerId: {_playerId}, Config: {_lobbyConfigName}, URL: {_baseUrl}");
        }
        
        /// <summary>
        /// Connect to SSE for a specific lobby
        /// </summary>
        public void ConnectToLobby(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(_playerId))
            {
                return;
            }
            
            // Check platform support
            if (!PlatformSSEHandler.IsSSESupported())
            {
                Debug.LogWarning("[LobbySseManager] SSE not supported on this platform, using polling instead");
                OnError?.Invoke("SSE not supported on this platform, using polling instead");
                return;
            }
            
            Debug.Log($"[LobbySseManager] ConnectToLobby called for lobby: {lobbyId}, player: {_playerId}");
            
            // Disconnect from previous lobby if any
            if (_currentLobbyId != lobbyId)
            {
                StopConnectionCoroutines(false); // Stop connection but don't clear lobbyId yet
            }
            
            _currentLobbyId = lobbyId;
            _shouldReconnect = true;
            _reconnectAttempts = 0;
            _reconnectDelay = 1f;
            
            if (_sseCoroutine == null && !_isPaused)
            {
                _sseCoroutine = StartCoroutine(SSEConnectionCoroutine());
            }
        }
        
        /// <summary>
        /// Disconnect from current SSE connection and clear lobby context.
        /// </summary>
        public void Disconnect()
        {
            _shouldReconnect = false;
            _currentLobbyId = null;
            StopConnectionCoroutines(true);
        }

        /// <summary>
        /// Pauses the SSE connection, typically when the app goes into the background.
        /// </summary>
        public void Pause()
        {
            if (_isPaused) return;
            Debug.Log("[LobbySseManager] Pausing SSE connection.");
            _isPaused = true;
            StopConnectionCoroutines(false); // Stop connection but keep lobby context
        }

        /// <summary>
        /// Resumes the SSE connection, typically when the app returns to the foreground.
        /// </summary>
        public void Resume()
        {
            if (!_isPaused) return;
            Debug.Log("[LobbySseManager] Resuming SSE connection.");
            _isPaused = false;
            
            // Re-trigger connection if we have a lobby ID and are not already connecting
            if (!string.IsNullOrEmpty(_currentLobbyId) && _sseCoroutine == null)
            {
                ConnectToLobby(_currentLobbyId);
            }
        }

        private void StopConnectionCoroutines(bool clearLobbyId)
        {
            if (clearLobbyId)
            {
                _currentLobbyId = null;
            }

            if (_sseCoroutine != null)
            {
                StopCoroutine(_sseCoroutine);
                _sseCoroutine = null;
            }
            
            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }
            
            if (IsConnected)
            {
                IsConnected = false;
                OnDisconnected?.Invoke();
            }
        }
        
        private IEnumerator SSEConnectionCoroutine()
        {
            if (_isConnecting)
            {
                yield break;
            }
            
            _isConnecting = true;
            
            // Build SSE URL
            string sseUrl = $"{_baseUrl}/lobbies-sse/{_currentLobbyId}/events";
            string queryString = $"?player-id={UnityWebRequest.EscapeURL(_playerId)}&lobby-config={UnityWebRequest.EscapeURL(_lobbyConfigName)}";
            string fullUrl = sseUrl + queryString;
            
            Debug.Log($"[LobbySseManager] Connecting to SSE URL: {fullUrl}");
            
            using (var request = UnityWebRequest.Get(fullUrl))
            {
                request.SetRequestHeader("api-key", _apiKey);
                request.SetRequestHeader("Accept", "text/event-stream");
                request.SetRequestHeader("Cache-Control", "no-cache");
                request.downloadHandler = new SSEDownloadHandler(this);
                
                yield return request.SendWebRequest();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string error = $"SSE connection failed: {request.error} (HTTP {request.responseCode})";
                    Debug.LogError($"[LobbySseManager] {error}");
                    OnError?.Invoke(error);
                    
                    if (_shouldReconnect && _reconnectCoroutine == null && !_isPaused)
                    {
                        _reconnectCoroutine = StartCoroutine(ReconnectCoroutine());
                    }
                }
            }
            
            _isConnecting = false;
            _sseCoroutine = null;
            
            if (IsConnected)
            {
                IsConnected = false;
                OnDisconnected?.Invoke();
            }
        }
        
        private IEnumerator ReconnectCoroutine()
        {
            // Exponential backoff with jitter
            float jitter = UnityEngine.Random.Range(0.5f, 1.5f);
            float delay = Mathf.Min(_reconnectDelay * jitter, _maxReconnectDelay);
            
            yield return new WaitForSeconds(delay);
            
            _reconnectCoroutine = null;
            _reconnectAttempts++;
            
            if (_reconnectAttempts >= _maxReconnectAttempts)
            {
                OnError?.Invoke($"Failed to reconnect after {_maxReconnectAttempts} attempts");
                _shouldReconnect = false;
                yield break;
            }
            
            // Increase delay for next attempt (exponential backoff)
            _reconnectDelay = Mathf.Min(_reconnectDelay * 2f, _maxReconnectDelay);
            
            if (_shouldReconnect && !string.IsNullOrEmpty(_currentLobbyId) && !_isPaused)
            {
                if (_sseCoroutine == null)
                {
                    _sseCoroutine = StartCoroutine(SSEConnectionCoroutine());
                }
            }
        }
        
        internal void HandleSSEMessage(string eventType, string data)
        {
            _lastDataReceived = Time.time;
            _reconnectAttempts = 0; // Reset on successful data
            _reconnectDelay = 1f; // Reset delay on success
            
            try
            {
                switch (eventType)
                {
                    case "connected":
                        Debug.Log($"[LobbySseManager] Received 'connected' event");
                        var connectedData = JObject.Parse(data);
                        var lobby = connectedData["lobby"]?.ToObject<Lobby>();
                        if (lobby != null)
                        {
                            IsConnected = true;
                            OnConnected?.Invoke();
                            OnLobbyUpdated?.Invoke(lobby);
                        }
                        break;
                        
                    case "lobby:updated":
                        var updatedLobby = JsonConvert.DeserializeObject<Lobby>(data);
                        if (updatedLobby != null)
                        {
                            OnLobbyUpdated?.Invoke(updatedLobby);
                        }
                        break;
                        
                    case "lobby:deleted":
                        var deletedData = JObject.Parse(data);
                        var lobbyId = deletedData["lobbyId"]?.ToString();
                        if (!string.IsNullOrEmpty(lobbyId))
                        {
                            OnLobbyDeleted?.Invoke(lobbyId);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to parse SSE message: {e.Message}");
            }
        }
        
        void OnDestroy()
        {
            Disconnect();
            
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        /// <summary>
        /// Custom download handler for SSE that processes the stream as it arrives
        /// </summary>
        private class SSEDownloadHandler : DownloadHandlerScript
        {
            private readonly LobbySseManager _manager;
            private StringBuilder _buffer = new StringBuilder();
            private string _currentEventType = "";
            private StringBuilder _currentData = new StringBuilder();
            
            public SSEDownloadHandler(LobbySseManager manager) : base()
            {
                _manager = manager;
            }
            
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength == 0)
                    return true;
                
                string text = Encoding.UTF8.GetString(data, 0, dataLength);
                _buffer.Append(text);
                
                // Process complete lines
                string bufferContent = _buffer.ToString();
                string[] lines = bufferContent.Split('\n');
                
                // Keep the last incomplete line in the buffer
                _buffer.Clear();
                if (!bufferContent.EndsWith("\n"))
                {
                    _buffer.Append(lines[lines.Length - 1]);
                    lines = lines.Take(lines.Length - 1).ToArray();
                }
                
                foreach (string line in lines)
                {
                    ProcessLine(line.TrimEnd('\r'));
                }
                
                return true;
            }
            
            private void ProcessLine(string line)
            {
                if (string.IsNullOrEmpty(line))
                {
                    // Empty line signals end of event
                    if (!string.IsNullOrEmpty(_currentEventType) && _currentData.Length > 0)
                    {
                        _manager.HandleSSEMessage(_currentEventType, _currentData.ToString());
                        _currentEventType = "";
                        _currentData.Clear();
                    }
                    return;
                }
                
                if (line.StartsWith("event: "))
                {
                    _currentEventType = line.Substring(7);
                }
                else if (line.StartsWith("data: "))
                {
                    if (_currentData.Length > 0)
                        _currentData.AppendLine();
                    _currentData.Append(line.Substring(6));
                }
            }
            
            protected override void CompleteContent()
            {
                // Process any remaining data
                if (!string.IsNullOrEmpty(_currentEventType) && _currentData.Length > 0)
                {
                    _manager.HandleSSEMessage(_currentEventType, _currentData.ToString());
                }
            }
        }
    }
}