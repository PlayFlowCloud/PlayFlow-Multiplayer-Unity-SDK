using UnityEngine;
using System.Collections.Generic;
using System;
using PlayFlow;

/// <summary>
/// A simple example demonstrating how to use the new PlayFlow Lobby SDK V2.
/// This script shows the core functionality with keyboard shortcuts for testing.
/// 
/// Setup Instructions:
/// 1. Create an empty GameObject and add the PlayFlowLobbyManagerV2 component to it.
/// 2. Configure your API key in the PlayFlowLobbyManagerV2 component in the Inspector.
/// 3. Create another empty GameObject and add this LobbyHelloWorld script to it.
/// 4. Drag the PlayFlowLobbyManagerV2 GameObject into the 'Lobby Manager' field on this script.
/// 5. Play the scene and use the keyboard shortcuts below.
/// 
/// Keyboard Shortcuts:
/// - R: Refresh lobby list
/// - C: Create a new lobby
/// - J: Join first available public lobby
/// - L: Leave current lobby
/// - S: Start the match (host only)
/// - E: End the match (host only)
/// - T: Send test player state update
/// </summary>
public class LobbyHelloWorld : MonoBehaviour
{
    [Header("Manager")]
    [Tooltip("Assign your PlayFlowLobbyManagerV2 component here.")]
    public PlayFlowLobbyManagerV2 lobbyManager;

    [Header("Settings")]
    [Tooltip("The name for lobbies created by this script")]
    public string lobbyName = "Test Lobby";
    
    [Tooltip("Maximum players for created lobbies")]
    [Range(2, 10)]
    public int maxPlayers = 4;
    
    [Tooltip("Whether created lobbies should be private")]
    public bool isPrivate = false;
    
    private string _playerId;
    
    void Start()
    {
        // Find the manager if it's not assigned
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<PlayFlowLobbyManagerV2>();
            if (lobbyManager == null)
            {
                Debug.LogError("[LobbyHelloWorld] PlayFlowLobbyManagerV2 not found in the scene! Please add it and configure your API key.", this);
                return;
            }
        }

        // Generate a unique player ID
        _playerId = "player-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        Debug.Log($"[LobbyHelloWorld] Starting with player ID: {_playerId}");
        
        // Initialize the manager
        lobbyManager.Initialize(_playerId, OnManagerReady);
        
        // Subscribe to events
        SubscribeToEvents();
    }
    
    void OnManagerReady()
    {
        Debug.Log("[LobbyHelloWorld] Manager is ready! Use keyboard shortcuts:");
        Debug.Log("  R - Refresh lobby list");
        Debug.Log("  C - Create a new lobby");
        Debug.Log("  J - Join first available lobby");
        Debug.Log("  L - Leave current lobby");
        Debug.Log("  S - Start the match");
        Debug.Log("  E - End the match");
        Debug.Log("  T - Send test player state");
    }
    
    void Update()
    {
        if (!lobbyManager.IsReady) return;
        
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
    }
    
    void RefreshLobbies()
    {
        Debug.Log("[LobbyHelloWorld] Refreshing lobby list...");
        
        lobbyManager.GetAvailableLobbies(
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
        Debug.Log($"[LobbyHelloWorld] Creating lobby '{lobbyName}'...");
        
        lobbyManager.CreateLobby(lobbyName, maxPlayers, isPrivate,
            onSuccess: (lobby) => {
                Debug.Log($"[LobbyHelloWorld] Successfully created lobby: {lobby.name} (ID: {lobby.id})");
                if (lobby.isPrivate && !string.IsNullOrEmpty(lobby.inviteCode))
                {
                    Debug.Log($"[LobbyHelloWorld] Invite code: {lobby.inviteCode}");
                }
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to create lobby: {error}");
            }
        );
    }
    
    void JoinFirstAvailableLobby()
    {
        Debug.Log("[LobbyHelloWorld] Looking for available lobbies...");
        
        lobbyManager.GetAvailableLobbies(
            onSuccess: (lobbies) => {
                var availableLobby = lobbies.Find(l => !l.isPrivate && l.currentPlayers < l.maxPlayers);
                
                if (availableLobby != null)
                {
                    Debug.Log($"[LobbyHelloWorld] Joining lobby: {availableLobby.name}");
                    
                    lobbyManager.JoinLobby(availableLobby.id,
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
        if (lobbyManager.CurrentLobby == null)
        {
            Debug.LogWarning("[LobbyHelloWorld] Not in a lobby!");
            return;
        }
        
        Debug.Log("[LobbyHelloWorld] Leaving current lobby...");
        
        lobbyManager.LeaveLobby(
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
        if (lobbyManager.CurrentLobby == null || !lobbyManager.IsHost)
        {
            Debug.LogWarning("[LobbyHelloWorld] Cannot start match: you must be in a lobby and be the host.");
            return;
        }
        
        Debug.Log("[LobbyHelloWorld] Starting match...");
        
        lobbyManager.StartMatch(
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
        if (lobbyManager.CurrentLobby == null || !lobbyManager.IsHost)
        {
            Debug.LogWarning("[LobbyHelloWorld] Cannot end match: you must be in a lobby and be the host.");
            return;
        }
        
        Debug.Log("[LobbyHelloWorld] Ending match...");
        
        lobbyManager.EndMatch(
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
        if (lobbyManager.CurrentLobby == null)
        {
            Debug.LogWarning("[LobbyHelloWorld] Not in a lobby!");
            return;
        }
        
        var testState = new Dictionary<string, object>
        {
            ["position"] = new Dictionary<string, float> { ["x"] = 10f, ["y"] = 20f },
            ["health"] = 100,
            ["ready"] = true,
            ["timestamp"] = DateTime.UtcNow.ToString()
        };
        
        Debug.Log("[LobbyHelloWorld] Sending player state update...");
        
        lobbyManager.UpdatePlayerState(testState,
            onSuccess: (lobby) => {
                Debug.Log("[LobbyHelloWorld] Successfully updated player state");
            },
            onError: (error) => {
                Debug.LogError($"[LobbyHelloWorld] Failed to update player state: {error}");
            }
        );
    }
    
    void SubscribeToEvents()
    {
        var events = lobbyManager.Events;
        
        // Lobby events
        events.OnLobbyCreated.AddListener(OnLobbyCreated);
        events.OnLobbyJoined.AddListener(OnLobbyJoined);
        events.OnLobbyUpdated.AddListener(OnLobbyUpdated);
        events.OnLobbyLeft.AddListener(OnLobbyLeft);
        
        // Match events
        events.OnMatchStarted.AddListener(OnMatchStarted);
        events.OnMatchEnded.AddListener(OnMatchEnded);
        
        // Player events
        events.OnPlayerJoined.AddListener(OnPlayerJoined);
        events.OnPlayerLeft.AddListener(OnPlayerLeft);
        
        // System events
        events.OnError.AddListener(OnError);
        
        // Session state changes
        var session = lobbyManager.GetComponent<PlayFlowSession>();
        session.OnStateChanged.AddListener(OnStateChanged);
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
    }
    
    void OnLobbyLeft()
    {
        Debug.Log("[LobbyHelloWorld] EVENT: Left lobby");
    }
    
    void OnPlayerJoined(string playerId, PlayerAction action)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Player joined - {playerId}");
    }
    
    void OnPlayerLeft(string playerId, PlayerAction action)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Player left - {playerId}");
    }
    
    void OnError(string error)
    {
        Debug.LogError($"[LobbyHelloWorld] EVENT: Error - {error}");
    }
    
    void OnMatchStarted(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Match started in lobby {lobby.name}!");
    }
    
    void OnMatchEnded(Lobby lobby)
    {
        Debug.Log($"[LobbyHelloWorld] EVENT: Match ended in lobby {lobby.name}. Returning to 'waiting' status.");
    }
    
    void OnDestroy()
    {
        // Clean up
        if (lobbyManager != null)
        {
            lobbyManager.Disconnect();
            
            // Unsubscribe from events
            var events = lobbyManager.Events;
            events.OnLobbyCreated.RemoveAllListeners();
            events.OnLobbyJoined.RemoveAllListeners();
            events.OnLobbyUpdated.RemoveAllListeners();
            events.OnLobbyLeft.RemoveAllListeners();
            events.OnPlayerJoined.RemoveAllListeners();
            events.OnPlayerLeft.RemoveAllListeners();
            events.OnError.RemoveAllListeners();
        }
    }
} 