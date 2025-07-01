using System;
using System.Collections;
using System.Collections.Generic;

namespace PlayFlow
{
    // Network interfaces
    public interface INetworkQueue
    {
        void EnqueueRequest(NetworkRequest request);
    }
    
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
        IEnumerator LeaveLobby(string lobbyId, string playerId, Action onSuccess, Action<string> onError);
        IEnumerator GetLobby(string lobbyId, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator ListLobbies(Action<List<Lobby>> onSuccess, Action<string> onError);
        IEnumerator UpdatePlayerState(string lobbyId, string playerId, Dictionary<string, object> state, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator UpdateLobbyStatus(string lobbyId, string playerId, string status, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator KickPlayer(string lobbyId, string requesterId, string playerToKickId, Action<Lobby> onSuccess, Action<string> onError);
        IEnumerator UpdateLobby(string lobbyId, string requesterId, Newtonsoft.Json.Linq.JObject payload, Action<Lobby> onSuccess, Action<string> onError);
    }
    
    public interface ILobbyCache
    {
        void SetCurrentLobby(Lobby lobby);
        Lobby GetCurrentLobby();
        void ClearCurrentLobby();
        void SetAvailableLobbies(List<Lobby> lobbies);
        List<Lobby> GetAvailableLobbies();
        bool TryGetLobby(string lobbyId, out Lobby lobby);
    }
    
    public interface IEventDispatcher
    {
        void Dispatch(string eventName, object data = null);
        void Subscribe(string eventName, Action<object> handler);
        void Unsubscribe(string eventName, Action<object> handler);
    }
    
    // Data structures
    public class NetworkRequest
    {
        public string Endpoint { get; set; }
        public string Method { get; set; } = "GET";
        public string Data { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public Action<NetworkResponse> OnSuccess { get; set; }
        public Action<string> OnError { get; set; }
        public Action<float> OnProgress { get; set; }
        public int RetryCount { get; set; }
        public bool IsComplete { get; set; }
    }
    
    public class NetworkResponse
    {
        public long StatusCode { get; set; }
        public string Data { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }
    
    public enum LobbyState
    {
        Disconnected,
        Connecting,
        Connected,
        InLobby,
        Error
    }
    
    public enum PlayerAction
    {
        Joined,
        Left,
        StateChanged,
        Disconnected
    }
} 