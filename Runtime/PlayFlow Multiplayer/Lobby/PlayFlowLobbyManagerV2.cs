using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace PlayFlow
{
    [RequireComponent(typeof(PlayFlowSession), typeof(PlayFlowEvents))]
    public class PlayFlowLobbyManagerV2 : MonoBehaviour
    {
        [Header("API Configuration")]
        [Tooltip("Your PlayFlow API key. Get this from your PlayFlow dashboard.")]
        [SerializeField] private string _apiKey;

        [Tooltip("The base URL for the PlayFlow backend")]
        [SerializeField] private string _baseUrl = "https://backend.computeflow.cloud";
        
        [Tooltip("The default lobby configuration name")]
        [SerializeField] private string _defaultLobbyConfig = "Default";
        
        [Header("Network Settings")]
        [Tooltip("How often to refresh lobby data (in seconds)")]
        [Range(3f, 30f)]
        [SerializeField] private float _refreshInterval = 5f;
        
        [Tooltip("Enable or disable automatic refreshing of lobby data.")]
        [SerializeField] private bool _autoRefresh = true;
        
        [Tooltip("Maximum number of retry attempts for failed requests")]
        [Range(1, 10)]
        [SerializeField] private int _maxRetryAttempts = 3;
        
        [Tooltip("Delay between retry attempts (in seconds)")]
        [Range(0.5f, 5f)]
        [SerializeField] private float _retryDelay = 1f;
        
        [Header("Timeouts")]
        [Tooltip("Request timeout in seconds")]
        [SerializeField] private float _requestTimeout = 30f;
        
        [Tooltip("Connection timeout in seconds")]
        [SerializeField] private float _connectionTimeout = 10f;
        
        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool _debugLogging = false;

        private PlayFlowSession _session;
        private PlayFlowEvents _events;
        private LobbyOperations _operations;
        private LobbyRefreshManager _refreshManager;
        private PlayFlowSettings _runtimeSettings;
        
        // Public API
        public bool IsReady => _session != null && _session.IsInitialized;
        public Lobby CurrentLobby => _session?.CurrentLobby;
        public string PlayerId => _session?.PlayerId;
        public LobbyState State => _session?.CurrentState ?? LobbyState.Disconnected;
        public PlayFlowEvents Events => _events;
        
        public List<Lobby> AvailableLobbies { get; private set; } = new List<Lobby>();
        
        // Helpers
        public bool IsInLobby => State == LobbyState.InLobby;
        public string CurrentLobbyId => CurrentLobby?.id;
        public bool IsHost => IsInLobby && CurrentLobby?.host == PlayerId;
        public string InviteCode => CurrentLobby?.inviteCode;
        
        // Settings access
        public string ApiKey => _apiKey;
        public string BaseUrl => _baseUrl;
        public string DefaultLobbyConfig => _defaultLobbyConfig;
        public float RefreshInterval => _refreshInterval;
        public bool AutoRefresh => _autoRefresh;
        public bool DebugLogging => _debugLogging;
        
        private void Awake()
        {
            // Get or add required components immediately
            _session = GetComponent<PlayFlowSession>() ?? gameObject.AddComponent<PlayFlowSession>();
            _events = GetComponent<PlayFlowEvents>() ?? gameObject.AddComponent<PlayFlowEvents>();
            AvailableLobbies = new List<Lobby>();
        }
        
        private void OnValidate()
        {
            // Ensure minimum values
            _refreshInterval = Mathf.Max(3f, _refreshInterval);
            _requestTimeout = Mathf.Max(5f, _requestTimeout);
            _connectionTimeout = Mathf.Max(5f, _connectionTimeout);
        }
        
        private void CreateRuntimeSettings()
        {
            _runtimeSettings = ScriptableObject.CreateInstance<PlayFlowSettings>();
            _runtimeSettings.apiKey = _apiKey;
            _runtimeSettings.baseUrl = _baseUrl;
            _runtimeSettings.defaultLobbyConfig = _defaultLobbyConfig;
            _runtimeSettings.refreshInterval = _refreshInterval;
            _runtimeSettings.autoRefresh = _autoRefresh;
            _runtimeSettings.maxRetryAttempts = _maxRetryAttempts;
            _runtimeSettings.retryDelay = _retryDelay;
            _runtimeSettings.requestTimeout = _requestTimeout;
            _runtimeSettings.connectionTimeout = _connectionTimeout;
            _runtimeSettings.debugLogging = _debugLogging;
        }
        
        /// <summary>
        /// Initialize the lobby manager with a player ID.
        /// The API Key must be set in the inspector.
        /// </summary>
        public void Initialize(string playerId, Action onComplete = null)
        {
            if (IsReady)
            {
                Debug.LogWarning("[PlayFlowLobbyManager] Already initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogError("[PlayFlowLobbyManager] API Key is required! Please set it in the inspector.", this);
                _events.InvokeError("API Key cannot be null or empty.");
                return;
            }

            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogError("[PlayFlowLobbyManager] PlayerId cannot be empty");
                _events.InvokeError("PlayerId cannot be empty");
                return;
            }
            
            // 1. Create runtime settings from Inspector values
            CreateRuntimeSettings();

            // 2. Initialize the Core with these settings
            PlayFlowCore.Instance.InitializeWithSettings(_runtimeSettings);
            
            // 3. Initialize this manager's components that depend on the Core
            _operations = new LobbyOperations(_runtimeSettings, _events);
            _refreshManager = GetComponent<LobbyRefreshManager>() ?? gameObject.AddComponent<LobbyRefreshManager>();
            _refreshManager.OnLobbyListRefreshed += HandleLobbyListRefreshed;
            _refreshManager.Initialize(_runtimeSettings, _operations);
            
            // 4. Subscribe to events
            _session.OnStateChanged.AddListener(OnSessionStateChanged);
            _session.OnLobbyUpdated.AddListener(OnSessionLobbyUpdated);
            _session.OnError.AddListener(_events.InvokeError);

            // 5. Start the initialization coroutine
            StartCoroutine(InitializeCoroutine(playerId, onComplete));
        }

        private IEnumerator InitializeCoroutine(string playerId, Action onComplete)
        {
            _session.Initialize(playerId, _runtimeSettings);
            
            yield return new WaitUntil(() => PlayFlowCore.Instance.LobbyAPI != null);
            
            _events.InvokeConnected();
            onComplete?.Invoke();
            
            if (_debugLogging)
            {
                Debug.Log($"[PlayFlowLobbyManager] Initialized successfully for player: {playerId}");
            }
        }
        
        /// <summary>
        /// Create a new lobby
        /// </summary>
        public void CreateLobby(string name, int maxPlayers, bool isPrivate, bool allowLateJoin, string region, Dictionary<string, object> customSettings, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("create lobby", onError))
                return;

            StartCoroutine(_operations.CreateLobbyCoroutine(name, maxPlayers, isPrivate, allowLateJoin, region, customSettings, PlayerId, lobby =>
            {
                _session.SetCurrentLobby(lobby);
                _events.InvokeLobbyCreated(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        /// <summary>
        /// Create a lobby with simple parameters
        /// </summary>
        public void CreateLobby(string name, int maxPlayers = 4, bool isPrivate = false, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            CreateLobby(name, maxPlayers, isPrivate, true, "us-west", new Dictionary<string, object>(), onSuccess, onError);
        }
        
        public void JoinLobby(string lobbyId, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("join lobby", onError))
                return;
                
            if (string.IsNullOrEmpty(lobbyId))
            {
                onError?.Invoke("Lobby ID cannot be empty");
                return;
            }
            
            StartCoroutine(_operations.JoinLobbyCoroutine(lobbyId, PlayerId, lobby =>
            {
                _session.SetCurrentLobby(lobby);
                _events.InvokeLobbyJoined(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void LeaveLobby(Action onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("leave lobby", onError))
                return;
                
            if (!_session.IsInLobby)
            {
                onError?.Invoke("Not in a lobby");
                return;
            }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.LeaveLobbyCoroutine(lobbyId, PlayerId, () =>
            {
                _session.ClearCurrentLobby();
                _events.InvokeLobbyLeft();
                onSuccess?.Invoke();
            }, onError));
        }
        
        public void UpdatePlayerState(Dictionary<string, object> state, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("update player state", onError))
                return;
                
            if (!_session.IsInLobby)
            {
                onError?.Invoke("Not in a lobby");
                return;
            }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.UpdatePlayerStateCoroutine(lobbyId, PlayerId, state, lobby =>
            {
                _session.UpdateCurrentLobby(lobby);
                _events.InvokePlayerStateChanged(PlayerId);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void GetAvailableLobbies(Action<List<Lobby>> onSuccess, Action<string> onError = null)
        {
            if (!ValidateOperation("get lobbies", onError))
                return;
                
            StartCoroutine(_operations.ListLobbiesCoroutine(lobbies => {
                this.AvailableLobbies = lobbies;
                onSuccess?.Invoke(lobbies);
            }, onError));
        }
        
        public void StartMatch(Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("start match", onError)) return;
            if (!IsHost)
            {
                onError?.Invoke("Only the host can start the match.");
                return;
            }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.UpdateLobbyStatusCoroutine(lobbyId, PlayerId, "in_game", lobby =>
            {
                _session.UpdateCurrentLobby(lobby);
                _events.InvokeMatchStarted(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }

        public void EndMatch(Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("end match", onError)) return;
            if (!IsHost)
            {
                onError?.Invoke("Only the host can end the match.");
                return;
            }

            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.UpdateLobbyStatusCoroutine(lobbyId, PlayerId, "waiting", lobby =>
            {
                _session.UpdateCurrentLobby(lobby);
                _events.InvokeMatchEnded(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void RefreshCurrentLobby(Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("refresh lobby", onError))
                return;
                
            if (!_session.IsInLobby)
            {
                onError?.Invoke("Not in a lobby");
                return;
            }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.GetLobbyCoroutine(lobbyId, lobby =>
            {
                _session.UpdateCurrentLobby(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        /// <summary>
        /// Force the refresh manager to check for updates immediately.
        /// </summary>
        public void ForceRefresh()
        {
            _refreshManager?.ForceRefresh();
        }

        public void Disconnect()
        {
            if (_session != null)
            {
                _session.Disconnect();
            }
            
            _events.InvokeDisconnected();
        }
        
        private bool ValidateOperation(string operation, Action<string> onError)
        {
            if (!IsReady)
            {
                var error = $"Cannot {operation}: Manager not initialized";
                Debug.LogError($"[PlayFlowLobbyManager] {error}");
                onError?.Invoke(error);
                return false;
            }
            
            return true;
        }
        
        private void OnSessionStateChanged(LobbyState oldState, LobbyState newState)
        {
            if (_debugLogging)
            {
                Debug.Log($"[PlayFlowLobbyManager] State changed: {oldState} -> {newState}");
            }
        }
        
        private void OnSessionLobbyUpdated(Lobby lobby)
        {
            if (lobby != null)
            {
                _events.InvokeLobbyUpdated(lobby);
                CheckForPlayerChanges(lobby);
            }
        }
        
        private void CheckForPlayerChanges(Lobby newLobby)
        {
            // This would compare with previous lobby state to detect player joins/leaves
            // For now, we'll rely on the backend to send these events
        }
        
        private void HandleLobbyListRefreshed(List<Lobby> lobbies)
        {
            this.AvailableLobbies = lobbies;
        }
        
        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnStateChanged.RemoveListener(OnSessionStateChanged);
                _session.OnLobbyUpdated.RemoveListener(OnSessionLobbyUpdated);
                _session.OnError.RemoveListener(_events.InvokeError);
            }
            
            if (_refreshManager != null)
            {
                _refreshManager.OnLobbyListRefreshed -= HandleLobbyListRefreshed;
            }
            
            if (_runtimeSettings != null)
            {
                DestroyImmediate(_runtimeSettings);
            }
        }

        public void KickPlayer(string playerToKickId, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("kick player", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can kick players."); return; }
            if (playerToKickId == PlayerId) { onError?.Invoke("Cannot kick yourself."); return; }

            StartCoroutine(_operations.KickPlayerCoroutine(CurrentLobbyId, PlayerId, playerToKickId, lobby =>
            {
                _session.UpdateCurrentLobby(lobby);
                _events.InvokePlayerLeft(playerToKickId);
                onSuccess?.Invoke(lobby);
            }, onError));
        }

        public void TransferHost(string newHostId, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("transfer host", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can transfer ownership."); return; }
            if (newHostId == PlayerId) { onError?.Invoke("Cannot transfer host to yourself."); return; }

            StartCoroutine(_operations.TransferHostCoroutine(CurrentLobbyId, PlayerId, newHostId, lobby =>
            {
                _session.UpdateCurrentLobby(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }

        public void UpdateLobbySettings(Dictionary<string, object> newSettings, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("update lobby settings", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can update lobby settings."); return; }

            StartCoroutine(_operations.UpdateLobbySettingsCoroutine(CurrentLobbyId, PlayerId, newSettings, lobby =>
            {
                _session.UpdateCurrentLobby(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
    }
} 