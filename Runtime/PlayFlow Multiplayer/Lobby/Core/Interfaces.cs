using System;
using System.Collections;
using System.Collections.Generic;

namespace PlayFlow
{
    // Network interfaces
    public interface INetworkManager
    {
        IEnumerator Get(string url, string apiKey, System.Action<string> onSuccess, System.Action<string> onError);
        IEnumerator Post(string url, string json, string apiKey, System.Action<string> onSuccess, System.Action<string> onError);
        IEnumerator Put(string url, string json, string apiKey, System.Action<string> onSuccess, System.Action<string> onError);
        IEnumerator Delete(string url, string apiKey, System.Action<string> onSuccess, System.Action<string> onError);
    }
    
    public interface ILobbyAPI
    {
        IEnumerator CreateLobby(
            string configName,
            string lobbyName, 
            int maxPlayers, 
            bool isPrivate, 
            bool allowLateJoin, 
            string region, 
            Dictionary<string, object> customSettings, 
            string playerId, 
            Action<Lobby> onSuccess, 
            Action<string> onError);
            
        IEnumerator JoinLobby(string lobbyId, string playerId, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator JoinLobbyByCode(string inviteCode, string playerId, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator LeaveLobby(string lobbyId, string playerId, Action onSuccess, Action<string> onError);
        IEnumerator GetLobby(string lobbyId, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator ListLobbies(Action<List<Lobby>> onSuccess, Action<string> onError);
        IEnumerator UpdatePlayerState(string lobbyId, string requesterId, string targetPlayerId, Dictionary<string, object> state, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator UpdateLobbyStatus(string lobbyId, string playerId, string status, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator KickPlayer(string lobbyId, string requesterId, string playerToKickId, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator UpdateLobby(string lobbyId, string requesterId, Newtonsoft.Json.Linq.JObject payload, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator FindLobbyByPlayerId(string playerId, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator DeleteLobby(string lobbyId, string requesterId, Action onSuccess, Action<string> onError);
        IEnumerator SendHeartbeat(string lobbyId, string playerId, Action onSuccess, Action<string> onError);
    }
    
    public interface IEventDispatcher
    {
        void Dispatch(string eventName, object data = null);
        void Subscribe(string eventName, Action<object> handler);
        void Unsubscribe(string eventName, Action<object> handler);
    }
    
    // Data structures
    public enum LobbyState
    {
        Disconnected,
        Connecting,
        Connected,
        InLobby,
        Error
    }
    
    public enum PlayerActionType
    {
        Join,
        Leave,
        Kick,
        StateChange,
        Disconnect
    }
    
    public struct PlayerAction
    {
        public string PlayerId;
        public PlayerActionType ActionType;

        public PlayerAction(string playerId, PlayerActionType actionType)
        {
            PlayerId = playerId;
            ActionType = actionType;
        }

        public static PlayerAction Joined(string playerId) => new PlayerAction(playerId, PlayerActionType.Join);
        public static PlayerAction Left(string playerId) => new PlayerAction(playerId, PlayerActionType.Leave);
        public static PlayerAction Kicked(string playerId) => new PlayerAction(playerId, PlayerActionType.Kick);
    }
}
 