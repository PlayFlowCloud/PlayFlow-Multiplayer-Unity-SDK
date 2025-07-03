using UnityEngine;
using UnityEngine.Events;
using System;

namespace PlayFlow
{
    [System.Serializable]
    public class PlayerEvent : UnityEvent<string, PlayerAction> { }
    
    [System.Serializable]
    public class ErrorEvent : UnityEvent<string> { }
    
    [System.Serializable]
    public class LobbyEvent : UnityEvent<Lobby> { }
    
    [System.Serializable]
    public class ConnectionInfoEvent : UnityEvent<ConnectionInfo> { }
    
    [System.Serializable]
    public class StateChangedEvent : UnityEvent<LobbyState, LobbyState> { }
    
    [System.Serializable]
    public class VoidEvent : UnityEvent { }
    
    [System.Serializable]
    public class StringEvent : UnityEvent<string> { }
    
    public class PlayFlowEvents : MonoBehaviour
    {
        [Header("Lobby Events")]
        [Tooltip("Fired when a lobby is successfully created")]
        public LobbyEvent OnLobbyCreated = new LobbyEvent();
        
        [Tooltip("Fired when successfully joined a lobby")]
        public LobbyEvent OnLobbyJoined = new LobbyEvent();
        
        [Tooltip("Fired when the current lobby is updated")]
        public LobbyEvent OnLobbyUpdated = new LobbyEvent();
        
        [Tooltip("Fired when leaving a lobby")]
        public VoidEvent OnLobbyLeft = new VoidEvent();
        
        [Header("Match Events")]
        [Tooltip("Fired when the match has started")]
        public LobbyEvent OnMatchStarted = new LobbyEvent();
        
        [Tooltip("Fired when the match is running")]
        public ConnectionInfoEvent OnMatchRunning = new ConnectionInfoEvent();
        
        [Tooltip("Fired when the match has ended")]
        public LobbyEvent OnMatchEnded = new LobbyEvent();
        
        [Header("Player Events")]
        [Tooltip("Fired when a player joins the lobby")]
        public PlayerEvent OnPlayerJoined = new PlayerEvent();
        
        [Tooltip("Fired when a player leaves the lobby")]
        public PlayerEvent OnPlayerLeft = new PlayerEvent();
        
        [Header("System Events")]
        [Tooltip("Fired when connected to the service")]
        public VoidEvent OnConnected = new VoidEvent();
        
        [Tooltip("Fired when disconnected from the service")]
        public VoidEvent OnDisconnected = new VoidEvent();
        
        [Tooltip("Fired when an error occurs")]
        public StringEvent OnError = new StringEvent();
        
        [Header("Debug")]
        [SerializeField] private bool _logEvents = false;
        
        // Helper methods for safe invocation
        public void InvokeLobbyCreated(Lobby lobby)
        {
            SafeInvoke(() => OnLobbyCreated?.Invoke(lobby), "LobbyCreated", lobby);
        }
        
        public void InvokeLobbyJoined(Lobby lobby)
        {
            SafeInvoke(() => OnLobbyJoined?.Invoke(lobby), "LobbyJoined", lobby);
        }
        
        public void InvokeLobbyUpdated(Lobby lobby)
        {
            SafeInvoke(() => OnLobbyUpdated?.Invoke(lobby), "LobbyUpdated", lobby);
        }
        
        public void InvokeLobbyLeft()
        {
            SafeInvoke(() => OnLobbyLeft?.Invoke(), "LobbyLeft");
        }
        
        public void InvokeMatchStarted(Lobby lobby)
        {
            SafeInvoke(() => OnMatchStarted?.Invoke(lobby), "MatchStarted", lobby);
        }
        
        public void InvokeMatchRunning(ConnectionInfo info)
        {
            SafeInvoke(() => OnMatchRunning?.Invoke(info), "MatchRunning", info);
        }
        
        public void InvokeMatchEnded(Lobby lobby)
        {
            SafeInvoke(() => OnMatchEnded?.Invoke(lobby), "MatchEnded", lobby);
        }
        
        public void InvokePlayerJoined(string playerId)
        {
            SafeInvoke(() => OnPlayerJoined?.Invoke(playerId, PlayerAction.Joined), "PlayerJoined", playerId);
        }
        
        public void InvokePlayerLeft(string playerId, PlayerAction action)
        {
            SafeInvoke(() => OnPlayerLeft?.Invoke(playerId, action), "PlayerLeft", playerId);
        }
        
        public void InvokeConnected()
        {
            SafeInvoke(() => OnConnected?.Invoke(), "Connected");
        }
        
        public void InvokeDisconnected()
        {
            SafeInvoke(() => OnDisconnected?.Invoke(), "Disconnected");
        }
        
        public void InvokeError(string error)
        {
            SafeInvoke(() => OnError?.Invoke(error), "Error", error);
        }
        
        private void SafeInvoke(Action action, string eventName, object data = null)
        {
            try
            {
                if (_logEvents)
                {
                    var dataString = data != null ? $" - {data}" : "";
                    Debug.Log($"[PlayFlowEvents] {eventName}{dataString}");
                }
                
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayFlowEvents] Error in {eventName} event: {e.Message}");
                OnError?.Invoke($"Event error: {e.Message}");
            }
        }
    }
} 