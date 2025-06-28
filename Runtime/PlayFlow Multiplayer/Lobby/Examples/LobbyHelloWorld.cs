using UnityEngine;
using PlayFlow; // Assuming PlayFlowLobbyManager and Lobby models are in this namespace
using System.Collections.Generic;
using System.Linq; // For FirstOrDefault
using System.Threading.Tasks;



/// <summary>
/// A comprehensive example script demonstrating how to use the PlayFlowLobbyManager for multiplayer game lobbies.
/// This script provides a complete implementation of lobby functionality including:
/// - Creating and joining lobbies
/// - Managing lobby settings and player states
/// - Handling match start/end
/// - Managing host privileges
/// - Responding to lobby events
///
/// Setup Instructions:
/// 1. Create an empty GameObject in your scene (e.g., "LobbyDemoManager")
/// 2. Attach this LobbyHelloWorld.cs script to it
/// 3. Attach the PlayFlowLobbyManager.cs script to the same GameObject or another one
/// 4. In the Inspector, assign the PlayFlowLobbyManager to the 'lobbyManager' field
/// 5. Configure the PlayFlowLobbyManager with your:
///    - API Key (from your PlayFlow dashboard)
///    - Base URL (default: https://backend.computeflow.cloud)
///    - Lobby Config Name (your configured lobby type)
///
/// UI Integration:
/// To create a lobby UI, you can:
/// 1. Create UI buttons in your scene
/// 2. In the Unity Inspector, add OnClick() events to these buttons
/// 3. Drag this LobbyHelloWorld component to the OnClick() event
/// 4. Select the appropriate method (e.g., DoCreateLobbyOnClick)
///
/// Example UI Button Setup:
/// - Create Lobby Button -> LobbyHelloWorld.DoCreateLobbyOnClick
/// - Join Lobby Button -> LobbyHelloWorld.DoJoinFirstAvailablePublicLobbyOnClick
/// - Leave Lobby Button -> LobbyHelloWorld.DoLeaveCurrentLobbyOnClick
/// - Start Match Button -> LobbyHelloWorld.DoStartMatchOnClick
///
/// Event Handling:
/// This script automatically subscribes to all lobby events and logs them to the console.
/// You can modify the event handlers to update your UI or game state accordingly.
///
/// Testing:
/// 1. Run the scene
/// 2. Check the console for detailed logs of all lobby operations
/// 3. Use the UI buttons to interact with the lobby system
/// 4. Monitor the console for event notifications
///
/// Note: This is a demo implementation. In a production environment, you should:
/// - Add proper error handling and user feedback
/// - Implement UI state management
/// - Add loading indicators during API calls
/// - Handle edge cases and network issues
/// </summary>
public class LobbyHelloWorld : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign your PlayFlowLobbyManager instance here. This is required for the script to function.")]
    public PlayFlowLobbyManager lobbyManager;

    [Header("Test Settings")]
    [Tooltip("Base Player ID for this test client. A unique suffix will be added to make it unique.")]
    public string baseTestPlayerId = "LobbyUser";
    
    [Tooltip("Default name for lobbies created by this script. Can be overridden when creating a lobby.")]
    public string defaultLobbyName = "MyTestLobby";
    
    [Tooltip("Default maximum number of players allowed in lobbies created by this script.")]
    [Range(1, 32)] // Reasonable range for most games, min 1 for testing
    public int defaultMaxPlayers = 1;
    
    [Tooltip("Whether lobbies created by this script should be private (require invite code) or public.")]
    public bool defaultIsPrivate = false;

    [Header("Startup Options")]
    [Tooltip("If true, this example script will attempt to rejoin any existing lobby the player might be in when the scene starts.")]
    public bool tryToRejoinPreviousLobbyOnStart = true;

    // Internal tracking of the unique player ID for this instance
    private string _uniquePlayerId;

    void Start()
    {
        if (lobbyManager == null)
        {
            Debug.LogError("[LobbyHelloWorld] PlayFlowLobbyManager is not assigned! Please assign it in the Inspector.");
            enabled = false; // Disable this script if manager is missing
            return;
        }

        // Generate a somewhat unique player ID for this session
        _uniquePlayerId = baseTestPlayerId + "_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        lobbyManager.SetPlayerInfo(_uniquePlayerId);
        Debug.Log($"[LobbyHelloWorld] Initialized with Player ID: {_uniquePlayerId}");

        // Subscribe to events
        SubscribeToLobbyEvents();

        // Optionally, refresh lobbies on start
        Debug.Log("[LobbyHelloWorld] Refreshing lobby list on start...");
        // Fire and forget initial refresh
        _ = RefreshLobbiesAsync();

        // Optionally, try to rejoin an existing lobby if the player was already in one
        if (tryToRejoinPreviousLobbyOnStart)
        {
            Debug.Log("[LobbyHelloWorld] Attempting to check and rejoin existing lobby...");
            _ = DoCheckAndRejoinExistingLobbyAsync();
        }
    }

    void OnDestroy()
    {
        if (lobbyManager != null)
        {
            UnsubscribeFromLobbyEvents();
        }
    }

    void SubscribeToLobbyEvents()
    {
        if (lobbyManager == null) return;

        lobbyManager.lobbyListEvents.onLobbiesRefreshed.AddListener(HandleLobbiesRefreshed);
        lobbyManager.lobbyListEvents.onLobbyAdded.AddListener(HandleLobbyAddedToList);
        lobbyManager.lobbyListEvents.onLobbyRemoved.AddListener(HandleLobbyRemovedFromList);
        lobbyManager.lobbyListEvents.onLobbyModified.AddListener(HandleLobbyModifiedInList);
        
        lobbyManager.individualLobbyEvents.onLobbyCreated.AddListener(HandleLobbyCreated);
        lobbyManager.individualLobbyEvents.onLobbyJoined.AddListener(HandleLobbyJoined);
        lobbyManager.individualLobbyEvents.onLobbyLeft.AddListener(HandleLobbyLeft);
        lobbyManager.individualLobbyEvents.onLobbyUpdated.AddListener(HandleLobbyUpdated);
        lobbyManager.individualLobbyEvents.onLobbySettingsUpdated.AddListener(HandleLobbySettingsUpdated);

        lobbyManager.playerEvents.onPlayerJoined.AddListener(HandlePlayerJoinedLobby);
        lobbyManager.playerEvents.onPlayerLeft.AddListener(HandlePlayerLeftLobby);
        lobbyManager.playerEvents.onHostTransferred.AddListener(HandleHostTransferred);
        lobbyManager.playerEvents.onPlayerStateRealTimeUpdated.AddListener(HandlePlayerStateRealTimeUpdated);
        
        lobbyManager.matchEvents.onMatchStarted.AddListener(HandleMatchStarted);
        lobbyManager.matchEvents.onMatchEnded.AddListener(HandleMatchEnded);

        lobbyManager.systemEvents.onError.AddListener(HandleError);
        lobbyManager.systemEvents.onPreAPICall.AddListener(HandlePreAPICall);
        lobbyManager.systemEvents.onPostAPICall.AddListener(HandlePostAPICall);
        
        Debug.Log("[LobbyHelloWorld] Subscribed to PlayFlowLobbyManager events.");
    }

    void UnsubscribeFromLobbyEvents()
    {
        if (lobbyManager == null) return;

        lobbyManager.lobbyListEvents.onLobbiesRefreshed.RemoveListener(HandleLobbiesRefreshed);
        lobbyManager.lobbyListEvents.onLobbyAdded.RemoveListener(HandleLobbyAddedToList);
        lobbyManager.lobbyListEvents.onLobbyRemoved.RemoveListener(HandleLobbyRemovedFromList);
        lobbyManager.lobbyListEvents.onLobbyModified.RemoveListener(HandleLobbyModifiedInList);

        lobbyManager.individualLobbyEvents.onLobbyCreated.RemoveListener(HandleLobbyCreated);
        lobbyManager.individualLobbyEvents.onLobbyJoined.RemoveListener(HandleLobbyJoined);
        lobbyManager.individualLobbyEvents.onLobbyLeft.RemoveListener(HandleLobbyLeft);
        lobbyManager.individualLobbyEvents.onLobbyUpdated.RemoveListener(HandleLobbyUpdated);
        lobbyManager.individualLobbyEvents.onLobbySettingsUpdated.RemoveListener(HandleLobbySettingsUpdated);
        
        lobbyManager.playerEvents.onPlayerJoined.RemoveListener(HandlePlayerJoinedLobby);
        lobbyManager.playerEvents.onPlayerLeft.RemoveListener(HandlePlayerLeftLobby);
        lobbyManager.playerEvents.onHostTransferred.RemoveListener(HandleHostTransferred);
        lobbyManager.playerEvents.onPlayerStateRealTimeUpdated.RemoveListener(HandlePlayerStateRealTimeUpdated);

        lobbyManager.matchEvents.onMatchStarted.RemoveListener(HandleMatchStarted);
        lobbyManager.matchEvents.onMatchEnded.RemoveListener(HandleMatchEnded);

        lobbyManager.systemEvents.onError.RemoveListener(HandleError);
        lobbyManager.systemEvents.onPreAPICall.RemoveListener(HandlePreAPICall);
        lobbyManager.systemEvents.onPostAPICall.RemoveListener(HandlePostAPICall);
        
        Debug.Log("[LobbyHelloWorld] Unsubscribed from PlayFlowLobbyManager events.");
    }

    #region Public Methods for UI Buttons or External Calls

    /// <summary>
    /// Refreshes the list of available lobbies.
    /// Call this when you want to update the lobby list in your UI.
    /// </summary>
    public async void DoRefreshLobbiesOnClick()
    {
        Debug.Log("[LobbyHelloWorld] Refreshing lobbies via UI button...");
        try
        {
            await lobbyManager.RefreshLobbiesAsync();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error refreshing lobbies: {ex.Message}");
        }
    }

    // Helper method for programmatic use (returns Task for proper async flow)
    public async Task RefreshLobbiesAsync()
    {
        try
        {
            await lobbyManager.RefreshLobbiesAsync();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error refreshing lobbies: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new lobby with the default settings specified in the Inspector.
    /// The lobby will be created with:
    /// - Name: defaultLobbyName
    /// - Max Players: defaultMaxPlayers
    /// - Privacy: defaultIsPrivate
    /// </summary>
    public async void DoCreateLobbyOnClick()
    {
        Debug.Log($"[LobbyHelloWorld] Attempting to create lobby with name: {defaultLobbyName}");
        try
        {
            lobbyManager.SetLobbyCreationSettings(defaultLobbyName, defaultMaxPlayers, defaultIsPrivate);
            Lobby createdLobby = await lobbyManager.CreateLobbyAsync();
            Debug.Log($"[LobbyHelloWorld] Successfully created lobby: {createdLobby.name} with ID: {createdLobby.id}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error creating lobby: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to join the first available public lobby that isn't full.
    /// This is useful for quick matchmaking or testing.
    /// </summary>
    public async void DoJoinFirstAvailablePublicLobbyOnClick()
    {
        if (lobbyManager.IsInLobby())
        {
            Debug.LogWarning("[LobbyHelloWorld] Already in a lobby. Please leave before joining another.");
            return;
        }

        try
        {
            // First refresh to get the latest list
            await lobbyManager.RefreshLobbiesAsync();
            
            List<Lobby> availableLobbies = lobbyManager.GetAvailableLobbies();
            if (availableLobbies == null || availableLobbies.Count == 0)
            {
                Debug.LogWarning("[LobbyHelloWorld] No lobbies available to join after refresh.");
                return;
            }

            Lobby publicLobbyToJoin = availableLobbies.FirstOrDefault(l => !l.isPrivate && l.currentPlayers < l.maxPlayers);

            if (publicLobbyToJoin != null)
            {
                Debug.Log($"[LobbyHelloWorld] Attempting to join lobby: {publicLobbyToJoin.name} ({publicLobbyToJoin.id})");
                Lobby joinedLobby = await lobbyManager.JoinLobbyAsync(publicLobbyToJoin.id);
                Debug.Log($"[LobbyHelloWorld] Successfully joined lobby: {joinedLobby.name}");
            }
            else
            {
                Debug.LogWarning("[LobbyHelloWorld] No available public lobbies found to join.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error joining lobby: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Joins a specific lobby using its invite code.
    /// Use this for private lobbies or when you have a specific lobby code.
    /// </summary>
    /// <param name="inviteCode">The invite code of the lobby to join</param>
    public async void DoJoinLobbyByCodeOnClick(string inviteCode)
    {
        if (string.IsNullOrEmpty(inviteCode))
        {
            Debug.LogWarning("[LobbyHelloWorld] Invite code cannot be empty.");
            return;
        }
        if (lobbyManager.IsInLobby())
        {
            Debug.LogWarning("[LobbyHelloWorld] Already in a lobby. Please leave before joining another.");
            return;
        }
        
        try
        {
            Debug.Log($"[LobbyHelloWorld] Attempting to join lobby by code: {inviteCode}");
            Lobby joinedLobby = await lobbyManager.JoinLobbyByCodeAsync(inviteCode);
            Debug.Log($"[LobbyHelloWorld] Successfully joined lobby by code: {joinedLobby.name}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error joining lobby by code: {ex.Message}");
        }
    }

    /// <summary>
    /// Leaves the current lobby.
    /// Call this when the player wants to exit the current lobby.
    /// </summary>
    public async void DoLeaveCurrentLobbyOnClick()
    {
        if (!lobbyManager.IsInLobby())
        {
            Debug.LogWarning("[LobbyHelloWorld] Not currently in a lobby.");
            return;
        }
        
        try
        {
            Debug.Log("[LobbyHelloWorld] Attempting to leave current lobby...");
            await lobbyManager.LeaveLobbyAsync();
            Debug.Log("[LobbyHelloWorld] Successfully left lobby.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error leaving lobby: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts a match in the current lobby.
    /// Only the host can start a match.
    /// This will trigger the match start process and allocate a game server.
    /// </summary>
    public async void DoStartMatchOnClick()
    {
        if (!lobbyManager.IsInLobby())
        {
            Debug.LogWarning("[LobbyHelloWorld] Not in a lobby to start a match.");
            return;
        }
        if (!lobbyManager.IsHost())
        {
            Debug.LogWarning("[LobbyHelloWorld] Only the host can start the match.");
            return;
        }
        
        try
        {
            Debug.Log("[LobbyHelloWorld] Attempting to start match...");
            Lobby updatedLobby = await lobbyManager.StartMatchAsync();
            Debug.Log($"[LobbyHelloWorld] Successfully started match. Lobby status: {updatedLobby.status}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error starting match: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Ends the current match/round.
    /// Only the host can end a match.
    /// This will return the lobby to its pre-match state.
    /// </summary>
    public async void DoEndMatchOnClick()
    {
        if (!lobbyManager.IsInLobby() || lobbyManager.GetCurrentLobbyStatus() != "in_game")
        {
            Debug.LogWarning("[LobbyHelloWorld] Not in a game to end, or lobby not in 'in_game' state.");
            return;
        }
        if (!lobbyManager.IsHost())
        {
            Debug.LogWarning("[LobbyHelloWorld] Only the host can end the match.");
            return;
        }
        
        try
        {
            Debug.Log("[LobbyHelloWorld] Attempting to end match (round)...");
            Lobby updatedLobby = await lobbyManager.EndGameAsync();
            Debug.Log($"[LobbyHelloWorld] Successfully ended match. Lobby status: {updatedLobby.status}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error ending match: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a sample player state update to the lobby.
    /// This demonstrates how to send custom player data to other players.
    /// In a real game, you would send actual player state data.
    /// </summary>
    public async void DoSendSamplePlayerStateOnClick()
    {
        if (!lobbyManager.IsInLobby())
        {
            Debug.LogWarning("[LobbyHelloWorld] Not in a lobby to send state.");
            return;
        }
        
        var sampleState = new Dictionary<string, object>
        {
            { "health", UnityEngine.Random.Range(50, 100) },
            { "status", "ready_for_action" },
            { "position", new Dictionary<string, float> { {"x", UnityEngine.Random.Range(0f, 10f)}, {"y", UnityEngine.Random.Range(0f, 10f)} } }
        };
        
        try
        {
            Debug.Log($"[LobbyHelloWorld] Sending sample player state: {Newtonsoft.Json.JsonConvert.SerializeObject(sampleState)}");
            bool success = await lobbyManager.SendPlayerStateUpdateAsync(sampleState);
            if (success)
            {
                Debug.Log("[LobbyHelloWorld] Successfully sent player state update.");
            }
            else
            {
                Debug.LogWarning("[LobbyHelloWorld] Failed to send player state update.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error sending player state: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Transfers host privileges to another player.
    /// Only the current host can transfer host privileges.
    /// </summary>
    /// <param name="newHostPlayerId">The player ID of the new host</param>
    public async void DoTransferHostOnClick(string newHostPlayerId)
    {
        if (!lobbyManager.IsInLobby())
        {
            Debug.LogWarning("[LobbyHelloWorld] Not in a lobby to transfer host.");
            return;
        }
        if (!lobbyManager.IsHost())
        {
            Debug.LogWarning("[LobbyHelloWorld] Only the current host can transfer ownership.");
            return;
        }
        if (string.IsNullOrEmpty(newHostPlayerId))
        {
            Debug.LogWarning("[LobbyHelloWorld] New host Player ID cannot be empty.");
            return;
        }
        
        try
        {
            Debug.Log($"[LobbyHelloWorld] Attempting to transfer host to {newHostPlayerId}...");
            Lobby updatedLobby = await lobbyManager.TransferHostAsync(newHostPlayerId);
            Debug.Log($"[LobbyHelloWorld] Successfully transferred host to {newHostPlayerId}. New host is: {updatedLobby.host}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error transferring host: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Kicks a player from the current lobby.
    /// Only the host can kick players.
    /// The host cannot kick themselves.
    /// </summary>
    /// <param name="playerToKickId">The player ID of the player to kick</param>
    public async void DoKickPlayerOnClick(string playerToKickId)
    {
        if (!lobbyManager.IsInLobby())
        {
            Debug.LogWarning("[LobbyHelloWorld] Not in a lobby to kick player.");
            return;
        }
        if (!lobbyManager.IsHost())
        {
            Debug.LogWarning("[LobbyHelloWorld] Only the current host can kick players.");
            return;
        }
        if (string.IsNullOrEmpty(playerToKickId))
        {
            Debug.LogWarning("[LobbyHelloWorld] Player ID to kick cannot be empty.");
            return;
        }
        if (playerToKickId == _uniquePlayerId)
        {
            Debug.LogWarning("[LobbyHelloWorld] Host cannot kick themselves. Use Leave Lobby instead.");
            return;
        }
        
        try
        {
            Debug.Log($"[LobbyHelloWorld] Attempting to kick player {playerToKickId}...");
            Lobby updatedLobby = await lobbyManager.KickPlayerAsync(playerToKickId);
            Debug.Log($"[LobbyHelloWorld] Successfully kicked player {playerToKickId} from lobby.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error kicking player: {ex.Message}");
        }
    }

    /// <summary>
    /// Example of how to explicitly check and join an existing lobby.
    /// </summary>
    public async Task DoCheckAndRejoinExistingLobbyAsync()
    {
        try
        {
            bool joined = await lobbyManager.CheckAndJoinExistingLobbyAsync();
            if (joined)
            {
                Debug.Log("[LobbyHelloWorld] Successfully checked and rejoined an existing lobby.");
            }
            else
            {
                Debug.Log("[LobbyHelloWorld] No existing lobby found for this player or failed to rejoin.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyHelloWorld] Error during CheckAndJoinExistingLobbyAsync: {ex.Message}");
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Called when the list of available lobbies is refreshed.
    /// Use this to update your lobby list UI.
    /// </summary>
    private void HandleLobbiesRefreshed(List<Lobby> lobbies)
    {
        Debug.Log($"[LobbyHelloWorld] Lobbies refreshed. Found {lobbies?.Count ?? 0} lobbies.");
        if (lobbies != null)
        {
            foreach (var lobby in lobbies)
            {
                Debug.Log($"  - Lobby: {lobby.name} ({lobby.id}), Players: {lobby.currentPlayers}/{lobby.maxPlayers}, Status: {lobby.status}, Private: {lobby.isPrivate}");
            }
        }
    }
    
    /// <summary>
    /// Called when a new lobby is added to the available lobbies list.
    /// Use this to add the lobby to your UI.
    /// </summary>
    private void HandleLobbyAddedToList(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] Lobby Added to List: {lobby.name} ({lobby.id})");
    }

    /// <summary>
    /// Called when a lobby is removed from the available lobbies list.
    /// Use this to remove the lobby from your UI.
    /// </summary>
    private void HandleLobbyRemovedFromList(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] Lobby Removed from List: {lobby.name} ({lobby.id})");
    }
    
    /// <summary>
    /// Called when a lobby's properties are modified in the available lobbies list.
    /// Use this to update the lobby's information in your UI.
    /// </summary>
    private void HandleLobbyModifiedInList(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] Lobby Modified in List: {lobby.name} ({lobby.id}), Status: {lobby.status}, Players: {lobby.currentPlayers}");
    }

    /// <summary>
    /// Called when a new lobby is successfully created.
    /// Use this to show the lobby's invite code to the host.
    /// </summary>
    private void HandleLobbyCreated(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] Successfully CREATED lobby: {lobby.name} ({lobby.id}), Invite Code: {lobby.inviteCode}");
    }

    /// <summary>
    /// Called when successfully joining a lobby.
    /// Use this to update your UI to show the lobby's current state.
    /// </summary>
    private void HandleLobbyJoined(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] Successfully JOINED lobby: {lobby.name} ({lobby.id})");
        Debug.Log($"  Current players in joined lobby: {lobby.currentPlayers}/{lobby.maxPlayers}");
        if (lobby.players != null)
        {
            foreach(var pId in lobby.players)
            {
                Debug.Log($"    - Player: {pId} " + (pId == lobby.host ? "(Host)" : ""));
            }
        }
    }

    /// <summary>
    /// Called when leaving a lobby.
    /// Use this to reset your UI to the lobby list view.
    /// </summary>
    private void HandleLobbyLeft()
    {
        Debug.Log("[LobbyHelloWorld] Successfully LEFT lobby.");
    }

    /// <summary>
    /// Called when the current lobby's properties are updated.
    /// Use this to update your UI with the latest lobby information.
    /// </summary>
    private void HandleLobbyUpdated(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] Current lobby UPDATED: {lobby.name} ({lobby.id}), Status: {lobby.status}, Players: {lobby.currentPlayers}/{lobby.maxPlayers}");
    }
    
    /// <summary>
    /// Called when the lobby's settings are updated.
    /// Use this to update your UI with the new lobby settings.
    /// </summary>
    private void HandleLobbySettingsUpdated(Dictionary<string, object> settings)
    {
        Debug.Log($"[LobbyHelloWorld] Lobby settings UPDATED: {Newtonsoft.Json.JsonConvert.SerializeObject(settings)}");
    }

    /// <summary>
    /// Called when a new player joins the current lobby.
    /// Use this to update your player list UI.
    /// </summary>
    private void HandlePlayerJoinedLobby(string playerId)
    {
        Debug.Log($"[LobbyHelloWorld] Player JOINED current lobby: {playerId}");
    }

    /// <summary>
    /// Called when a player leaves the current lobby.
    /// Use this to update your player list UI.
    /// </summary>
    private void HandlePlayerLeftLobby(string playerId)
    {
        Debug.Log($"[LobbyHelloWorld] Player LEFT current lobby: {playerId}");
    }
    
    /// <summary>
    /// Called when host privileges are transferred to another player.
    /// Use this to update UI elements that are host-only.
    /// </summary>
    private void HandleHostTransferred(string newHostId)
    {
        Debug.Log($"[LobbyHelloWorld] Host Transferred. New host is: {newHostId}. Am I the new host? {(newHostId == _uniquePlayerId)}");
    }

    /// <summary>
    /// Called when a match starts.
    /// Use this to transition to your game scene or show the game UI.
    /// </summary>
    private async void HandleMatchStarted(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] Match STARTED in lobby: {lobby.name} ({lobby.id}). Server Info: {(lobby.gameServer != null ? Newtonsoft.Json.JsonConvert.SerializeObject(lobby.gameServer) : "N/A")}");
        
        if (lobbyManager == null) return;

        Debug.Log($"[LobbyHelloWorld] Waiting for game server of lobby {lobby.id} to report 'running' status...");
        // Default timeout is 30s, defined in PlayFlowLobbyManager.WaitForGameServerRunningAsync
        bool serverIsRunning = await lobbyManager.WaitForGameServerRunningAsync(50f); 

        if (serverIsRunning)
        {
            Debug.Log($"[LobbyHelloWorld] Game server for lobby {lobby.id} is now RUNNING. Printing details:");
            lobbyManager.PrintGameServerDetails();

            // Example: Get and print details for internal port 7770 (UDP)
            Debug.Log($"[LobbyHelloWorld] Attempting to get details for internal port 7770 (UDP)...");
            NetworkPortDetails? gamePortUdp = lobbyManager.GetGameServerPortInfo(7770, "udp", "game_udp");
            if (gamePortUdp.HasValue)
            {
                Debug.Log($"[LobbyHelloWorld] UDP Game Port (7770) Details: Name='{gamePortUdp.Value.Name}', Host='{gamePortUdp.Value.Host}', External Port={gamePortUdp.Value.ExternalPort}, Protocol='{gamePortUdp.Value.Protocol}'");
                // Here you would typically use gamePortUdp.Value.Host and gamePortUdp.Value.ExternalPort to connect
            }
            else
            {
                Debug.LogWarning("[LobbyHelloWorld] UDP Game Port (7770) with name 'game_udp' not found.");
            }
            
            // Example: Get and print details for internal port 7771 (TCP)
            Debug.Log($"[LobbyHelloWorld] Attempting to get details for internal port 7771 (TCP)...");
            NetworkPortDetails? webGlPort = lobbyManager.GetGameServerPortInfo(7771, "tcp", "webglPORT");
            if(webGlPort.HasValue)
            {
                Debug.Log($"[LobbyHelloWorld] WebGL Port (7771) Details: Name='{webGlPort.Value.Name}', Host='{webGlPort.Value.Host}', External Port={webGlPort.Value.ExternalPort}, Protocol='{webGlPort.Value.Protocol}'");
            }
            else
            {
                Debug.LogWarning("[LobbyHelloWorld] WebGL Port (7771) with name 'webglPORT' not found.");
            }
        }
        else
        {
            Debug.LogWarning($"[LobbyHelloWorld] Timed out or failed while waiting for game server of lobby {lobby.id} to be 'running'. Details might not be available or server is not up.");
        }
    }
    
    /// <summary>
    /// Called when a match ends.
    /// Use this to return to the lobby UI or show match results.
    /// </summary>
    private void HandleMatchEnded(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] Match ENDED in lobby: {lobby.name} ({lobby.id})");
    }

    /// <summary>
    /// Called when an error occurs.
    /// Use this to show error messages to the user.
    /// </summary>
    private void HandleError(string errorMessage)
    {
        Debug.LogError($"[LobbyHelloWorld] Error received: {errorMessage}");
    }
    
    /// <summary>
    /// Called before making an API call.
    /// Use this to show loading indicators.
    /// </summary>
    private void HandlePreAPICall()
    {
        // Debug.Log("[LobbyHelloWorld] SystemEvent: PreAPICall");
    }

    /// <summary>
    /// Called after an API call completes.
    /// Use this to hide loading indicators.
    /// </summary>
    private void HandlePostAPICall()
    {
        // Debug.Log("[LobbyHelloWorld] SystemEvent: PostAPICall");
    }

    /// <summary>
    /// Called when a specific player's real-time state is updated in the current lobby.
    /// </summary>
    /// <param name="playerId">The ID of the player whose state was updated.</param>
    /// <param name="newState">The new state data for the player.</param>
    private void HandlePlayerStateRealTimeUpdated(string playerId, Dictionary<string, object> newState)
    {
        Debug.Log($"[LobbyHelloWorld] Player ({playerId}) real-time state updated: {Newtonsoft.Json.JsonConvert.SerializeObject(newState)}");
        
        // Example: If you are tracking player scores or health from this state:
        // if (newState.TryGetValue("score", out object scoreValue) && scoreValue is long score) // Assuming score is sent as a number (long by JObject default)
        // {
        //    Debug.Log($"[LobbyHelloWorld] Player {playerId} new score: {score}");
        //    // Update UI or game logic for this player's score
        // }
        // if (newState.TryGetValue("health", out object healthValue) && healthValue is long health)
        // {
        //    Debug.Log($"[LobbyHelloWorld] Player {playerId} new health: {health}");
        //    // Update UI or game logic for this player's health
        // }

        // Add your game-specific logic here to react to the player's state update.
        // For example, if this client is responsible for player PlayerId, update its local representation.
        // Or, if this is a general update, update the representation of that player for all clients.
    }

    #endregion

    // Add Update method for keyboard shortcuts
    void Update()
    {
        // Create Lobby with 'C'
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (!lobbyManager.IsInLobby())
            {
                Debug.Log("[LobbyHelloWorld] 'C' pressed. Attempting to create lobby...");
                DoCreateLobbyOnClick();
            }
            else
            {
                Debug.LogWarning("[LobbyHelloWorld] 'C' pressed, but already in a lobby. Cannot create another.");
            }
        }

        // Delete (Leave as Host) Lobby with 'D'
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (lobbyManager.IsInLobby())
            {
                if (lobbyManager.IsHost())
                {
                    Debug.Log("[LobbyHelloWorld] 'D' pressed by Host. Attempting to end match (if active) and leave lobby...");
                    // If the game is in progress, end it first.
                    if (lobbyManager.GetCurrentLobbyStatus() == "in_game")
                    {
                        Debug.Log("[LobbyHelloWorld] Match is 'in_game', ending it before leaving.");
                        _ = lobbyManager.EndGameAsync(); 
                        // Note: EndGameAsync is async. For simplicity here, we proceed with fire-and-forget.
                        // In a real game, you might wait for EndGameAsync to complete or for the lobby status to change.
                    }
                    // Then leave the lobby
                    DoLeaveCurrentLobbyOnClick();
                }
                else
                {
                    Debug.LogWarning("[LobbyHelloWorld] 'D' pressed, but you are not the host. Only the host can 'delete' (by leaving) the lobby this way. You can leave normally.");
                }
            }
            else
            {
                Debug.LogWarning("[LobbyHelloWorld] 'D' pressed, but not in a lobby to delete/leave.");
            }
        }

        // Start Match with 'M'
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log("[LobbyHelloWorld] 'M' pressed. Attempting to start match...");
            DoStartMatchOnClick(); // This method already contains checks for IsInLobby and IsHost
        }

        // End Match with 'N'
        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug.Log("[LobbyHelloWorld] 'N' pressed. Attempting to end match...");
            DoEndMatchOnClick(); // This method already contains checks for IsInLobby, IsHost, and game status
        }
    }
} 