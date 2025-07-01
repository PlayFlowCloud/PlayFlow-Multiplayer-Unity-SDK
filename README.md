# PlayFlow Multiplayer Unity SDK

![PlayFlow](https://getplayflow.com/wp-content/uploads/2024/04/playflow_logo_v2.png)

The official Unity SDK for the [PlayFlow Multiplayer Platform](https://getplayflow.com). This package provides tools for game server management, lobbies, and matchmaking.

## Installation

To install the PlayFlow Multiplayer Unity SDK, follow these steps:

1. Open your Unity project.
2. Go to **Window > Package Manager**.
3. Click the **+** button in the top-left corner and select **Add package from git URL...**
4. Enter the following URL:

```
https://github.com/PlayFlow-Cloud/PlayFlow-Multiplayer-Unity-SDK.git
```

5. Click **Add**.

## Getting Started

To get started with the PlayFlow Multiplayer Unity SDK, please refer to our [official documentation](https://docs.getplayflow.com).

## Sample Usage 

Here is a basic example of how to use the PlayFlow Lobby Manager with the platform-safe callback system. This approach ensures your game will work on all platforms, including WebGL and consoles.

```csharp
using UnityEngine;
using PlayFlow;
using System; // Required for Action<> callbacks

public class LobbyExample : MonoBehaviour
{
    private PlayFlowLobbyManager lobbyManager;

    void Start()
    {
        // Find the Lobby Manager in your scene
        lobbyManager = FindObjectOfType<PlayFlowLobbyManager>();
        
        // Subscribe to an event, for example, when you join a lobby
        lobbyManager.individualLobbyEvents.onLobbyJoined.AddListener(HandleLobbyJoined);
    }

    // --- Public methods you can link to UI buttons ---

    public void CreateLobby()
    {
        Debug.Log("Creating a new lobby...");

        // Define what happens on success
        Action<Lobby> onSuccess = (createdLobby) => {
            Debug.Log($"Lobby created successfully! ID: {createdLobby.id}");
        };

        // Define what happens on error
        Action<Exception> onError = (error) => {
            Debug.LogError($"Failed to create lobby: {error.Message}");
        };

        // Call the method with the callbacks
        lobbyManager.CreateLobby(onSuccess, onError);
    }

    public void JoinFirstAvailableLobby()
    {
        // Find a public, non-full lobby from the available list
        var lobbyToJoin = lobbyManager.GetAvailableLobbies().Find(l => !l.isPrivate && l.currentPlayers < l.maxPlayers);

        if (lobbyToJoin != null)
        {
            Debug.Log($"Joining lobby: {lobbyToJoin.name}...");
            lobbyManager.JoinLobby(
                lobbyToJoin.id,
                (joinedLobby) => {
                    Debug.Log($"Successfully joined lobby: {joinedLobby.name}");
                },
                (error) => {
                    Debug.LogError($"Failed to join lobby: {error.Message}");
                }
            );
        }
        else
        {
            Debug.LogWarning("No available public lobbies found. Creating a new one.");
            CreateLobby();
        }
    }
    
    // --- Event Handler ---
    
    private void HandleLobbyJoined(Lobby lobby)
    {
        Debug.Log($"Event received: Successfully joined lobby '{lobby.name}' with {lobby.currentPlayers} players.");
        // Your logic to transition to the lobby screen would go here
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (lobbyManager != null)
        {
            lobbyManager.individualLobbyEvents.onLobbyJoined.RemoveListener(HandleLobbyJoined);
        }
    }
}
```

## Contact

If you have any questions or feedback, please contact us at [support@playflowcloud.com](mailto:support@getplayflow.com) or join our [Discord server](https://discord.gg/P5w45Vx5Q8).