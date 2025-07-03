<div align="center">

<img src="https://raw.githubusercontent.com/PlayFlowCloud/PlayFlow-Multiplayer-Unity-SDK/main/Resources/playflow.png" width="150">

# PlayFlow Multiplayer Unity SDK
**The complete, production-ready backend for your Unity multiplayer game.**

</div>

<div align="center">

[![Made with Unity](https://img.shields.io/badge/Made%20with-Unity-57b9d3.svg?style=for-the-badge&logo=unity)](https://unity.com)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg?style=for-the-badge)](https://opensource.org/licenses/Apache-2.0)
[![YouTube Channel](https://img.shields.io/badge/YouTube-%40playflow__cloud-red.svg?style=for-the-badge&logo=youtube)](https://www.youtube.com/@playflow_cloud)
[![Discord](https://img.shields.io/discord/837375669335195688?style=for-the-badge&logo=discord&label=Discord)](https://discord.gg/P5w45Vx5Q8)

</div>

---

**PlayFlow** is the all-in-one multiplayer platform that handles the complexity of game server hosting, lobbies, and matchmaking, so you can focus on building your game. This official Unity SDK is your gateway to unlocking PlayFlow's powerful features directly in your project.

- **Website:** [playflowcloud.com](https://playflowcloud.com)
- **Documentation:** [documentation.playflowcloud.com](https://documentation.playflowcloud.com)

## Key Features

The PlayFlow SDK is designed to be a complete, drop-in solution for your multiplayer backend needs, organized into three core components:

### Lobby System
A powerful and flexible lobby system designed for any game type.
- **Public & Private Lobbies:** Create lobbies that are open to all or invite-only.
- **Invite Codes:** Simple, shareable codes for joining private games.
- **Host Migration:** Automatic host transfer ensures the game continues if the host disconnects.
- **Custom Lobby Settings:** Define any game rules, maps, or modes.
- **Player State Sync:** Easily synchronize player data (like character selection or ready status) across the lobby.
- **Late Join:** Allow players to join a match that's already in progress.

### Matchmaking
A fast, scalable matchmaking engine to get players into the right games.
- **Rule-Based Matching:** Create custom rules to match players based on skill (ELO), region, game mode, or any other custom attribute.
- **Region & Latency Matching:** Ensure players have the best connection by matching them in the closest geographical region.
- **Backfill Support:** Automatically find new players to fill empty slots in active matches.

### Game Server Management
A robust, hands-off system for managing and scaling your dedicated game servers.
- **On-Demand Servers:** Game servers are spun up instantly when a match starts and automatically shut down when it ends, saving you money.
- **Global Regions:** Deploy your game servers across multiple regions worldwide to provide low latency for all players.
- **Auto-Scaling:** The system automatically handles scaling your server fleet to meet player demand.
- **Headless Linux Builds:** A simple build tool is included to create optimized, headless Linux builds of your game server right from the Unity Editor.

## Installation

The SDK is a standard Unity package. You can install it directly from the Git URL.

1.  In your Unity project, open the **Package Manager** (`Window > Package Manager`).
2.  Click the `+` icon in the top-left corner and select **"Add package from git URL..."**.
3.  Enter the following URL and click **Add**:
    ```
    https://github.com/PlayFlowCloud/PlayFlow-Multiplayer-Unity-SDK.git
    ```
The package will be installed and ready to use.

## Getting Started

Integrating the PlayFlow Lobby System is designed to be fast and intuitive.

1.  **Add the Manager:** Create an empty GameObject in your persistent scene (e.g., a "Managers" object) and add the `PlayFlowLobbyManagerV2` component to it.
2.  **Configure API Key:** In the Inspector for the `PlayFlowLobbyManagerV2` component, enter your **API Key** from the [PlayFlow Dashboard](https://playflowcloud.com).
3.  **Initialize the SDK:** In your own manager script, get the singleton instance and initialize it with a unique player ID.

    ```csharp
    using UnityEngine;
    using PlayFlow;
    
    public class MyGameManager : MonoBehaviour
    {
        void Start()
        {
            // Generate a unique ID for the player for this session
            string playerId = "player-" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            // Initialize the manager. All functionality is available after the OnManagerReady callback.
            PlayFlowLobbyManagerV2.Instance.Initialize(playerId, OnManagerReady);
        }

        void OnManagerReady()
        {
            Debug.Log("PlayFlow is ready to use!");
            // Now you can start creating or joining lobbies.
        }
    }
    ```

## Example Usage

Here is a practical example based on `LobbyHelloWorld.cs` showing how to create a lobby, join a lobby, and react to events.

```csharp
using UnityEngine;
using PlayFlow;
using System;

public class LobbyController : MonoBehaviour
{
    private void Start()
    {
        // Ensure the manager is initialized before subscribing to events.
        // This is typically done in a separate script that runs on app startup.
        if (!PlayFlowLobbyManagerV2.Instance.IsReady)
        {
            PlayFlowLobbyManagerV2.Instance.Initialize("my-player-id", SubscribeToEvents);
        }
        else
        {
            SubscribeToEvents();
        }
    }

    private void SubscribeToEvents()
    {
        var events = PlayFlowLobbyManagerV2.Instance.Events;
        
        // Listen for when you successfully join a lobby
        events.OnLobbyJoined.AddListener(HandleLobbyJoined);
        
        // Listen for when another player joins your current lobby
        events.OnPlayerJoined.AddListener(HandlePlayerJoined);
        
        // Listen for when a player leaves
        events.OnPlayerLeft.AddListener(HandlePlayerLeft);
        
        // Listen for when the match starts and the server is ready
        events.OnMatchRunning.AddListener(HandleMatchRunning);
        
        // Listen for critical errors
        events.OnError.AddListener(HandleError);
    }
    
    // --- Public methods you can link to UI buttons ---

    public void CreateLobby()
    {
        Debug.Log("Attempting to create a lobby...");
        PlayFlowLobbyManagerV2.Instance.CreateLobby(
            "My Cool Game", 
            maxPlayers: 4, 
            isPrivate: false,
            onSuccess: (lobby) => {
                Debug.Log($"Lobby created! ID: {lobby.id}. Invite Code: {lobby.inviteCode}");
            },
            onError: (error) => {
                Debug.LogError($"Failed to create lobby: {error}");
            }
        );
    }

    public void JoinFirstAvailableLobby()
    {
        Debug.Log("Looking for a lobby to join...");
        PlayFlowLobbyManagerV2.Instance.GetAvailableLobbies(
            onSuccess: (lobbies) => {
                var availableLobby = lobbies.Find(l => !l.isPrivate && l.currentPlayers < l.maxPlayers);
                if (availableLobby != null)
                {
                    PlayFlowLobbyManagerV2.Instance.JoinLobby(availableLobby.id);
                }
                else
                {
                    Debug.LogWarning("No public lobbies available. Creating a new one instead.");
                    CreateLobby();
                }
            },
            onError: (error) => {
                Debug.LogError($"Error getting lobby list: {error}");
            }
        );
    }

    public void StartMatch()
    {
        if (PlayFlowLobbyManagerV2.Instance.IsHost)
        {
            Debug.Log("Host is starting the match...");
            PlayFlowLobbyManagerV2.Instance.StartMatch();
        }
        else
        {
            Debug.LogWarning("Only the host can start the match.");
        }
    }
    
    // --- Event Handlers ---
    
    private void HandleLobbyJoined(Lobby lobby)
    {
        Debug.Log($"Successfully joined lobby '{lobby.name}'. Players: {lobby.currentPlayers}/{lobby.maxPlayers}");
        // Your logic to load the lobby UI scene would go here.
    }
    
    private void HandlePlayerJoined(PlayerAction action)
    {
        Debug.Log($"Player '{action.PlayerId}' joined the lobby!");
        // Update your UI to show the new player.
    }

    private void HandlePlayerLeft(PlayerAction action)
    {
        Debug.Log($"Player '{action.PlayerId}' left the lobby.");
        // Update your UI to remove the player.
    }

    private void HandleMatchRunning(ConnectionInfo connectionInfo)
    {
        Debug.Log($"Server is ready! Connecting to IP: {connectionInfo.Ip}, Port: {connectionInfo.Port}");
        // Here you would use a transport layer (like Netcode for GameObjects, Mirror, etc.)
        // to connect to the dedicated server using the provided IP and Port.
    }

    private void HandleError(string errorMessage)
    {
        Debug.LogError($"An error occurred: {errorMessage}");
        // Show an error popup to the user.
    }

    private void OnDestroy()
    {
        // It's good practice to unsubscribe from events when this object is destroyed.
        if (PlayFlowLobbyManagerV2.Instance != null)
        {
            var events = PlayFlowLobbyManagerV2.Instance.Events;
            events.OnLobbyJoined.RemoveListener(HandleLobbyJoined);
            events.OnPlayerJoined.RemoveListener(HandlePlayerJoined);
            events.OnPlayerLeft.RemoveListener(HandlePlayerLeft);
            events.OnMatchRunning.RemoveListener(HandleMatchRunning);
            events.OnError.RemoveListener(HandleError);
        }
    }
}
```

## Contact & Community

- **Discord:** Have questions or want to show off your project? [Join our Discord server!](https://discord.gg/P5w45Vx5Q8)
- **YouTube:** Check out our tutorials and feature showcases on our [YouTube Channel](https://www.youtube.com/@playflow_cloud).
- **Email:** For business inquiries or support, contact us at `support@playflowcloud.com`.

---
*PlayFlow - The fastest way to build, deploy, and scale your multiplayer game.*