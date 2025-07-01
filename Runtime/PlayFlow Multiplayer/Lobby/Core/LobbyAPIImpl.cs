using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayFlow
{
    public class LobbyAPIImpl : ILobbyAPI
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly INetworkManager _networkManager;
        private readonly string _lobbyConfigName;
        
        public LobbyAPIImpl(string baseUrl, string apiKey, string lobbyConfigName, INetworkManager networkManager)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _lobbyConfigName = lobbyConfigName;
            _networkManager = networkManager;
        }
        
        public IEnumerator CreateLobby(string configName, string lobbyName, int maxPlayers, bool isPrivate, bool allowLateJoin, string region, Dictionary<string, object> settings, string playerId, Action<Lobby> onSuccess, Action<string> onError)
        {
            // Following PlayFlowLobbyActions.cs pattern exactly
            var url = $"{_baseUrl}/lobbies?name={UnityWebRequest.EscapeURL(configName)}";
            
            var payload = new JObject
            {
                ["name"] = lobbyName,
                ["maxPlayers"] = maxPlayers,
                ["isPrivate"] = isPrivate,
                ["useInviteCode"] = isPrivate,
                ["allowLateJoin"] = allowLateJoin,
                ["region"] = region,
                ["settings"] = settings != null ? JObject.FromObject(settings) : new JObject(),
                ["host"] = playerId
            };
            
            var json = payload.ToString();
            
            yield return _networkManager.Post(url, json, _apiKey, (response) =>
            {
                try
                {
                    var lobbyJObject = JObject.Parse(response);
                    var lobby = lobbyJObject.ToObject<Lobby>();
                    onSuccess?.Invoke(lobby);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse lobby response: {e.Message}");
                }
            }, onError);
        }
        
        public IEnumerator JoinLobby(string lobbyId, string playerId, Action<Lobby> onSuccess, Action<string> onError)
        {
            // Following PlayFlowLobbyActions.cs pattern
            var url = $"{_baseUrl}/lobbies/{lobbyId}/players?name={UnityWebRequest.EscapeURL(_lobbyConfigName)}";
            
            var metadata = new JObject { ["displayName"] = "Player" };
            var payload = new JObject
            {
                ["playerId"] = playerId,
                ["metadata"] = metadata
            };
            
            var json = payload.ToString();
            
            yield return _networkManager.Post(url, json, _apiKey, (response) =>
            {
                try
                {
                    var lobbyJObject = JObject.Parse(response);
                    var lobby = lobbyJObject.ToObject<Lobby>();
                    onSuccess?.Invoke(lobby);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse lobby response: {e.Message}");
                }
            }, onError);
        }
        
        public IEnumerator LeaveLobby(string lobbyId, string playerId, Action onSuccess, Action<string> onError)
        {
            // Following PlayFlowLobbyActions.cs pattern
            var url = $"{_baseUrl}/lobbies/{lobbyId}/players/{playerId}?name={UnityWebRequest.EscapeURL(_lobbyConfigName)}&requesterId={UnityWebRequest.EscapeURL(playerId)}&isKick=false";
            
            yield return _networkManager.Delete(url, _apiKey, (response) =>
            {
                onSuccess?.Invoke();
            }, onError);
        }
        
        public IEnumerator GetLobby(string lobbyId, Action<Lobby> onSuccess, Action<string> onError)
        {
            // Following PlayFlowLobbyActions.cs pattern
            var url = $"{_baseUrl}/lobbies/{lobbyId}?name={UnityWebRequest.EscapeURL(_lobbyConfigName)}";
            
            yield return _networkManager.Get(url, _apiKey, (response) =>
            {
                try
                {
                    var lobbyJObject = JObject.Parse(response);
                    var lobby = lobbyJObject.ToObject<Lobby>();
                    onSuccess?.Invoke(lobby);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse lobby response: {e.Message}");
                }
            }, onError);
        }
        
        public IEnumerator ListLobbies(Action<List<Lobby>> onSuccess, Action<string> onError)
        {
            // Following PlayFlowLobbyActions.cs pattern - using lobbyConfigName query parameter
            var url = $"{_baseUrl}/lobbies?name={UnityWebRequest.EscapeURL(_lobbyConfigName)}";
            
            yield return _networkManager.Get(url, _apiKey, (response) =>
            {
                try
                {
                    var lobbiesArray = JArray.Parse(response);
                    var lobbies = lobbiesArray.ToObject<List<Lobby>>();
                    onSuccess?.Invoke(lobbies ?? new List<Lobby>());
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse lobbies: {e.Message}");
                }
            }, onError);
        }
        
        public IEnumerator UpdatePlayerState(string lobbyId, string playerId, Dictionary<string, object> state, Action<Lobby> onSuccess, Action<string> onError)
        {
            // Following PlayFlowLobbyActions.cs pattern - using PUT to update lobby with playerState
            var url = $"{_baseUrl}/lobbies/{lobbyId}?name={UnityWebRequest.EscapeURL(_lobbyConfigName)}";
            
            var payload = new JObject
            {
                ["requesterId"] = playerId,
                ["playerState"] = JObject.FromObject(state)
            };
            
            var json = payload.ToString();
            
            yield return _networkManager.Put(url, json, _apiKey, (response) =>
            {
                try
                {
                    var lobbyJObject = JObject.Parse(response);
                    var lobby = lobbyJObject.ToObject<Lobby>();
                    onSuccess?.Invoke(lobby);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse lobby response: {e.Message}");
                }
            }, onError);
        }

        public IEnumerator UpdateLobby(string lobbyId, string requesterId, JObject payload, Action<Lobby> onSuccess, Action<string> onError)
        {
            var url = $"{_baseUrl}/lobbies/{lobbyId}?name={UnityWebRequest.EscapeURL(_lobbyConfigName)}";
            
            // Add requesterId to the payload
            payload["requesterId"] = requesterId;

            var json = payload.ToString();
            
            yield return _networkManager.Put(url, json, _apiKey, (response) =>
            {
                try
                {
                    var lobbyJObject = JObject.Parse(response);
                    var lobby = lobbyJObject.ToObject<Lobby>();
                    onSuccess?.Invoke(lobby);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse lobby response: {e.Message}");
                }
            }, onError);
        }

        public IEnumerator UpdateLobbyStatus(string lobbyId, string playerId, string status, Action<Lobby> onSuccess, Action<string> onError)
        {
            var payload = new JObject { ["status"] = status };
            yield return UpdateLobby(lobbyId, playerId, payload, onSuccess, onError);
        }

        public IEnumerator KickPlayer(string lobbyId, string requesterId, string playerToKickId, Action<Lobby> onSuccess, Action<string> onError)
        {
            var url = $"{_baseUrl}/lobbies/{lobbyId}/players/{playerToKickId}?name={UnityWebRequest.EscapeURL(_lobbyConfigName)}&requesterId={UnityWebRequest.EscapeURL(requesterId)}&isKick=true";
            
            yield return _networkManager.Delete(url, _apiKey, (response) =>
            {
                try
                {
                    // A kick operation can return the updated lobby object
                    var lobbyJObject = JObject.Parse(response);
                    var lobby = lobbyJObject.ToObject<Lobby>();
                    onSuccess?.Invoke(lobby);
                }
                catch (Exception e)
                {
                    // If parsing fails, it might be an empty success response for a different API version
                    // For now, we assume success if no error is thrown and parsing fails.
                    onSuccess?.Invoke(null); 
                }
            }, onError);
        }
    }
} 