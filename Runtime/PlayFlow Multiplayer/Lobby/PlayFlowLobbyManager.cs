using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using PlayFlow;

/// <summary>
/// Main component for managing multiplayer game lobbies using the PlayFlow service.
/// This manager provides a high-level API for creating, joining, leaving, and managing lobbies.
/// 
/// Setup:
/// 1. Attach this component to a GameObject in your scene (e.g., a "LobbyController").
/// 2. Configure the `apiKey` and `lobbyConfigName` in the Inspector.
/// 3. In your own script, get a reference to this component.
/// 4. Call `SetPlayerInfo()` with a unique ID for the local player before making any other calls.
/// 5. Subscribe to the public events (e.g., `lobbyListEvents`, `individualLobbyEvents`) to respond to lobby changes.
/// </summary>
public class PlayFlowLobbyManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The base URL of the PlayFlow backend.")]
    [SerializeField] private string baseUrl = "https://backend.computeflow.cloud";
    [Tooltip("Your project's API Key from the PlayFlow dashboard.")]
    [SerializeField] private string apiKey;
    [Tooltip("The name of the Lobby Configuration to use (e.g., 'Default', 'Ranked').")]
    [SerializeField] private string lobbyConfigName = "firstLobby";
    [Tooltip("How often to refresh the lobby list or current lobby state (in seconds). Minimum is 3.")] 
    [SerializeField] private float refreshInterval = 5f;
    [Tooltip("Enable or disable automatic refreshing of lobby data.")]
    [SerializeField] private bool autoRefresh = true;
    [Tooltip("Enable detailed logging to the console for debugging.")]
    [SerializeField] private bool debugLogging = false;

    /// <summary>
    /// Gets a value indicating whether detailed debug logging is enabled.
    /// </summary>
    public bool DebugLogging => debugLogging;
    
    /// <summary>
    /// Gets or sets the refresh interval for lobby data. Minimum value is 3 seconds.
    /// </summary>
    public float RefreshInterval { get => refreshInterval; set => refreshInterval = Mathf.Max(3f, value); }

    [Header("Events")]
    [Tooltip("Events related to the list of all available lobbies.")]
    public PlayFlowLobbyEvents.LobbyListEvents lobbyListEvents = new PlayFlowLobbyEvents.LobbyListEvents();
    [Tooltip("Events related to the specific lobby the player is currently in.")]
    public PlayFlowLobbyEvents.IndividualLobbyEvents individualLobbyEvents = new PlayFlowLobbyEvents.IndividualLobbyEvents();
    [Tooltip("Events related to players joining, leaving, or changing state.")]
    public PlayFlowLobbyEvents.PlayerEvents playerEvents = new PlayFlowLobbyEvents.PlayerEvents();
    [Tooltip("Events related to the match lifecycle (e.g., match started, ended).")]
    public PlayFlowLobbyEvents.MatchEvents matchEvents = new PlayFlowLobbyEvents.MatchEvents();
    [Tooltip("System-level events for monitoring API calls and errors.")]
    public PlayFlowLobbyEvents.SystemEvents systemEvents = new PlayFlowLobbyEvents.SystemEvents();


    private LobbyClient _lobbyClient;
    private PlayFlowLobbyActions _lobbyActions;
    private PlayFlowLobbyComparer _lobbyComparer;
    private PlayFlowLobbyRefresher _lobbyRefresher;
    private PlayFlowGameServerUtility _gameServerUtility;
    private PlayFlowLobbyStateManager _stateManager;
    
    private string _playerId;
    private bool _isInitialized = false;
    private float _lastRefreshTime;
    private Coroutine _refreshCoroutine;
    private bool _isRefreshing = false;

    private void Awake()
    {
        _lobbyClient = new LobbyClient(baseUrl, apiKey);
        _stateManager = new PlayFlowLobbyStateManager();

        var eventLogger = new PlayFlowLobbyEvents.EventLogger();
        eventLogger.Initialize(debugLogging);
        
        lobbyListEvents.Initialize(eventLogger);
        individualLobbyEvents.Initialize(eventLogger);
        playerEvents.Initialize(eventLogger);
        matchEvents.Initialize(eventLogger);
        systemEvents.Initialize(eventLogger);
        
        _lobbyActions = new PlayFlowLobbyActions(_lobbyClient, lobbyConfigName, "uninitialized-player", this, this, systemEvents, individualLobbyEvents, Debug.Log, Debug.LogError);
        _lobbyComparer = new PlayFlowLobbyComparer(lobbyListEvents, individualLobbyEvents, playerEvents, matchEvents, debugLogging);
        _lobbyRefresher = new PlayFlowLobbyRefresher(_lobbyActions, _lobbyComparer, lobbyListEvents, debugLogging);
        _gameServerUtility = new PlayFlowGameServerUtility(individualLobbyEvents, debugLogging);
        
        _stateManager.OnStateChanged += (oldLobby, newLobby) => _lobbyComparer.CompareAndFireLobbyEvents(oldLobby, newLobby);
    }
    
    private void Start()
    {
        // Auto-refresh lobbies on start if initialized
        if (_isInitialized)
        {
            RefreshLobbies();
        }
    }
    
    private void Update()
    {
        if (!autoRefresh || !_isInitialized || _isRefreshing || Time.time - _lastRefreshTime < refreshInterval) return;
        
        _lastRefreshTime = Time.time;
        
        if (_refreshCoroutine == null)
        {
            _isRefreshing = true;
            if (IsInLobby())
            {
                _refreshCoroutine = StartCoroutine(RefreshCurrentLobbyCoroutine());
            }
            else
            {
                _refreshCoroutine = StartCoroutine(RefreshLobbiesCoroutine());
            }
        }
    }

    /// <summary>
    /// Sets the local player's information. This must be called before any other lobby operations.
    /// </summary>
    /// <param name="playerId">A unique identifier for the local player.</param>
    public void SetPlayerInfo(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("Player ID cannot be null or empty.");
            return;
        }
        this._playerId = playerId;
        _lobbyActions.SetPlayerId(playerId);
        _isInitialized = true;
        
        // Automatically refresh lobbies once the player ID is set
        RefreshLobbies();
    }
    
    private IEnumerator RefreshCurrentLobbyCoroutine()
    {
        yield return _lobbyRefresher.RefreshCurrentLobbyCoroutine(GetCurrentLobbyId());
        _refreshCoroutine = null;
        _isRefreshing = false;
    }

    /// <summary>
    /// Manually triggers a refresh of the public lobby list.
    /// </summary>
    public void RefreshLobbies()
    {
        if (_refreshCoroutine == null && !_isRefreshing)
        {
            _isRefreshing = true;
            _refreshCoroutine = StartCoroutine(RefreshLobbiesCoroutine());
        }
    }

    private IEnumerator RefreshLobbiesCoroutine()
    {
        yield return _lobbyRefresher.RefreshLobbiesCoroutine();
        _refreshCoroutine = null;
        _isRefreshing = false;
    }
    
    private bool IsInitializedWithPlayerId(Action<Exception> onError, string operationName)
    {
        if (!_isInitialized)
        {
            onError?.Invoke(new InvalidOperationException($"SetPlayerInfo must be called before {operationName}."));
            return false;
        }
        return true;
    }
    
    /// <summary>
    /// Creates a new lobby with the specified settings.
    /// </summary>
    /// <param name="lobbyName">The public name of the lobby.</param>
    /// <param name="maxPlayers">The maximum number of players that can join.</param>
    /// <param name="isPrivate">If true, the lobby will not appear in public listings and will require an invite code.</param>
    /// <param name="allowLateJoin">If true, players can join even after the match has started.</param>
    /// <param name="region">The server region for the lobby.</param>
    /// <param name="customLobbySettings">A dictionary of custom key-value pairs for game-specific settings.</param>
    /// <param name="onSuccess">Callback invoked with the created Lobby object on success.</param>
    /// <param name="onError">Callback invoked with an Exception on failure.</param>
    public void CreateLobby(
        string lobbyName, 
        int maxPlayers, 
        bool isPrivate, 
        bool allowLateJoin, 
        string region,
        Dictionary<string, object> customLobbySettings, 
        Action<Lobby> onSuccess, 
        Action<Exception> onError)
    {
        if (!IsInitializedWithPlayerId(onError, "creating a lobby"))
        {
            return;
        }

        PlayFlowRequestQueue.Instance.EnqueueOperation(
            "CreateLobby",
            () => _lobbyActions.CreateLobbyCoroutine(lobbyName, maxPlayers, isPrivate, allowLateJoin, region, customLobbySettings, 
                (lobby) => {
                    _stateManager.TryUpdateState(lobby, true);
                    individualLobbyEvents.InvokeLobbyCreated(lobby);
                    RefreshLobbies();
                    onSuccess?.Invoke(lobby);
                }, 
                onError),
            onError
        );
    }
    
    /// <summary>
    /// Joins an existing lobby by its ID.
    /// </summary>
    /// <param name="lobbyId">The unique ID of the lobby to join.</param>
    /// <param name="onSuccess">Callback invoked with the joined Lobby object on success.</param>
    /// <param name="onError">Callback invoked with an Exception on failure.</param>
    public void JoinLobby(string lobbyId, Action<Lobby> onSuccess, Action<Exception> onError)
    {
        if (!IsInitializedWithPlayerId(onError, "joining a lobby"))
        {
            return;
        }
        
        PlayFlowRequestQueue.Instance.EnqueueOperation(
            "JoinLobby",
            () => _lobbyActions.JoinLobbyCoroutine(lobbyId, 
                (lobby) => {
                    _stateManager.TryUpdateState(lobby, true);
                    RefreshLobbies();
                    onSuccess?.Invoke(lobby);
                },
                onError),
            onError
        );
    }

    /// <summary>
    /// Leaves the current lobby.
    /// </summary>
    /// <param name="onSuccess">Callback invoked on successfully leaving the lobby.</param>
    /// <param name="onError">Callback invoked with an Exception on failure.</param>
    public void LeaveLobby(Action onSuccess, Action<Exception> onError)
    {
        if (!IsInLobby()) { onError?.Invoke(new InvalidOperationException("Not in a lobby.")); return; }
        
        string lobbyIdToLeave = GetCurrentLobbyId();

        PlayFlowRequestQueue.Instance.EnqueueOperation(
            "LeaveLobby",
            () => _lobbyActions.LeaveLobbyCoroutine(lobbyIdToLeave,
                (success) => {
                    if (success) {
                        _stateManager.TryUpdateState(null, true);
                        individualLobbyEvents.InvokeLobbyLeft();
                        RefreshLobbies();
                        onSuccess?.Invoke();
                    }
                },
                onError),
            onError
        );
    }

    /// <summary>
    /// Gets the current lobby the player is in. Returns null if not in a lobby.
    /// </summary>
    public Lobby GetCurrentLobby() => _stateManager.CurrentLobby;
    
    /// <summary>
    /// Gets the latest list of available public lobbies.
    /// </summary>
    public List<Lobby> GetAvailableLobbies() => _lobbyRefresher.GetAvailableLobbies();
    
    /// <summary>
    /// Returns true if the player is currently in a lobby.
    /// </summary>
    public bool IsInLobby() => GetCurrentLobby() != null;

    /// <summary>
    /// Gets the unique ID of the local player.
    /// </summary>
    public string GetPlayerId() => _playerId;

    /// <summary>
    /// Gets the ID of the current lobby. Returns null if not in a lobby.
    /// </summary>
    public string GetCurrentLobbyId() => GetCurrentLobby()?.id;
    
    /// <summary>
    /// Gets the name of the current lobby. Returns null if not in a lobby.
    /// </summary>
    public string GetCurrentLobbyName() => GetCurrentLobby()?.name;

    /// <summary>
    /// Gets the number of players in the current lobby. Returns 0 if not in a lobby.
    /// </summary>
    public int GetCurrentPlayerCount() => GetCurrentLobby()?.currentPlayers ?? 0;
    
    /// <summary>
    /// Gets the status of the current lobby (e.g., "waiting", "in_game"). Returns null if not in a lobby.
    /// </summary>
    public string GetCurrentLobbyStatus() => GetCurrentLobby()?.status;
    
    /// <summary>
    /// Returns true if the local player is the host of the current lobby.
    /// </summary>
    public bool IsHost() => IsInLobby() && GetCurrentLobby().host == _playerId;
    
    /// <summary>
    /// Gets a list of player IDs in the current lobby. Returns null if not in a lobby.
    /// </summary>
    public string[] GetCurrentPlayers() => GetCurrentLobby()?.players;
    
    /// <summary>
    /// Gets the game server connection information for the current lobby. Returns null if no server is active.
    /// </summary>
    public Dictionary<string, object> GetGameServerInfo() => GetCurrentLobby()?.gameServer;
    
    /// <summary>
    /// Gets the invite code for the current lobby. Returns null if the lobby has no invite code.
    /// </summary>
    public string GetInviteCode() => GetCurrentLobby()?.inviteCode;
    
    public void SendPlayerStateUpdate(string currentLobbyId, Dictionary<string, object> state, Action<Lobby> onSuccess, Action<Exception> onError)
    {
        if (!IsInitializedWithPlayerId(onError, "sending a player state update"))
        {
            return;
        }
        if (string.IsNullOrEmpty(currentLobbyId))
        {
            onError?.Invoke(new ArgumentException("Lobby ID cannot be null or empty."));
            return;
        }
        _lobbyActions.SendPlayerStateUpdate(currentLobbyId, state, onSuccess, onError);
    }
    
    private void OnDestroy()
    {
        // Stop any running coroutines
        if (_refreshCoroutine != null)
        {
            StopCoroutine(_refreshCoroutine);
            _refreshCoroutine = null;
        }
        
        // Clear state
        _stateManager?.Clear();
        
        // Unsubscribe from events to prevent memory leaks
        if (_stateManager != null)
        {
            _stateManager.OnStateChanged -= _lobbyComparer.CompareAndFireLobbyEvents;
        }
        
        // Reset flags
        _isRefreshing = false;
        _isInitialized = false;
    }
}

