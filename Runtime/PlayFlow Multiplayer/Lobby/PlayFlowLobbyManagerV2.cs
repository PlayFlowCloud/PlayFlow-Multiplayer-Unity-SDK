using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PlayFlow
{
    /// <summary>
    /// A simple data structure to hold server connection information.
    /// </summary>
    public struct ConnectionInfo
    {
        public string Ip;
        public int Port;
    }

    /// <summary>
    /// The primary controller for managing PlayFlow lobbies.
    /// This class provides a singleton interface (`Instance`) for all lobby operations.
    ///
    /// HOW TO USE:
    /// 1. Add this component to a persistent GameObject in your scene (e.g., a "Managers" object).
    /// 2. Configure the API Key and other settings in the Inspector.
    /// 3. In your own script, get the instance and initialize it:
    ///    `PlayFlowLobbyManagerV2.Instance.Initialize("my-player-id", () => { Debug.Log("Manager is ready!"); });`
    /// 4. Subscribe to events via `PlayFlowLobbyManagerV2.Instance.Events` to react to lobby changes.
    /// 5. Call public methods like `CreateLobby`, `JoinLobby`, etc. to interact with the system.
    ///
    /// EXAMPLE - GETTING AN INVITE CODE:
    /// After creating a private lobby, the invite code is available on the `CurrentLobby` object
    /// or directly via the `InviteCode` property.
    ///
    /// <code>
    /// PlayFlowLobbyManagerV2.Instance.CreateLobby(
    ///     "My Private Lobby", 4, isPrivate: true,
    ///     onSuccess: (lobby) => {
    ///         Debug.Log($"Lobby created! Invite Code: {lobby.inviteCode}");
    ///         // Or access it anytime via the singleton instance:
    ///         string inviteCode = PlayFlowLobbyManagerV2.Instance.InviteCode;
    ///         // Now you can display this code in your UI for other players to use.
    ///     },
    ///     onError: (error) => {
    ///         Debug.LogError($"Failed to create lobby: {error}");
    ///     }
    /// );
    /// </code>
    /// </summary>
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
        private bool _hasFiredMatchRunningEvent;
        
        /// <summary>
        /// Singleton instance of the Lobby Manager. Access all lobby functionality through this.
        /// </summary>
        public static PlayFlowLobbyManagerV2 Instance { get; private set; }
        
        // Public API
        
        /// <summary>
        /// Returns true if the manager has been successfully initialized.
        /// </summary>
        public bool IsReady => _session != null && _session.IsInitialized;
        
        /// <summary>
        /// Gets the full data for the lobby the player is currently in. Returns null if not in a lobby.
        /// </summary>
        public Lobby CurrentLobby => _session?.CurrentLobby;
        
        /// <summary>
        /// Gets the unique ID of the current player, as provided during initialization.
        /// </summary>
        public string PlayerId => _session?.PlayerId;
        
        /// <summary>
        /// Gets the current state of the lobby session (e.g., Disconnected, InLobby, Searching).
        /// </summary>
        public LobbyState State => _session?.CurrentState ?? LobbyState.Disconnected;
        
        /// <summary>
        /// Provides access to all lobby-related UnityEvents (e.g., OnLobbyJoined, OnPlayerLeft).
        /// Subscribe to these to make your game UI react to lobby state changes.
        /// Example: `PlayFlowLobbyManagerV2.Instance.Events.OnLobbyUpdated.AddListener(myLobbyUpdateFunction);`
        /// </summary>
        public PlayFlowEvents Events => _events;
        
        /// <summary>
        /// A cached list of all currently available public lobbies. This list is updated automatically
        /// if auto-refresh is enabled.
        /// </summary>
        public List<Lobby> AvailableLobbies { get; private set; } = new List<Lobby>();
        
        // Helpers
        
        /// <summary>
        /// A quick check to see if the player is currently in a lobby.
        /// </summary>
        public bool IsInLobby => State == LobbyState.InLobby;
        
        /// <summary>
        /// Gets the ID of the current lobby. Returns null if not in a lobby.
        /// </summary>
        public string CurrentLobbyId => CurrentLobby?.id;
        
        /// <summary>
        /// Returns true if the current player is the host of the lobby they are in.
        /// </summary>
        public bool IsHost => IsInLobby && CurrentLobby?.host == PlayerId;
        
        /// <summary>
        /// Gets the invite code for the current private lobby. Returns null if not in a private lobby.
        /// This is the primary way to share access to a private lobby.
        /// </summary>
        public string InviteCode => CurrentLobby?.inviteCode;
        
        // Settings access
        
        /// <summary>Read-only access to the configured API Key.</summary>
        public string ApiKey => _apiKey;
        /// <summary>Read-only access to the configured Base URL.</summary>
        public string BaseUrl => _baseUrl;
        /// <summary>Read-only access to the default lobby configuration name.</summary>
        public string DefaultLobbyConfig => _defaultLobbyConfig;
        /// <summary>Read-only access to the lobby data refresh interval.</summary>
        public float RefreshInterval => _refreshInterval;
        /// <summary>Read-only access to the auto-refresh setting.</summary>
        public bool AutoRefresh => _autoRefresh;
        /// <summary>Read-only access to the debug logging setting.</summary>
        public bool Debugging => _debugLogging;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

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
        /// Initializes the lobby manager. This must be called before any other lobby operations.
        /// The API Key must be set in the inspector.
        /// </summary>
        /// <param name="playerId">A unique identifier for the local player.</param>
        /// <param name="onComplete">An optional callback that is invoked when initialization is successful.</param>
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
        /// Creates a new lobby with detailed configuration.
        /// </summary>
        /// <param name="name">The public name of the lobby.</param>
        /// <param name="maxPlayers">The maximum number of players that can join.</param>
        /// <param name="isPrivate">If true, the lobby will not appear in public searches and will require an invite code or direct ID to join.</param>
        /// <param name="allowLateJoin">If true, players can join even after the match has started.</param>
        /// <param name="region">The server region for the lobby (e.g., "us-west").</param>
        /// <param name="customSettings">A dictionary of custom game-specific settings.</param>
        /// <param name="onSuccess">Callback invoked with the created lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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
        /// Creates a new lobby with simple, common parameters. The lobby will be public by default.
        /// </summary>
        /// <param name="name">The public name of the lobby.</param>
        /// <param name="maxPlayers">The maximum number of players that can join.</param>
        /// <param name="isPrivate">If true, the lobby will not appear in public searches and will require an invite code to join.</param>
        /// <param name="onSuccess">Callback invoked with the created lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
        public void CreateLobby(string name, int maxPlayers = 4, bool isPrivate = false, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            CreateLobby(name, maxPlayers, isPrivate, true, "us-west", new Dictionary<string, object>(), onSuccess, onError);
        }
        
        /// <summary>
        /// Joins an existing lobby using its unique ID. For joining with an invite code, use `JoinLobbyByCode`.
        /// </summary>
        /// <param name="lobbyId">The unique ID (UUID) of the lobby to join.</param>
        /// <param name="onSuccess">Callback invoked with the joined lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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
        
        /// <summary>
        /// Joins an existing private lobby using its invite code.
        /// </summary>
        /// <param name="inviteCode">The invite code of the lobby to join.</param>
        /// <param name="onSuccess">Callback invoked with the joined lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
        public void JoinLobbyByCode(string inviteCode, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("join lobby by code", onError))
                return;

            if (string.IsNullOrEmpty(inviteCode))
            {
                onError?.Invoke("Invite Code cannot be empty");
                return;
            }

            StartCoroutine(_operations.JoinLobbyByCodeCoroutine(inviteCode, PlayerId, lobby =>
            {
                _session.SetCurrentLobby(lobby);
                _events.InvokeLobbyJoined(lobby);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        /// <summary>
        /// Leaves the lobby the player is currently in.
        /// </summary>
        /// <param name="onSuccess">Callback invoked on successfully leaving the lobby.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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
        
        /// <summary>
        /// Updates the local player's custom state data within the lobby.
        /// This is useful for synchronizing non-critical data like character selection, ready status, etc.
        /// The update will be broadcast to all other players in the lobby via the `OnLobbyUpdated` event.
        /// </summary>
        /// <param name="state">A dictionary representing the player's custom data.</param>
        /// <param name="onSuccess">Callback invoked with the updated lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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
            // A player updates their own state, so requester and target are the same.
            StartCoroutine(_operations.UpdatePlayerStateCoroutine(lobbyId, PlayerId, PlayerId, state, lobby =>
            {
                _session.UpdateCurrentLobby(lobby);
                _events.InvokePlayerStateChanged(PlayerId);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        /// <summary>
        /// [Host-Only] Updates another player's custom state data within the lobby.
        /// This is useful for authoritative game logic where the host controls player properties.
        /// </summary>
        /// <param name="targetPlayerId">The ID of the player whose state is being updated.</param>
        /// <param name="state">A dictionary representing the player's custom data.</param>
        /// <param name="onSuccess">Callback invoked with the updated lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
        public void UpdateStateForPlayer(string targetPlayerId, Dictionary<string, object> state, Action<Lobby> onSuccess = null, Action<string> onError = null)
        {
            if (!ValidateOperation("update state for player", onError)) return;
            if (!IsHost) { onError?.Invoke("Only the host can update another player's state."); return; }
            if (!_session.IsInLobby) { onError?.Invoke("Not in a lobby"); return; }
            if (string.IsNullOrEmpty(targetPlayerId)) { onError?.Invoke("Target Player ID cannot be empty."); return; }

            var lobbyId = CurrentLobby.id;
            StartCoroutine(_operations.UpdatePlayerStateCoroutine(lobbyId, PlayerId, targetPlayerId, state, lobby =>
            {
                _session.UpdateCurrentLobby(lobby);
                _events.InvokePlayerStateChanged(targetPlayerId);
                onSuccess?.Invoke(lobby);
            }, onError));
        }
        
        /// <summary>
        /// Fetches a list of all available public lobbies.
        /// </summary>
        /// <param name="onSuccess">Callback invoked with the list of lobbies on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
        public void GetAvailableLobbies(Action<List<Lobby>> onSuccess, Action<string> onError = null)
        {
            if (!ValidateOperation("get lobbies", onError))
                return;
                
            StartCoroutine(_operations.ListLobbiesCoroutine(lobbies => {
                this.AvailableLobbies = lobbies;
                onSuccess?.Invoke(lobbies);
            }, onError));
        }
        
        /// <summary>
        /// Starts the match. This can only be called by the lobby host.
        /// Changes the lobby status to "in_game", which can prevent new players from joining (depending on lobby settings).
        /// </summary>
        /// <param name="onSuccess">Callback invoked with the updated lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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

        /// <summary>
        /// Ends the match. This can only be called by the lobby host.
        /// Changes the lobby status back to "waiting".
        /// </summary>
        /// <param name="onSuccess">Callback invoked with the updated lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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
        
        /// <summary>
        /// Manually refreshes the data for the current lobby.
        /// This is useful to call after specific actions or to ensure the local state is perfectly in sync.
        /// </summary>
        /// <param name="onSuccess">Callback invoked with the updated lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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
        /// This will refresh both the available lobby list and the current lobby state if applicable.
        /// </summary>
        public void ForceRefresh()
        {
            _refreshManager?.ForceRefresh();
        }

        /// <summary>
        /// Disconnects the manager, stops all background refreshing, and clears the session state.
        /// </summary>
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

                // Check if the server is now running and we haven't told the user yet.
                if (lobby.status == "in_game" && !_hasFiredMatchRunningEvent)
                {
                    var connectionInfo = GetGameServerConnectionInfo();
                    if (connectionInfo.HasValue)
                    {
                        _events.InvokeMatchRunning(connectionInfo.Value);
                        _hasFiredMatchRunningEvent = true;
                    }
                }
                else if (lobby.status != "in_game")
                {
                    // Reset the flag when the match is no longer in progress.
                    _hasFiredMatchRunningEvent = false;
                }
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
            
            if (Instance == this)
            {
                Instance = null;
            }
            
            if (_runtimeSettings != null)
            {
                DestroyImmediate(_runtimeSettings);
            }
        }

        /// <summary>
        /// Kicks a player from the lobby. This can only be called by the host.
        /// </summary>
        /// <param name="playerToKickId">The unique ID of the player to remove from the lobby.</param>
        /// <param name="onSuccess">Callback invoked with the updated lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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

        /// <summary>
        /// Transfers host privileges to another player in the lobby. This can only be called by the current host.
        /// </summary>
        /// <param name="newHostId">The unique ID of the player who will become the new host.</param>
        /// <param name="onSuccess">Callback invoked with the updated lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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

        /// <summary>
        /// Updates the lobby's custom settings. This can only be called by the host.
        /// </summary>
        /// <param name="newSettings">A dictionary of new settings to apply to the lobby.</param>
        /// <param name="onSuccess">Callback invoked with the updated lobby data on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
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

        /// <summary>
        /// Finds the lobby that a specific player is currently in.
        /// This is useful for reconnecting or for social features like "join friend's game".
        /// </summary>
        /// <param name="playerId">The ID of the player to find.</param>
        /// <param name="onSuccess">Callback invoked with the found lobby data, or null if no lobby is found.</param>
        /// <param name="onError">Callback invoked with an error message on failure (but not for 404 "Not Found" errors).</param>
        public void FindLobbyByPlayerId(string playerId, Action<Lobby> onSuccess, Action<string> onError = null)
        {
            if (!ValidateOperation("find lobby by player", onError)) return;
            StartCoroutine(_operations.FindLobbyByPlayerIdCoroutine(playerId, onSuccess, onError));
        }

        /// <summary>
        /// Attempts to reconnect the current player to a lobby they might have been disconnected from.
        /// It checks if the player is in an existing lobby and, if so, automatically restores the session.
        /// </summary>
        /// <param name="onReconnected">Callback invoked with the lobby data if reconnection is successful.</param>
        /// <param name="onNoLobbyFound">Callback invoked if no active lobby is found for the player.</param>
        /// <param name="onError">Callback invoked if a system error occurs during the check.</param>
        public void TryReconnect(Action<Lobby> onReconnected, Action onNoLobbyFound, Action<string> onError = null)
        {
            if (!ValidateOperation("reconnect", onError)) return;

            FindLobbyByPlayerId(this.PlayerId,
                lobby =>
                {
                    if (lobby != null)
                    {
                        _session.SetCurrentLobby(lobby);
                        _events.InvokeLobbyJoined(lobby); // Fire standard joined event
                        onReconnected?.Invoke(lobby);
                        if (_debugLogging) Debug.Log($"[PlayFlowLobbyManager] Successfully reconnected to lobby {lobby.id}");
                    }
                    else
                    {
                        onNoLobbyFound?.Invoke();
                        if (_debugLogging) Debug.Log($"[PlayFlowLobbyManager] No active lobby found for player {PlayerId}.");
                    }
                },
                onError);
        }

        /// <summary>
        /// Gets the connection details (IP and Port) for the game server if the lobby is in an active match.
        /// </summary>
        /// <returns>A ConnectionInfo struct if the server is running, otherwise null.</returns>
        public ConnectionInfo? GetGameServerConnectionInfo()
        {
            if (CurrentLobby?.status != "in_game" || CurrentLobby.gameServer == null)
            {
                return null;
            }

            try
            {
                // Parse the gameServer dictionary into a JObject for easier traversal
                var gameServerJson = JObject.FromObject(CurrentLobby.gameServer);
                
                // Explicitly check if the server status is "running".
                if (gameServerJson["status"]?.ToString() != "running")
                {
                    return null;
                }

                var networkPorts = gameServerJson["network_ports"] as JArray;

                if (networkPorts == null || networkPorts.Count == 0)
                {
                    return null;
                }

                // Find the primary game port (usually UDP) or return the first port available.
                var gamePort = networkPorts.FirstOrDefault(p => p["protocol"]?.ToString() == "udp")
                               ?? networkPorts.First();

                return new ConnectionInfo
                {
                    Ip = gamePort["host"]?.ToString(),
                    Port = gamePort["external_port"]?.ToObject<int>() ?? 0
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayFlowLobbyManager] Error parsing game server info: {e.Message}");
                return null;
            }
        }
    }
} 