using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Threading;
using PlayFlow; // Assuming PlayFlowLobbyManager and Lobby models are in this namespace

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
///
/// Event Handling:
/// This script automatically subscribes to all lobby events and logs them to the console.
/// You can modify the event handlers to update your UI or game state accordingly.
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
    [Tooltip("Assign your PlayFlowLobbyManager GameObject here.")]
    public PlayFlowLobbyManager lobbyManager;

    [Header("Test Settings")]
    [Tooltip("The name for the lobby you want to create.")]
    public string lobbyName = "My Awesome Lobby";
    
    [Tooltip("Default maximum number of players allowed in lobbies created by this script.")]
    [Range(1, 100)]
    public int maxPlayers = 8;
    
    [Tooltip("Whether lobbies created by this script should be private (require invite code) or public.")]
    public bool isPrivate = false;

    [Tooltip("Whether players can join after the game has started.")]
    public bool allowLateJoin = true;
    
    [Tooltip("The server region for the lobby.")]
    public string region = "us-west";

    private string _uniquePlayerId;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    void Start()
    {
        if (lobbyManager == null)
        {
            Debug.LogError("PlayFlowLobbyManager not assigned!");
            return;
        }

        _uniquePlayerId = "player-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        lobbyManager.SetPlayerInfo(_uniquePlayerId);

        SubscribeToLobbyEvents();
    }
    
    void OnDestroy()
    {
        UnsubscribeFromLobbyEvents();
        _cts.Cancel();
    }

    void Update()
    {
        // Refresh Lobbies List
        if (Input.GetKeyDown(KeyCode.R))
        {
            DoRefreshLobbiesOnClick();
        }

        // Create a new lobby
        if (Input.GetKeyDown(KeyCode.C))
        {
            DoCreateLobbyOnClick();
        }

        // Join the first available public lobby
        if (Input.GetKeyDown(KeyCode.J))
        {
            DoJoinFirstAvailablePublicLobbyOnClick();
        }

        // Leave the current lobby
        if (Input.GetKeyDown(KeyCode.L))
        {
            DoLeaveCurrentLobbyOnClick();
        }
    }
    
    void SubscribeToLobbyEvents()
    {
        lobbyManager.lobbyListEvents.onLobbiesRefreshed.AddListener(HandleLobbiesRefreshed);
        lobbyManager.individualLobbyEvents.onLobbyJoined.AddListener(HandleLobbyJoined);
        lobbyManager.individualLobbyEvents.onLobbyLeft.AddListener(HandleLobbyLeft);
        lobbyManager.playerEvents.onPlayerJoined.AddListener(HandlePlayerJoinedLobby);
        lobbyManager.playerEvents.onPlayerLeft.AddListener(HandlePlayerLeftLobby);
        lobbyManager.matchEvents.onMatchStarted.AddListener(HandleMatchStarted);
        lobbyManager.matchEvents.onMatchEnded.AddListener(HandleMatchEnded);
        lobbyManager.systemEvents.onError.AddListener(HandleError);
    }

    void UnsubscribeFromLobbyEvents()
    {
        lobbyManager.lobbyListEvents.onLobbiesRefreshed.RemoveListener(HandleLobbiesRefreshed);
        lobbyManager.individualLobbyEvents.onLobbyJoined.RemoveListener(HandleLobbyJoined);
        lobbyManager.individualLobbyEvents.onLobbyLeft.RemoveListener(HandleLobbyLeft);
        lobbyManager.playerEvents.onPlayerJoined.RemoveListener(HandlePlayerJoinedLobby);
        lobbyManager.playerEvents.onPlayerLeft.RemoveListener(HandlePlayerLeftLobby);
        lobbyManager.matchEvents.onMatchStarted.RemoveListener(HandleMatchStarted);
        lobbyManager.matchEvents.onMatchEnded.RemoveListener(HandleMatchEnded);
        lobbyManager.systemEvents.onError.RemoveListener(HandleError);
    }

    public void DoRefreshLobbiesOnClick()
    {
        Debug.Log("Attempting to refresh lobbies...");
        lobbyManager.RefreshLobbies();
    }

    public void DoCreateLobbyOnClick()
    {
        Debug.Log($"Attempting to create lobby '{lobbyName}'...");
        lobbyManager.CreateLobby(
            lobbyName,
            maxPlayers,
            isPrivate,
            allowLateJoin,
            region,
            new Dictionary<string, object>(), // Empty custom settings
            (lobby) => Debug.Log($"Successfully created lobby: {lobby.name} ({lobby.id})"),
            (error) => Debug.LogError($"Failed to create lobby: {error.Message}")
        );
    }

    public void DoJoinFirstAvailablePublicLobbyOnClick()
    {
        var lobbies = lobbyManager.GetAvailableLobbies();
        var firstPublicLobby = lobbies.Find(l => !l.isPrivate && l.currentPlayers < l.maxPlayers);

        if (firstPublicLobby != null)
        {
            Debug.Log($"Attempting to join lobby: {firstPublicLobby.name} ({firstPublicLobby.id})");
            lobbyManager.JoinLobby(
                firstPublicLobby.id,
                (lobby) => Debug.Log($"Successfully joined lobby: {lobby.name}"),
                (error) => Debug.LogError($"Failed to join lobby: {error.Message}")
            );
        }
        else
        {
            Debug.LogWarning("No public lobbies available to join. Creating a new one instead.");
            DoCreateLobbyOnClick();
        }
    }
    
    public void DoLeaveCurrentLobbyOnClick()
    {
        if (lobbyManager.IsInLobby())
        {
            Debug.Log("Attempting to leave current lobby...");
            lobbyManager.LeaveLobby(
                () => Debug.Log("Successfully left lobby."),
                (error) => Debug.LogError($"Failed to leave lobby: {error.Message}")
            );
        }
        else
        {
            Debug.LogWarning("Not in a lobby, cannot leave.");
        }
    }
    
    private void HandleLobbiesRefreshed(List<Lobby> lobbies)
    {
        Debug.Log($"Lobby list refreshed. Found {lobbies.Count} lobbies.");
    }

    private void HandleLobbyJoined(Lobby lobby)
    {
        Debug.Log($"Successfully joined lobby: {lobby.name} ({lobby.id})");
    }

    private void HandleLobbyLeft()
    {
        Debug.Log("Successfully left the lobby.");
    }
    
    private void HandlePlayerJoinedLobby(string playerId)
    {
        Debug.Log($"Player {playerId} joined the lobby.");
    }

    private void HandlePlayerLeftLobby(string playerId)
    {
        Debug.Log($"Player {playerId} left the lobby.");
    }

    private void HandleMatchStarted(Lobby lobby)
    {
        Debug.Log("Match started!");
    }

    private void HandleMatchEnded(Lobby lobby)
    {
        Debug.Log("Match ended.");
    }

    private void HandleError(string errorMessage)
    {
        Debug.LogError($"An error occurred: {errorMessage}");
    }
} 