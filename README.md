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

Here is a basic example of how to use the PlayFlow Lobby Manager to create and join a lobby:

```csharp
using UnityEngine;
using PlayFlow;

public class LobbyExample : MonoBehaviour
{
    private PlayFlowLobbyManager lobbyManager;

    void Start()
    {
        lobbyManager = FindObjectOfType<PlayFlowLobbyManager>();
    }

    public async void CreateLobby()
    {
        await lobbyManager.CreateLobbyAsync();
    }

    public async void JoinLobby(string lobbyId)
    {
        await lobbyManager.JoinLobbyAsync(lobbyId);
    }
}
```

## Contact

If you have any questions or feedback, please contact us at [support@getplayflow.com](mailto:support@getplayflow.com) or join our [Discord server](https://discord.gg/P5w45Vx5Q8).