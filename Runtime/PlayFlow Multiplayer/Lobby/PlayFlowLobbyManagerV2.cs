using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PlayFlow
{
    public struct ConnectionInfo { public string Ip; public int Port; }

    [RequireComponent(typeof(PlayFlowEvents))]
    public class PlayFlowLobbyManagerV2 : MonoBehaviour
    {
        [Header("API Configuration")]
        [Tooltip("Your PlayFlow API key. Get this from your PlayFlow dashboard.")]
        [SerializeField] private string _apiKey;
        [Tooltip("The base URL for the PlayFlow backend")]
        [SerializeField] private string _baseUrl = "https://api.scale.computeflow.cloud";
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

        private PlayFlowEvents _events;
        private LobbyOperations _operations;
        private LobbyRefreshManager _refreshManager;
        private PlayFlowSettings _runtimeSettings;
        private bool _hasFiredMatchRunningEvent;
        private HashSet<string> _previousPlayerIds = new HashSet<string>();

        // --- Merged Session state fields ---
        private LobbyState _currentState = LobbyState.Disconnected;
        private string _playerId;
        private Lobby _currentLobby;
        
        public static PlayFlowLobbyManagerV2 Instance { get; private set; }
        
        // --- Public API Properties ---
        public bool IsReady { get; private set; } = false;
        public Lobby CurrentLobby => _currentLobby;
        public string PlayerId => _playerId;
        public LobbyState State => _currentState;
        public PlayFlowEvents Events => _events;
        public List<Lobby> AvailableLobbies { get; private set; } = new List<Lobby>();
        
        // --- Helpers ---
        public bool IsInLobby => State == LobbyState.InLobby;
        public string CurrentLobbyId => CurrentLobby?.id;
        public bool IsHost => IsInLobby && CurrentLobby?.host == PlayerId;
        public string InviteCode => CurrentLobby?.inviteCode;
        
        // --- Settings access ---
        public string ApiKey => _apiKey;
        public string BaseUrl => _baseUrl;
        public string DefaultLobbyConfig => _defaultLobbyConfig;
        public float RefreshInterval => _refreshInterval;
        public bool AutoRefresh => _autoRefresh;
        public bool Debugging => _debugLogging;
        
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _events = GetComponent<PlayFlowEvents>() ?? gameObject.AddComponent<PlayFlowEvents>();
            AvailableLobbies = new List<Lobby>();
        }
        
        private void OnValidate()
        {
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
        
        public void Initialize(string playerId, Action onComplete = null)
        {
            if (IsReady) { Debug.LogWarning("[PlayFlowLobbyManager] Already initialized."); return; }
            if (string.IsNullOrEmpty(_apiKey)) { Debug.LogError("[PlayFlowLobbyManager] API Key is required!", this); _events.InvokeError("API Key cannot be null or empty."); return; }
            if (string.IsNullOrEmpty(playerId)) { Debug.LogError("[PlayFlowLobbyManager] PlayerId cannot be empty"); _events.InvokeError("PlayerId cannot be empty"); return; }
            
            CreateRuntimeSettings();
            PlayFlowCore.Instance.InitializeWithSettings(_runtimeSettings);
            
            _operations = new LobbyOperations(_runtimeSettings, _events);
            _refreshManager = GetComponent<LobbyRefreshManager>() ?? gameObject.AddComponent<LobbyRefreshManager>();
            _refreshManager.OnLobbyListRefreshed += HandleLobbyListRefreshed;
            _refreshManager.Initialize(_runtimeSettings, _operations);
            
            _events.OnError.AddListener(error => Debug.LogError($"[PlayFlow] Error: {error}"));

            StartCoroutine(InitializeCoroutine(playerId, onComplete));
        }

        private IEnumerator InitializeCoroutine(string playerId, Action onComplete)
        {
            _playerId = playerId;
            ChangeState(LobbyState.Connected);
            yield return new WaitUntil(() => PlayFlowCore.Instance.LobbyAPI != null);
            IsReady = true;
            _events.InvokeConnected();
            onComplete?.Invoke();
            if (_debugLogging) { Debug.Log($"[PlayFlowLobbyManager] Initialized successfully for player: {playerId}"); }
        }
        
        public void CreateLobby(string name, int maxPlayers, bool isPrivate, bool allowLateJoin, string region, Dictionary<string, object> customSettings, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("create lobby", onError)) return;
            StartCoroutine(_operations.CreateLobbyCoroutine(name, maxPlayers, isPrivate, allowLateJoin, region, customSettings, PlayerId, lobby => {
                // Mark this update before setting to prevent race condition with refresh
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                SetCurrentLobby(lobby);
                _events.InvokeLobbyCreated(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void CreateLobby(string name, int maxPlayers = 4, bool isPrivate = false, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            CreateLobby(name, maxPlayers, isPrivate, true, "us-west", new Dictionary<string, object>(), onSuccess, onError);
        }
        
        public void JoinLobby(string lobbyId, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("join lobby", onError)) return;
            if (string.IsNullOrEmpty(lobbyId)) { onError?.Invoke("Lobby ID cannot be empty"); return; }
            StartCoroutine(_operations.JoinLobbyCoroutine(lobbyId, PlayerId, lobby => {
                // Mark this update before setting to prevent race condition with refresh
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                SetCurrentLobby(lobby);
                _events.InvokeLobbyJoined(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void JoinLobbyByCode(string inviteCode, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("join lobby by code", onError)) return;
            if (string.IsNullOrEmpty(inviteCode)) { onError?.Invoke("Invite Code cannot be empty"); return; }
            StartCoroutine(_operations.JoinLobbyByCodeCoroutine(inviteCode, PlayerId, lobby => {
                // Mark this update before setting to prevent race condition with refresh
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                SetCurrentLobby(lobby);
                _events.InvokeLobbyJoined(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void LeaveLobby(Action onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("leave lobby", onError)) return;
            if (!IsInLobby) { onError?.Invoke("Not in a lobby"); return; }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.LeaveLobbyCoroutine(lobbyId, PlayerId, () => {
                ClearCurrentLobby();
                onSuccess?.Invoke();
            }, onError));
        }
        
        public void UpdatePlayerState(Dictionary<string, object> state, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("update player state", onError)) return;
            if (!IsInLobby) { onError?.Invoke("Not in a lobby"); return; }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.UpdatePlayerStateCoroutine(lobbyId, PlayerId, PlayerId, state, lobby => {
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                UpdateCurrentLobby(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void UpdateStateForPlayer(string targetPlayerId, Dictionary<string, object> state, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("update state for player", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can update another player's state."); return; }
            if (!IsInLobby) { onError?.Invoke("Not in a lobby"); return; }
            if (string.IsNullOrEmpty(targetPlayerId)) { onError?.Invoke("Target Player ID cannot be empty."); return; }

            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.UpdatePlayerStateCoroutine(lobbyId, PlayerId, targetPlayerId, state, lobby => {
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                UpdateCurrentLobby(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void GetAvailableLobbies(Action<List<Lobby>> onSuccess, Action<string> onError = null)
        {
            if (!ValidateOperation("get lobbies", onError)) return;
            StartCoroutine(_operations.ListLobbiesCoroutine(lobbies => {
                this.AvailableLobbies = lobbies;
                onSuccess?.Invoke(lobbies);
            }, onError));
        }
        
        public void StartMatch(Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("start match", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can start the match."); return; }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.UpdateLobbyStatusCoroutine(lobbyId, PlayerId, "in_game", lobby => {
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                UpdateCurrentLobby(lobby);
                _events.InvokeMatchStarted(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }

        public void EndMatch(Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("end match", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can end the match."); return; }

            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.UpdateLobbyStatusCoroutine(lobbyId, PlayerId, "waiting", lobby => {
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                UpdateCurrentLobby(lobby);
                _events.InvokeMatchEnded(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void FindMatch(string mode, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("find match", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can start matchmaking."); return; }
            if (string.IsNullOrEmpty(mode)) { onError?.Invoke("Matchmaking mode is required."); return; }
            if (CurrentLobby?.status != "waiting") { onError?.Invoke("Can only start matchmaking when lobby is in 'waiting' status."); return; }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.StartMatchmakingCoroutine(lobbyId, PlayerId, mode, lobby => {
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                UpdateCurrentLobby(lobby);
                _events.InvokeMatchmakingStarted(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void CancelMatchmaking(Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("cancel matchmaking", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can cancel matchmaking."); return; }
            if (CurrentLobby?.status != "in_queue") { onError?.Invoke("Lobby is not currently in matchmaking queue."); return; }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.CancelMatchmakingCoroutine(lobbyId, PlayerId, lobby => {
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                UpdateCurrentLobby(lobby);
                _events.InvokeMatchmakingCancelled(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void RefreshCurrentLobby(Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("refresh lobby", onError)) return;
            if (!IsInLobby) { onError?.Invoke("Not in a lobby"); return; }
            
            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.GetLobbyCoroutine(lobbyId, lobby => {
                UpdateCurrentLobby(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        public void ForceRefresh() { _refreshManager?.ForceRefresh(); }

        public void Disconnect()
        {
            ClearCurrentLobby();
            _playerId = null;
            ChangeState(LobbyState.Disconnected);
            _events.InvokeDisconnected();
        }
        
        private bool ValidateOperation(string operation, Action<string> onError)
        {
            if (this == null || !gameObject.activeInHierarchy) return false;
            if (!IsReady) { var error = $"Cannot {operation}: Manager not initialized"; Debug.LogError($"[PlayFlowLobbyManager] {error}"); onError?.Invoke(error); return false; }
            return true;
        }
        
        private void ChangeState(LobbyState newState)
        {
            if (_currentState == newState) return;
            var oldState = _currentState;
            _currentState = newState;
            _events.OnStateChanged?.Invoke(oldState, newState);
            if (_debugLogging) { Debug.Log($"[PlayFlowLobbyManager] State changed from {oldState} to {newState}"); }
        }

        internal void SetCurrentLobby(Lobby lobby)
        {
            var wasInLobby = _currentLobby != null;
            _currentLobby = lobby;
            if (!wasInLobby) { ChangeState(LobbyState.InLobby); }
            ProcessLobbyUpdate(lobby);
        }

        internal void UpdateCurrentLobby(Lobby newLobby)
        {
            if (newLobby == null || _currentLobby == null || newLobby.id != _currentLobby.id) return;
            _currentLobby = newLobby;
            ProcessLobbyUpdate(newLobby);
        }

        internal void ClearCurrentLobby()
        {
            if (_currentLobby == null) return;
            _currentLobby = null;
            _previousPlayerIds.Clear();
            ChangeState(LobbyState.Connected);
            _events.InvokeLobbyLeft();
        }
        
        private void ProcessLobbyUpdate(Lobby lobby)
        {
            if (lobby == null) return;
            CheckForPlayerChanges(lobby);
            _events.InvokeLobbyUpdated(lobby);
            
            // Handle status-specific events
            switch (lobby.status)
            {
                case "in_game":
                    if (!_hasFiredMatchRunningEvent)
                    {
                        var connectionInfo = GetGameServerConnectionInfo();
                        if (connectionInfo.HasValue)
                        {
                            _events.InvokeMatchRunning(connectionInfo.Value);
                            _hasFiredMatchRunningEvent = true;
                        }
                    }
                    break;
                    
                case "match_found":
                    // Fire match found event when transitioning to match_found status
                    if (_currentLobby?.status == "in_queue")
                    {
                        _events.InvokeMatchFound(lobby);
                    }
                    break;
                    
                case "waiting":
                    // Reset the match running flag when returning to waiting
                    _hasFiredMatchRunningEvent = false;
                    break;
                    
                case "in_queue":
                    // Already handled by FindMatch method
                    break;
            }
        }
        
        private void CheckForPlayerChanges(Lobby newLobby)
        {
            if (this == null || !gameObject.activeInHierarchy) return;
            var newPlayerIds = newLobby?.players != null ? new HashSet<string>(newLobby.players) : new HashSet<string>();
            if (_previousPlayerIds.Count == 0 && newPlayerIds.Count > 0)
            {
                foreach (var playerId in newPlayerIds) { _events.InvokePlayerJoined(PlayerAction.Joined(playerId)); }
            }
            else
            {
                var leftPlayerIds = new HashSet<string>(_previousPlayerIds);
                leftPlayerIds.ExceptWith(newPlayerIds);
                foreach (var playerId in leftPlayerIds) { _events.InvokePlayerLeft(PlayerAction.Left(playerId)); }
                var joinedPlayerIds = new HashSet<string>(newPlayerIds);
                joinedPlayerIds.ExceptWith(_previousPlayerIds);
                foreach (var playerId in joinedPlayerIds) { _events.InvokePlayerJoined(PlayerAction.Joined(playerId)); }
            }
            _previousPlayerIds = newPlayerIds;
        }
        
        private void HandleLobbyListRefreshed(List<Lobby> lobbies) { this.AvailableLobbies = lobbies; }
        
        private void OnDestroy()
        {
            if (_refreshManager != null) { _refreshManager.OnLobbyListRefreshed -= HandleLobbyListRefreshed; }
            if (Instance == this) { Instance = null; }
            if (_runtimeSettings != null) { DestroyImmediate(_runtimeSettings); }
        }

        public void KickPlayer(string playerToKickId, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("kick player", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can kick players."); return; }
            if (playerToKickId == PlayerId) { onError?.Invoke("Cannot kick yourself."); return; }
            StartCoroutine(_operations.KickPlayerCoroutine(CurrentLobbyId, PlayerId, playerToKickId, lobby => {
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                UpdateCurrentLobby(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }

        public void TransferHost(string newHostId, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("transfer host", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can transfer ownership."); return; }
            if (newHostId == PlayerId) { onError?.Invoke("Cannot transfer host to yourself."); return; }
            StartCoroutine(_operations.TransferHostCoroutine(CurrentLobbyId, PlayerId, newHostId, lobby => {
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                UpdateCurrentLobby(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }

        public void UpdateLobby(string name = null, int? maxPlayers = null, bool? isPrivate = null, bool? useInviteCode = null, bool? allowLateJoin = null, string region = null, Dictionary<string, object> customSettings = null, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("update lobby", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can update lobby settings."); return; }
            if (CurrentLobby?.status == "in_game") { onError?.Invoke("Cannot update lobby settings during an active game."); return; }
            if (name != null && (name.Length < 3 || name.Length > 50)) { onError?.Invoke("Lobby name must be between 3 and 50 characters."); return; }
            if (maxPlayers.HasValue && (maxPlayers.Value < 1 || maxPlayers.Value > 100)) { onError?.Invoke("Max players must be between 1 and 100."); return; }
            StartCoroutine(_operations.UpdateLobbyCoroutine(CurrentLobbyId, PlayerId, name, maxPlayers, isPrivate, useInviteCode, allowLateJoin, region, customSettings, lobby => {
                if (_refreshManager != null) _refreshManager.MarkLocalUpdate(lobby);
                UpdateCurrentLobby(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }

        public void UpdateLobbySettings(Dictionary<string, object> newSettings, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            UpdateLobby(customSettings: newSettings, onSuccess: onSuccess, onError: onError);
        }

        public void DeleteLobby(Action onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("delete lobby", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can delete the lobby."); return; }
            if (!IsInLobby) { onError?.Invoke("Not in a lobby."); return; }
            var lobbyId = CurrentLobbyId;
            StartCoroutine(_operations.DeleteLobbyCoroutine(lobbyId, PlayerId, () => {
                ClearCurrentLobby();
                onSuccess?.Invoke();
            }, onError));
        }

        public void FindLobbyByPlayerId(string playerId, Action<Lobby> onSuccess, Action<string> onError = null)
        {
            if (!ValidateOperation("find lobby by player", onError)) return;
            StartCoroutine(_operations.FindLobbyByPlayerIdCoroutine(playerId, onSuccess, onError));
        }

        public void TryReconnect(Action<Lobby> onReconnected, Action onNoLobbyFound, Action<string> onError = null)
        {
            if (!ValidateOperation("reconnect", onError)) return;
            FindLobbyByPlayerId(this.PlayerId, lobby => {
                if (lobby != null)
                {
                    SetCurrentLobby(lobby);
                    _events.InvokeLobbyJoined(lobby);
                    onReconnected?.Invoke(lobby);
                    if (_debugLogging) Debug.Log($"[PlayFlowLobbyManager] Successfully reconnected to lobby {lobby.id}");
                }
                else
                {
                    onNoLobbyFound?.Invoke();
                    if (_debugLogging) Debug.Log($"[PlayFlowLobbyManager] No active lobby found for player {PlayerId}.");
                }
            }, onError);
        }

        public ConnectionInfo? GetGameServerConnectionInfo()
        {
            if (CurrentLobby?.status != "in_game") return null;
            if (CurrentLobby.gameServer == null || !CurrentLobby.gameServer.ContainsKey("status") || CurrentLobby.gameServer["status"]?.ToString() != "running")
            {
                return null;
            }
            return Lobby.GetPrimaryConnectionInfo(CurrentLobby);
        }
    }
}