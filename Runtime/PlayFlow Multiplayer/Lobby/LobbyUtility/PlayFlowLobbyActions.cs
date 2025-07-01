using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFlow;
using System.Collections;

namespace PlayFlow
{
    public class PlayFlowLobbyActions
    {
        private LobbyClient lobbyClient;
        private string lobbyConfigName;
        private string playerId;
        private MonoBehaviour coroutineRunner; // To start coroutines
        private PlayFlowLobbyManager manager;
        
        // Delegate for logging
        private Action<string> logger;
        private Action<string> errorLogger;
        
        // References to events
        private PlayFlowLobbyEvents.SystemEvents systemEvents;
        private PlayFlowLobbyEvents.IndividualLobbyEvents individualLobbyEvents;

        public PlayFlowLobbyActions(
            LobbyClient lobbyClient, 
            string lobbyConfigName, 
            string playerId,
            MonoBehaviour coroutineRunner, // Pass a MonoBehaviour to run coroutines
            PlayFlowLobbyManager manager,
            PlayFlowLobbyEvents.SystemEvents systemEvents,
            PlayFlowLobbyEvents.IndividualLobbyEvents individualLobbyEvents,
            Action<string> logger = null,
            Action<string> errorLogger = null)
        {
            this.lobbyClient = lobbyClient;
            this.lobbyConfigName = lobbyConfigName;
            this.playerId = playerId;
            this.coroutineRunner = coroutineRunner;
            this.manager = manager;
            this.systemEvents = systemEvents;
            this.individualLobbyEvents = individualLobbyEvents;
            this.logger = logger ?? (msg => { });
            this.errorLogger = errorLogger ?? (msg => { });
        }

        private void Run(IEnumerator coroutine)
        {
            coroutineRunner.StartCoroutine(coroutine);
        }

        public void CreateLobby(
            string newLobbyName,
            int maxPlayers,
            bool isPrivate,
            bool allowLateJoin,
            string region,
            Dictionary<string, object> lobbySettings,
            Action<Lobby> onSuccess,
            Action<Exception> onError)
        {
            Run(CreateLobbyCoroutine(newLobbyName, maxPlayers, isPrivate, allowLateJoin, region, lobbySettings, onSuccess, onError));
        }

        public IEnumerator CreateLobbyCoroutine(
            string newLobbyName,
            int maxPlayers,
            bool isPrivate,
            bool allowLateJoin,
            string region,
            Dictionary<string, object> lobbySettings,
            Action<Lobby> onSuccess,
            Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            JObject settingsJObject = lobbySettings != null ? JObject.FromObject(lobbySettings) : new JObject();

            yield return lobbyClient.CreateLobbyCoroutine(
                lobbyConfigName, newLobbyName, maxPlayers, isPrivate, false, allowLateJoin, region, settingsJObject, playerId,
                (response) => {
                    var lobby = response.ToObject<Lobby>();
                    onSuccess?.Invoke(lobby);
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError(error.Message);
                    systemEvents?.InvokePostAPICall();
            }
            );
        }

        public void JoinLobby(string lobbyId, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            Run(JoinLobbyCoroutine(lobbyId, onSuccess, onError));
        }

        public IEnumerator JoinLobbyCoroutine(string lobbyId, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            var metadata = new JObject { ["displayName"] = "Player" }; // Example metadata
            
            yield return lobbyClient.AddPlayerToLobbyCoroutine(lobbyConfigName, lobbyId, playerId, metadata,
                (response) => {
                    onSuccess?.Invoke(response.ToObject<Lobby>());
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError(error.Message);
                    systemEvents?.InvokePostAPICall();
            }
            );
        }

        public void LeaveLobby(string currentLobbyId, Action<bool> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));
                
            Run(LeaveLobbyCoroutine(currentLobbyId, onSuccess, onError));
        }

        public IEnumerator LeaveLobbyCoroutine(string currentLobbyId, Action<bool> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            yield return lobbyClient.RemovePlayerFromLobbyCoroutine(lobbyConfigName, currentLobbyId, playerId, playerId, false,
                (response) => {
                    onSuccess?.Invoke(true);
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to leave lobby: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void JoinLobbyByCode(string code, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            Run(JoinLobbyByCodeCoroutine(code, onSuccess, onError));
        }

        private IEnumerator JoinLobbyByCodeCoroutine(string code, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            yield return lobbyClient.JoinLobbyByCodeCoroutine(lobbyConfigName, code, playerId, null,
                (response) => {
                    onSuccess?.Invoke(response.ToObject<Lobby>());
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to join lobby by code: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void KickPlayer(string currentLobbyId, string playerToKick, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            Run(KickPlayerCoroutine(currentLobbyId, playerToKick, onSuccess, onError));
        }

        private IEnumerator KickPlayerCoroutine(string currentLobbyId, string playerToKick, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            yield return lobbyClient.RemovePlayerFromLobbyCoroutine(lobbyConfigName, currentLobbyId, playerToKick, playerId, true,
                (response) => {
                    onSuccess?.Invoke(response.ToObject<Lobby>());
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to kick player: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void UpdateAllLobbySettings(
            string currentLobbyId,
            Action<Lobby> onSuccess,
            Action<Exception> onError,
            Dictionary<string, object> newSettings = null,
            string newName = null,
            int? newMaxPlayers = null,
            bool? newIsPrivate = null,
            bool? newAllowLateJoin = null)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            Run(UpdateAllLobbySettingsCoroutine(currentLobbyId, onSuccess, onError, newSettings, newName, newMaxPlayers, newIsPrivate, newAllowLateJoin));
        }

        private IEnumerator UpdateAllLobbySettingsCoroutine(
            string currentLobbyId,
            Action<Lobby> onSuccess,
            Action<Exception> onError,
            Dictionary<string, object> newSettings = null,
            string newName = null,
            int? newMaxPlayers = null,
            bool? newIsPrivate = null,
            bool? newAllowLateJoin = null)
        {
            systemEvents?.InvokePreAPICall();
            
            JObject innerSettingsPayload = new JObject();
            if (newName != null) innerSettingsPayload["name"] = newName;
            if (newMaxPlayers.HasValue) innerSettingsPayload["maxPlayers"] = newMaxPlayers.Value;
            if (newIsPrivate.HasValue) innerSettingsPayload["isPrivate"] = newIsPrivate.Value;
            if (newAllowLateJoin.HasValue) innerSettingsPayload["allowLateJoin"] = newAllowLateJoin.Value;
            if (newSettings != null && newSettings.Count > 0) innerSettingsPayload["settings"] = JObject.FromObject(newSettings);
            else if (newSettings != null && newSettings.Count == 0) innerSettingsPayload["settings"] = new JObject();

            JObject payload = new JObject
            {
                ["settings"] = innerSettingsPayload
            };

            yield return lobbyClient.UpdateLobbyCoroutine(lobbyConfigName, currentLobbyId, playerId, payload,
                (response) => {
                    onSuccess?.Invoke(response.ToObject<Lobby>());
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to update lobby settings: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void UpdateLobbySettings(string currentLobbyId, Dictionary<string, object> lobbySettings, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            Run(UpdateLobbySettingsCoroutine(currentLobbyId, lobbySettings, onSuccess, onError));
        }

        private IEnumerator UpdateLobbySettingsCoroutine(string currentLobbyId, Dictionary<string, object> lobbySettings, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            JObject innerCustomSettings = new JObject();
            if (lobbySettings != null && lobbySettings.Count > 0) innerCustomSettings = JObject.FromObject(lobbySettings);
            
            JObject payload = new JObject
            {
                ["settings"] = new JObject
                {
                    ["settings"] = innerCustomSettings
                }
            };
            
            yield return lobbyClient.UpdateLobbyCoroutine(lobbyConfigName, currentLobbyId, playerId, payload,
                (response) => {
                    onSuccess?.Invoke(response.ToObject<Lobby>());
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to update lobby settings: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void StartMatch(string currentLobbyId, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            Run(StartMatchCoroutine(currentLobbyId, onSuccess, onError));
        }

        private IEnumerator StartMatchCoroutine(string currentLobbyId, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            var payload = new JObject { ["status"] = "in_game" };
            yield return lobbyClient.UpdateLobbyCoroutine(lobbyConfigName, currentLobbyId, playerId, payload,
                (response) => {
                    onSuccess?.Invoke(response.ToObject<Lobby>());
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to start game: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void EndGame(string currentLobbyId, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            Run(EndGameCoroutine(currentLobbyId, onSuccess, onError));
        }

        private IEnumerator EndGameCoroutine(string currentLobbyId, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            var payload = new JObject { ["status"] = "waiting" };
            yield return lobbyClient.UpdateLobbyCoroutine(lobbyConfigName, currentLobbyId, playerId, payload,
                (response) => {
                    onSuccess?.Invoke(response.ToObject<Lobby>());
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to end game: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void TransferHost(string currentLobbyId, string newHostId, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            Run(TransferHostCoroutine(currentLobbyId, newHostId, onSuccess, onError));
        }

        private IEnumerator TransferHostCoroutine(string currentLobbyId, string newHostId, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            var payload = new JObject { ["host"] = newHostId };
            yield return lobbyClient.UpdateLobbyCoroutine(lobbyConfigName, currentLobbyId, playerId, payload,
                (response) => {
                    onSuccess?.Invoke(response.ToObject<Lobby>());
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to transfer host: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void SendPlayerStateUpdate(string currentLobbyId, Dictionary<string, object> state, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));
                
            if (state == null)
                throw new ArgumentNullException(nameof(state), "State cannot be null");

            Run(SendPlayerStateUpdateCoroutine(currentLobbyId, state, onSuccess, onError));
        }

        private IEnumerator SendPlayerStateUpdateCoroutine(string currentLobbyId, Dictionary<string, object> state, Action<Lobby> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            var payload = new JObject { ["playerState"] = JObject.FromObject(state) };
            yield return lobbyClient.UpdateLobbyCoroutine(lobbyConfigName, currentLobbyId, playerId, payload,
                (response) => {
                    onSuccess?.Invoke(response.ToObject<Lobby>());
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to update player state: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void ListLobbies(Action<JArray> onSuccess, Action<Exception> onError)
        {
            Run(ListLobbiesCoroutine(onSuccess, onError));
        }

        public IEnumerator ListLobbiesCoroutine(Action<JArray> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            yield return lobbyClient.ListLobbiesCoroutine(lobbyConfigName,
                (response) => {
                    onSuccess?.Invoke(response as JArray);
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to list lobbies: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        public void GetLobby(string lobbyId, Action<JObject> onSuccess, Action<Exception> onError)
        {
            Run(GetLobbyCoroutine(lobbyId, onSuccess, onError));
        }

        public IEnumerator GetLobbyCoroutine(string lobbyId, Action<JObject> onSuccess, Action<Exception> onError)
        {
            systemEvents?.InvokePreAPICall();
            
            yield return lobbyClient.GetLobbyCoroutine(lobbyConfigName, lobbyId,
                (response) => {
                    onSuccess?.Invoke(response as JObject);
                    systemEvents?.InvokePostAPICall();
                },
                (error) => {
                    onError?.Invoke(error);
                    systemEvents?.InvokeError($"Failed to get lobby: {error.Message}");
                    systemEvents?.InvokePostAPICall();
                }
            );
        }

        // Update the playerId used for API calls
        public void SetPlayerId(string newPlayerId)
        {
            playerId = newPlayerId;
        }
    }
} 
