using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using PlayFlow;
using System.Collections;
using Newtonsoft.Json;

/// <summary>
/// A simple example demonstrating how to use the new PlayFlow Lobby SDK V2.
/// This script shows the core functionality with keyboard shortcuts for testing.
///
/// Setup Instructions:
/// 1. Create an empty GameObject and add the PlayFlowLobbyManagerV2 component to it.
/// 2. Configure your API key in the PlayFlowLobbyManagerV2 component in the Inspector.
/// 3. Set the defaultLobbyConfig to "firstLobby" in the inspector
/// 4. Create another empty GameObject and add this LobbyHelloWorld script to it.
/// 5. Play the scene and use the keyboard shortcuts below.
/// 
/// Keyboard Shortcuts:
/// - R: Refresh lobby list
/// - C: Create a new lobby (for 1v1 matchmaking)
/// - J: Join first available public lobby
/// - L: Leave current lobby
/// - S: Start the match (host only - direct match)
/// - E: End the match (host only)
/// - T: Send test player state update (with MMR)
/// - U: Update other player's state (host only)
/// - I: Get game server connection info
/// - M: Start 1v1 Matchmaking (host only)
/// - N: Cancel Matchmaking (host only)
/// </summary>
public class LobbyHelloWorld : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The name for lobbies created by this script")]
    public string lobbyName = "1v1 Match Lobby";
    
    [Tooltip("Maximum players for created lobbies (set to 2 for 1v1)")]
    [Range(2, 10)]
    public int maxPlayers = 2;
    
    [Tooltip("Whether created lobbies should be private")]
    public bool isPrivate = false;
    
    [Header("Matchmaking Settings")]
    [Tooltip("Your player's MMR (matchmaking rating) for testing")]
    [Range(800, 2000)]
    public int playerMMR = 1200;

    private string _playerId;

    void Start()
    {
        // Manager can now be accessed directly via the singleton instance
        if (PlayFlowLobbyManagerV2.Instance == null)
        {
            Debug.LogError("[LobbyHelloWorld] PlayFlowLobbyManagerV2 not found in the scene! Please add it to a GameObject.", this);
            return;
        }

        // Generate a unique player ID
        _playerId = "player-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        Debug.Log($"[LobbyHelloWorld] Starting with player ID: {_playerId}");
        
        // Initialize the manager
        PlayFlowLobbyManagerV2.Instance.Initialize(_playerId, OnManagerReady);
        
        // Subscribe to events
        SubscribeToEvents();
    }
    
    void OnManagerReady()
    {
        Debug.Log("[LobbyHelloWorld] Manager is ready! Use keyboard shortcuts:");
        Debug.Log("  R - Refresh lobby list");
        Debug.Log("  C - Create a new lobby (for 1v1 matchmaking)");
        Debug.Log("  J - Join first available lobby");
        Debug.Log("  L - Leave current lobby");
        Debug.Log("  S - Start the match (direct match)");
        Debug.Log("  E - End the match");
        Debug.Log("  T - Send test player state (with MMR)");
        Debug.Log("  U - Update other player's state");
        Debug.Log("  I - Get game server connection info");
        Debug.Log("  M - Start 1v1 Matchmaking");
        Debug.Log("  N - Cancel Matchmaking");
        
        // Automatically set initial player state with MMR when ready
        StartCoroutine(SetInitialPlayerState());
    }
    
    void Update()
    {
        if (!PlayFlowLobbyManagerV2.Instance.IsReady) return;
        
        // Refresh lobby list
        if (Input.GetKeyDown(KeyCode.R))
        {
            RefreshLobbies();
        }
        
        // Create lobby
        if (Input.GetKeyDown(KeyCode.C))
        {
            CreateLobby();
        }
        
        // Join first available lobby
        if (Input.GetKeyDown(KeyCode.J))
        {
            JoinFirstAvailableLobby();
        }
        
        // Leave lobby
        if (Input.GetKeyDown(KeyCode.L))
        {
            LeaveLobby();
        }
        
        // Send player state update
        if (Input.GetKeyDown(KeyCode.T))
        {
            SendTestPlayerState();
        }
        
        // Start match
        if (Input.GetKeyDown(KeyCode.S))
        {
            StartMatch();
        }
        
        // End match
        if (Input.GetKeyDown(KeyCode.E))
        {
            EndMatch();
        }
        
        // [Host-Only] Update another player's state
        if (Input.GetKeyDown(KeyCode.U))
        {
            UpdateOtherPlayerState();
        }

        // Get game server connection info
        if (Input.GetKeyDown(KeyCode.I))
        {
            GetConnectionInfo();
        }
        
        // Start 1v1 Matchmaking
        if (Input.GetKeyDown(KeyCode.M))
        {
            StartMatchmaking();
        }
        
        // Cancel Matchmaking
        if (Input.GetKeyDown(KeyCode.N))
        {
            CancelMatchmaking();
        }
    }
    
    void RefreshLobbies()
    {
        Debug.Log("[LobbyHelloWorld] Refreshing lobby list...");
        
        PlayFlowLobbyManagerV2.Instance.GetAvailableLobbies(
            onSuccess: (lobbies) => {
                Debug.Log($"[LobbyHelloWorld] Found {lobbies.Count} lobbies:");
                foreach (var lobby in lobbies)
                {
                    Debug.Log($"  - {lobby.name} ({lobby.currentPlayers}/{lobby.maxPlayers}) ID: {lobby.id}");
                }
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to get lobbies: {error}");
            }
        );
    }
    
    void CreateLobby()
    {
        Debug.Log($"[LobbyHelloWorld] Creating 1v1 lobby '{lobbyName}'...");
        
        // Create custom settings for the lobby
        var customSettings = new Dictionary<string, object>
        {
            ["gameMode"] = "1v1",
            ["matchType"] = "ranked"
        };
        
        // Create lobby optimized for 1v1 matchmaking
        PlayFlowLobbyManagerV2.Instance.CreateLobby(
            name: lobbyName,
            maxPlayers: 2, // Always 2 for 1v1
            isPrivate: isPrivate,
            allowLateJoin: false, // No late join for competitive 1v1
            region: "us-west", // You might want to make this configurable
            customSettings: customSettings,
            onSuccess: (lobby) => {
                Debug.Log($"[LobbyHelloWorld] Successfully created 1v1 lobby: {lobby.name} (ID: {lobby.id})");
                if (lobby.isPrivate && !string.IsNullOrEmpty(lobby.inviteCode))
                {
                    Debug.Log($"[LobbyHelloWorld] Invite code: {lobby.inviteCode}");
                }
                Debug.Log("[LobbyHelloWorld] Tip: Press 'M' to start 1v1 matchmaking once you're ready!");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to create lobby: {error}");
            }
        );
    }
    
    void JoinFirstAvailableLobby()
    {
        Debug.Log("[LobbyHelloWorld] Looking for available lobbies...");
        
        PlayFlowLobbyManagerV2.Instance.GetAvailableLobbies(
            onSuccess: (lobbies) => {
                var availableLobby = lobbies.Find(l => !l.isPrivate && l.currentPlayers < l.maxPlayers);
                
                if (availableLobby != null)
                {
                    Debug.Log($"[LobbyHelloWorld] Joining lobby: {availableLobby.name}");
                    
                    PlayFlowLobbyManagerV2.Instance.JoinLobby(availableLobby.id,
                        onSuccess: (lobby) => {
                            Debug.Log($"[LobbyHelloWorld] Successfully joined lobby: {lobby.name}");
                        },
                        onError: (error) => {
                            Debug.LogError($"[LobbyHelloWorld] Failed to join lobby: {error}");
                        }
                    );
                }
                else
                {
                    Debug.LogWarning("[LobbyHelloWorld] No available public lobbies found. Try creating one!");
                }
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to get lobbies: {error}");
            }
        );
    }
    
    void LeaveLobby()
    {
        if (PlayFlowLobbyManagerV2.Instance.CurrentLobby == null)
        {
            Debug.LogWarning("[LobbyHelloWorld] Not in a lobby!");
            return;
        }
        
        Debug.Log("[LobbyHelloWorld] Leaving current lobby...");
        
        PlayFlowLobbyManagerV2.Instance.LeaveLobby(
            onSuccess: () => {
                Debug.Log("[LobbyHelloWorld] Successfully left lobby");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to leave lobby: {error}");
            }
        );
    }
    
    void StartMatch()
    {
        if (PlayFlowLobbyManagerV2.Instance.CurrentLobby == null || !PlayFlowLobbyManagerV2.Instance.IsHost)
        {
            Debug.LogWarning("[LobbyHelloWorld] Cannot start match: you must be in a lobby and be the host.");
            return;
        }
        
        Debug.Log("[LobbyHelloWorld] Starting match...");
        
        PlayFlowLobbyManagerV2.Instance.StartMatch(
            onSuccess: (lobby) => {
                Debug.Log($"[LobbyHelloWorld] Match started successfully. Status: {lobby.status}");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to start match: {error}");
            }
        );
    }
    
    void EndMatch()
    {
        if (PlayFlowLobbyManagerV2.Instance.CurrentLobby == null || !PlayFlowLobbyManagerV2.Instance.IsHost)
        {
            Debug.LogWarning("[LobbyHelloWorld] Cannot end match: you must be in a lobby and be the host.");
            return;
        }
        
        Debug.Log("[LobbyHelloWorld] Ending match...");
        
        PlayFlowLobbyManagerV2.Instance.EndMatch(
            onSuccess: (lobby) => {
                Debug.Log($"[LobbyHelloWorld] Match ended successfully. Status: {lobby.status}");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to end match: {error}");
            }
        );
    }
    
    void SendTestPlayerState()
    {
        if (PlayFlowLobbyManagerV2.Instance.CurrentLobby == null)
        {
            Debug.LogWarning("[LobbyHelloWorld] Not in a lobby!");
            return;
        }
        
        var testState = new Dictionary<string, object>
        {
            ["position"] = new Dictionary<string, float> { ["x"] = 10f, ["y"] = 20f },
            ["health"] = 100,
            ["ready"] = true,
            ["mmr"] = playerMMR, // Include MMR for matchmaking
            ["regions"] = new List<string> { "eu-west", "eu-north" },
            ["timestamp"] = DateTime.UtcNow.ToString()
        };
        
        Debug.Log($"[LobbyHelloWorld] Sending player state update with MMR: {playerMMR}...");
        
        PlayFlowLobbyManagerV2.Instance.UpdatePlayerState(testState,
            onSuccess: (lobby) => {
                Debug.Log("[LobbyHelloWorld] Successfully updated player state");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to update player state: {error}");
            }
        );
    }
    
    void GetConnectionInfo()
    {
        var manager = PlayFlowLobbyManagerV2.Instance;
        if (manager.CurrentLobby?.status != "in_game")
        {
            Debug.LogWarning("[LobbyHelloWorld] Cannot get connection info: not in a running match.");
            return;
        }

        ConnectionInfo? connectionInfo = manager.GetGameServerConnectionInfo();

        if (connectionInfo.HasValue)
        {
            Debug.Log($"[LobbyHelloWorld] Game Server Connection Info: IP = {connectionInfo.Value.Ip}, Port = {connectionInfo.Value.Port}");
            // Here you would use this information to connect your game client (e.g., using Netcode, Mirror, etc.)
        }
        else
        {
            Debug.LogError("[LobbyHelloWorld] Failed to get connection info, although the match is running. The gameServer data might be missing.");
        }
    }
    
    void UpdateOtherPlayerState()
    {
        var manager = PlayFlowLobbyManagerV2.Instance;
        if (!manager.IsHost)
        {
            Debug.LogWarning("[LobbyHelloWorld] Only the host can update another player's state.");
            return;
        }

        if (manager.CurrentLobby == null || manager.CurrentLobby.players.Length < 2)
        {
            Debug.LogWarning("[LobbyHelloWorld] Need at least one other player in the lobby to test this feature.");
            return;
        }

        // Find the first player who is not the host
        string otherPlayerId = null;
        foreach (var player in manager.CurrentLobby.players)
        {
            if (player != manager.PlayerId)
            {
                otherPlayerId = player;
                break;
            }
        }

        if (string.IsNullOrEmpty(otherPlayerId))
        {
             Debug.LogWarning("[LobbyHelloWorld] Couldn't find another player in the lobby.");
             return;
        }

        var testState = new Dictionary<string, object>
        {
            ["messageFromHost"] = "The host updated your state!",
            ["timestamp"] = DateTime.UtcNow.ToString()
        };

        Debug.Log($"[LobbyHelloWorld] Host is updating state for player {otherPlayerId}...");
        
        manager.UpdateStateForPlayer(otherPlayerId, testState,
            onSuccess: (lobby) => {
                Debug.Log($"[LobbyHelloWorld] Successfully updated state for player {otherPlayerId}.");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to update player state: {error}");
            }
        );
    }
    
    void StartMatchmaking()
    {
        if (!PlayFlowLobbyManagerV2.Instance.IsInLobby || !PlayFlowLobbyManagerV2.Instance.IsHost)
        {
            Debug.LogWarning("[LobbyHelloWorld] Must be host in a lobby to start matchmaking!");
            return;
        }

        Debug.Log("[LobbyHelloWorld] Starting 1v1 matchmaking...");
        
        PlayFlowLobbyManagerV2.Instance.FindMatch("1VS1",
            onSuccess: (lobby) => {
                Debug.Log($"[LobbyHelloWorld] Successfully started matchmaking! Status: {lobby.status}");
                Debug.Log($"[LobbyHelloWorld] Matchmaking mode: {lobby.matchmakingMode}");
                Debug.Log($"[LobbyHelloWorld] Ticket ID: {lobby.matchmakingTicketId}");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to start matchmaking: {error}");
            }
        );
    }
    
    void CancelMatchmaking()
    {
        if (!PlayFlowLobbyManagerV2.Instance.IsInLobby || !PlayFlowLobbyManagerV2.Instance.IsHost)
        {
            Debug.LogWarning("[LobbyHelloWorld] Must be host in a lobby to cancel matchmaking!");
            return;
        }

        Debug.Log("[LobbyHelloWorld] Cancelling matchmaking...");
        
        PlayFlowLobbyManagerV2.Instance.CancelMatchmaking(
            onSuccess: (lobby) => {
                Debug.Log($"[LobbyHelloWorld] Successfully cancelled matchmaking! Status: {lobby.status}");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to cancel matchmaking: {error}");
            }
        );
    }
    
    IEnumerator SetInitialPlayerState()
    {
        // Wait a frame to ensure everything is initialized
        yield return null;
        
        // Wait until we're in a lobby
        yield return new WaitUntil(() => PlayFlowLobbyManagerV2.Instance.IsInLobby);
        
        // Set initial player state with MMR
        var initialState = new Dictionary<string, object>
        {
            ["mmr"] = playerMMR,
            ["ready"] = false,
            ["playerName"] = $"Player_{_playerId.Substring(0, 8)}"
        };
        
        Debug.Log($"[LobbyHelloWorld] Setting initial player state with MMR: {playerMMR}");
        
        PlayFlowLobbyManagerV2.Instance.UpdatePlayerState(initialState,
            onSuccess: (lobby) => {
                Debug.Log("[LobbyHelloWorld] Initial player state set successfully");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to set initial player state: {error}");
            }
        );
    }
    
    void SubscribeToEvents()
    {
        var events = PlayFlowLobbyManagerV2.Instance.Events;
        
        // Lobby events
        events.OnLobbyCreated.AddListener(OnLobbyCreated);
        events.OnLobbyJoined.AddListener(OnLobbyJoined);
        events.OnLobbyUpdated.AddListener(OnLobbyUpdated);
        events.OnLobbyLeft.AddListener(OnLobbyLeft);
        
        // Match events
        events.OnMatchStarted.AddListener(OnMatchStarted);
        events.OnMatchEnded.AddListener(OnMatchEnded);
        events.OnMatchRunning.AddListener(OnMatchRunning);
        
        // Matchmaking events
        events.OnMatchmakingStarted.AddListener(OnMatchmakingStarted);
        events.OnMatchmakingCancelled.AddListener(OnMatchmakingCancelled);
        events.OnMatchFound.AddListener(OnMatchFound);
        
        // Player events
        events.OnPlayerJoined.AddListener(OnPlayerJoined);
        events.OnPlayerLeft.AddListener(OnPlayerLeft);
        
        // System events
        events.OnError.AddListener(OnError);
        
        // Session state changes
        var manager = PlayFlowLobbyManagerV2.Instance;
        manager.Events.OnStateChanged.AddListener(OnStateChanged);
    }
    
    // Event handlers
    void OnStateChanged(LobbyState oldState, LobbyState newState)
    {
        Debug.Log($"[LobbyHelloWorld] State changed: {oldState} -> {newState}");
    }
    
    void OnLobbyCreated(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Lobby created - {lobby.name}");
    }
    
    void OnLobbyJoined(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Joined lobby - {lobby.name}");
        Debug.Log($"  Players: {string.Join(", ", lobby.players)}");
        Debug.Log($"  Host: {lobby.host}");
    }
    
    void OnLobbyUpdated(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Lobby updated - {lobby.name} ({lobby.currentPlayers}/{lobby.maxPlayers})");
        
        // Check if SSE is connected
        var sseManager = LobbySseManager.Instance;
        if (sseManager != null && sseManager.IsConnected)
        {
            Debug.Log("[LobbyHelloWorld] ✅ Update received via SSE (real-time)");
        }
        else
        {
            Debug.Log("[LobbyHelloWorld] ⏱️ Update received via polling");
        }
    }
    
    void OnLobbyLeft()
    {
        Debug.Log("[LobbyHelloWorld] EVENT: Left lobby");
    }
    
    // =============================================================================
    // PLAYER STATE UPDATE EXAMPLES
    // =============================================================================
    
    /// <summary>
    /// Example: Update your own player state
    /// Any player can update their own state at any time
    /// </summary>
    public void UpdateMyOwnState()
    {
        // Check if we're in a lobby
        if (!PlayFlowLobbyManagerV2.Instance.IsInLobby)
        {
            Debug.LogWarning("Not in a lobby!");
            return;
        }
        
        // Create your player state data
        var myState = new Dictionary<string, object>
        {
            ["position"] = new Dictionary<string, float> { ["x"] = 100f, ["y"] = 50f, ["z"] = 0f },
            ["health"] = 85,
            ["armor"] = 50,
            ["weapon"] = "plasma_rifle",
            ["team"] = "blue",
            ["ready"] = true,
            ["lastUpdated"] = DateTime.UtcNow.ToString()
        };
        
        // Update your own state
        PlayFlowLobbyManagerV2.Instance.UpdatePlayerState(myState, 
            onSuccess: (lobby) => {
                Debug.Log($"Successfully updated my state. My player ID: {PlayFlowLobbyManagerV2.Instance.PlayerId}");
                
                // You can access your updated state from the lobby
                if (lobby.lobbyStateRealTime.TryGetValue(PlayFlowLobbyManagerV2.Instance.PlayerId, out var updatedState))
                {
                    Debug.Log($"My updated state: {JsonConvert.SerializeObject(updatedState)}");
                }
            },
            onError: (error) => {
                Debug.LogError($"Failed to update my state: {error}");
            }
        );
    }
    
    /// <summary>
    /// Example: Host updates another player's state
    /// Only the host can update other players' states
    /// </summary>
    public void HostUpdateAnotherPlayerState(string targetPlayerId)
    {
        // Check if we're in a lobby
        if (!PlayFlowLobbyManagerV2.Instance.IsInLobby)
        {
            Debug.LogWarning("Not in a lobby!");
            return;
        }
        
        // Check if we're the host
        if (!PlayFlowLobbyManagerV2.Instance.IsHost)
        {
            Debug.LogError("Only the host can update other players' states!");
            return;
        }
        
        // Verify target player is in the lobby
        var currentLobby = PlayFlowLobbyManagerV2.Instance.CurrentLobby;
        if (!currentLobby.players.Contains(targetPlayerId))
        {
            Debug.LogError($"Player {targetPlayerId} is not in the lobby!");
            return;
        }
        
        // Create state data for the target player
        var targetPlayerState = new Dictionary<string, object>
        {
            ["team"] = "red",  // Host assigns player to red team
            ["role"] = "sniper",  // Host assigns player role
            ["spawnPoint"] = new Dictionary<string, float> { ["x"] = 200f, ["y"] = 100f, ["z"] = 50f },
            ["allowedWeapons"] = new List<string> { "sniper_rifle", "pistol" },
            ["updatedByHost"] = true,
            ["hostUpdatedAt"] = DateTime.UtcNow.ToString()
        };
        
        // Update the target player's state
        PlayFlowLobbyManagerV2.Instance.UpdateStateForPlayer(targetPlayerId, targetPlayerState,
            onSuccess: (lobby) => {
                Debug.Log($"Host successfully updated state for player: {targetPlayerId}");
                
                // Verify the update
                if (lobby.lobbyStateRealTime.TryGetValue(targetPlayerId, out var updatedState))
                {
                    Debug.Log($"Target player's updated state: {JsonConvert.SerializeObject(updatedState)}");
                }
            },
            onError: (error) => {
                Debug.LogError($"Failed to update player {targetPlayerId}'s state: {error}");
            }
        );
    }
    
    /// <summary>
    /// Example: Host assigns teams to all players
    /// Demonstrates batch updates by the host
    /// </summary>
    public void HostAssignTeamsToAllPlayers()
    {
        if (!PlayFlowLobbyManagerV2.Instance.IsInLobby || !PlayFlowLobbyManagerV2.Instance.IsHost)
        {
            Debug.LogError("Must be host and in lobby to assign teams!");
            return;
        }
        
        var players = PlayFlowLobbyManagerV2.Instance.CurrentLobby.players;
        var teamAssignments = new[] { "blue", "red" };
        
        for (int i = 0; i < players.Count(); i++)
        {
            var playerId = players[i];
            var team = teamAssignments[i % 2]; // Alternate between blue and red
            
            var teamState = new Dictionary<string, object>
            {
                ["team"] = team,
                ["teamAssignedAt"] = DateTime.UtcNow.ToString()
            };
            
            // Skip if it's the host's own ID - use UpdatePlayerState instead
            if (playerId == PlayFlowLobbyManagerV2.Instance.PlayerId)
            {
                PlayFlowLobbyManagerV2.Instance.UpdatePlayerState(teamState);
            }
            else
            {
                PlayFlowLobbyManagerV2.Instance.UpdateStateForPlayer(playerId, teamState);
            }
        }
        
        Debug.Log("Host assigned teams to all players!");
    }
    
    void OnPlayerJoined(PlayerAction action)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Player joined - {action.PlayerId}");
    }
    
    void OnPlayerLeft(PlayerAction action)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Player left - {action.PlayerId}");
    }
    
    void OnError(string error)
    {
        Debug.LogError($"[LobbyHelloWorld] EVENT: Error - {error}");
    }
    
    void OnMatchStarted(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Match start has been triggered for lobby {lobby.name}. Waiting for server to be ready...");
    }

    void OnMatchRunning(ConnectionInfo connectionInfo)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Server is ready! IP: {connectionInfo.Ip}, Port: {connectionInfo.Port}");
        // Here you would connect your game client using the connectionInfo details.
    }
    
    void OnMatchEnded(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Match ended in lobby {lobby.name}. Returning to 'waiting' status.");
    }
    
    void OnMatchmakingStarted(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Matchmaking started! Mode: {lobby.matchmakingMode}, Status: {lobby.status}");
        Debug.Log($"[LobbyHelloWorld] Looking for opponents with similar MMR...");
    }
    
    void OnMatchmakingCancelled(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Matchmaking cancelled. Status: {lobby.status}");
    }
    
    void OnMatchFound(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Match found! Status: {lobby.status}");
        Debug.Log($"[LobbyHelloWorld] Game server is being launched...");
        
        // Log matchmaking data if available
        if (lobby.matchmakingData != null)
        {
            Debug.Log($"[LobbyHelloWorld] Matchmaking data: {JsonConvert.SerializeObject(lobby.matchmakingData)}");
        }
    }
    
    void OnDestroy()
    {
        // Clean up
        if (PlayFlowLobbyManagerV2.Instance != null)
        {
            // If the player is in a lobby, make sure they leave it gracefully.
            if (PlayFlowLobbyManagerV2.Instance.IsInLobby)
            {
                // This is a fire-and-forget call. We don't wait for the response
                // because the application is likely quitting.
                PlayFlowLobbyManagerV2.Instance.LeaveLobby();
            }
            
            PlayFlowLobbyManagerV2.Instance.Disconnect();
            
            // Unsubscribe from events
            var events = PlayFlowLobbyManagerV2.Instance.Events;
            events.OnLobbyCreated.RemoveAllListeners();
            events.OnLobbyJoined.RemoveAllListeners();
            events.OnLobbyUpdated.RemoveAllListeners();
            events.OnLobbyLeft.RemoveAllListeners();
            events.OnPlayerJoined.RemoveAllListeners();
            events.OnPlayerLeft.RemoveAllListeners();
            events.OnMatchStarted.RemoveAllListeners();
            events.OnMatchEnded.RemoveAllListeners();
            events.OnMatchRunning.RemoveAllListeners();
            events.OnMatchmakingStarted.RemoveAllListeners();
            events.OnMatchmakingCancelled.RemoveAllListeners();
            events.OnMatchFound.RemoveAllListeners();
            events.OnError.RemoveAllListeners();
        }
    }
} 