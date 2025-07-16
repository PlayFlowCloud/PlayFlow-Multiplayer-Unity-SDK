using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace PlayFlow
{
    public class LobbyOperations
    {
        private readonly PlayFlowSettings _settings;
        private readonly PlayFlowEvents _events;
        private ILobbyAPI _api => PlayFlowCore.Instance.LobbyAPI;
        
        public LobbyOperations(PlayFlowSettings settings, PlayFlowEvents events)
        {
            _settings = settings;
            _events = events;
        }
        
        public IEnumerator CreateLobbyCoroutine(string lobbyName, int maxPlayers, bool isPrivate, bool allowLateJoin, string region, Dictionary<string, object> customSettings, string playerId, Action<Lobby> onSuccess, Action<string> onError)
        {
            if (_api == null)
            {
                onError?.Invoke("Lobby API not initialized");
                yield break;
            }
            
            yield return _api.CreateLobby(_settings.defaultLobbyConfig, lobbyName, maxPlayers, isPrivate, allowLateJoin, region, customSettings, playerId, onSuccess, onError);
        }
        
        public IEnumerator JoinLobbyCoroutine(string lobbyId, string playerId, Action<Lobby> onSuccess, Action<string> onError)
        {
            if (_api == null)
            {
                onError?.Invoke("Lobby API not initialized");
                yield break;
            }
            
            yield return _api.JoinLobby(lobbyId, playerId, onSuccess, onError);
        }
        
        public IEnumerator JoinLobbyByCodeCoroutine(string inviteCode, string playerId, Action<Lobby> onSuccess, Action<string> onError)
        {
            if (_api == null) { onError?.Invoke("Lobby API not initialized"); yield break; }
            yield return _api.JoinLobbyByCode(inviteCode, playerId, onSuccess, onError);
        }
        
        public IEnumerator LeaveLobbyCoroutine(string lobbyId, string playerId, Action onSuccess, Action<string> onError)
        {
            if (_api == null)
            {
                onError?.Invoke("Lobby API not initialized");
                yield break;
            }
            
            yield return _api.LeaveLobby(lobbyId, playerId, onSuccess, onError);
        }
        
        public IEnumerator GetLobbyCoroutine(string lobbyId, Action<Lobby> onSuccess, Action<string> onError)
        {
            if (_api == null)
            {
                onError?.Invoke("Lobby API not initialized");
                yield break;
            }
            
            yield return _api.GetLobby(lobbyId, onSuccess, onError);
        }
        
        public IEnumerator ListLobbiesCoroutine(Action<List<Lobby>> onSuccess, Action<string> onError)
        {
            if (_api == null)
            {
                onError?.Invoke("Lobby API not initialized");
                yield break;
            }
            
            yield return _api.ListLobbies(onSuccess, onError);
        }
        
        public IEnumerator UpdatePlayerStateCoroutine(string lobbyId, string requesterId, string targetPlayerId, Dictionary<string, object> state, Action<Lobby> onSuccess, Action<string> onError)
        {
            if (_api == null)
            {
                onError?.Invoke("Lobby API not initialized");
                yield break;
            }
            
            yield return _api.UpdatePlayerState(lobbyId, requesterId, targetPlayerId, state, onSuccess, onError);
        }

        public IEnumerator UpdateLobbyStatusCoroutine(string lobbyId, string playerId, string status, Action<Lobby> onSuccess, Action<string> onError)
        {
            if (_api == null)
            {
                onError?.Invoke("Lobby API not initialized");
                yield break;
            }
            
            yield return _api.UpdateLobbyStatus(lobbyId, playerId, status, onSuccess, onError);
        }

        public IEnumerator KickPlayerCoroutine(string lobbyId, string requesterId, string playerToKickId, Action<Lobby> onSuccess, Action<string> onError)
        {
            if (_api == null) { onError?.Invoke("Lobby API not initialized"); yield break; }
            yield return _api.KickPlayer(lobbyId, requesterId, playerToKickId, onSuccess, onError);
        }

        public IEnumerator TransferHostCoroutine(string lobbyId, string requesterId, string newHostId, Action<Lobby> onSuccess, Action<string> onError)
        {
            if (_api == null) { onError?.Invoke("Lobby API not initialized"); yield break; }
            var payload = new JObject { ["host"] = newHostId };
            yield return _api.UpdateLobby(lobbyId, requesterId, payload, onSuccess, onError);
        }

        public IEnumerator UpdateLobbyCoroutine(
            string lobbyId, 
            string requesterId, 
            string name,
            int? maxPlayers,
            bool? isPrivate,
            bool? useInviteCode,
            bool? allowLateJoin,
            string region,
            Dictionary<string, object> customSettings,
            Action<Lobby> onSuccess, 
            Action<string> onError)
        {
            if (_api == null) { onError?.Invoke("Lobby API not initialized"); yield break; }
            
            var payload = new JObject();
            
            // Only add properties that are being updated (not null)
            if (name != null) payload["name"] = name;
            if (maxPlayers.HasValue) payload["maxPlayers"] = maxPlayers.Value;
            if (isPrivate.HasValue) payload["isPrivate"] = isPrivate.Value;
            if (useInviteCode.HasValue) payload["useInviteCode"] = useInviteCode.Value;
            if (allowLateJoin.HasValue) payload["allowLateJoin"] = allowLateJoin.Value;
            if (region != null) payload["region"] = region;
            
            // Handle custom settings with proper nesting
            if (customSettings != null)
            {
                payload["settings"] = new JObject
                {
                    ["settings"] = JObject.FromObject(customSettings)
                };
            }
            
            yield return _api.UpdateLobby(lobbyId, requesterId, payload, onSuccess, onError);
        }

        public IEnumerator DeleteLobbyCoroutine(string lobbyId, string requesterId, Action onSuccess, Action<string> onError)
        {
            if (_api == null) { onError?.Invoke("Lobby API not initialized"); yield break; }
            
            // Use the LobbyClient directly for delete operation
            var client = new LobbyClient(PlayFlowCore.Instance.Settings.baseUrl, PlayFlowCore.Instance.Settings.apiKey);
            yield return client.DeleteLobbyCoroutine(
                PlayFlowCore.Instance.Settings.defaultLobbyConfig,
                lobbyId,
                requesterId,
                _ => onSuccess?.Invoke(), // Convert JObject response to simple callback
                ex => onError?.Invoke(ex.Message)
            );
        }

        public IEnumerator FindLobbyByPlayerIdCoroutine(string playerId, Action<Lobby> onSuccess, Action<string> onError)
        {
            if (_api == null) { onError?.Invoke("Lobby API not initialized"); yield break; }
            yield return _api.FindLobbyByPlayerId(playerId, onSuccess, onError);
        }
    }
} 