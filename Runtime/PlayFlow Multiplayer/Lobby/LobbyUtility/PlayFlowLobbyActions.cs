using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFlow;

namespace PlayFlow
{
    public class PlayFlowLobbyActions
    {
        private LobbyClient lobbyClient;
        private string lobbyConfigName;
        private string playerId;
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
            PlayFlowLobbyManager manager,
            PlayFlowLobbyEvents.SystemEvents systemEvents,
            PlayFlowLobbyEvents.IndividualLobbyEvents individualLobbyEvents,
            Action<string> logger = null,
            Action<string> errorLogger = null)
        {
            this.lobbyClient = lobbyClient;
            this.lobbyConfigName = lobbyConfigName;
            this.playerId = playerId;
            this.manager = manager;
            this.systemEvents = systemEvents;
            this.individualLobbyEvents = individualLobbyEvents;
            this.logger = logger ?? ((message) => Debug.Log(message));
            this.errorLogger = errorLogger ?? ((message) => Debug.LogError(message));
        }

        public async Task<Lobby> CreateLobbyAsync(
            string newLobbyName,
            int maxPlayers,
            bool isPrivate,
            bool allowLateJoin,
            string region,
            Dictionary<string, object> lobbySettings)
        {
            systemEvents.InvokePreAPICall();
            try
            {
                JObject settingsPayload = lobbySettings != null && lobbySettings.Count > 0 
                    ? JObject.FromObject(lobbySettings) 
                    : new JObject();

                JObject createdLobbyJObject = await lobbyClient.CreateLobbyAsync(
                    lobbyConfigName,
                    newLobbyName,
                    maxPlayers,
                    isPrivate,
                    isPrivate, // useInviteCode defaults to isPrivate
                    allowLateJoin,
                    region,
                    settingsPayload,
                    playerId
                );

                if (createdLobbyJObject == null)
                {
                    throw new Exception("CreateLobbyAsync returned null JObject");
                }
                
                var createdLobby = createdLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                
                if (createdLobby == null)
                {
                    throw new Exception("Failed to deserialize created lobby from JObject");
                }

                logger($"Created lobby: {createdLobby.name}");
                return createdLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to create lobby: {e.Message}");
                systemEvents.InvokeError($"Failed to create lobby: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<Lobby> JoinLobbyAsync(string lobbyId)
        {
            systemEvents.InvokePreAPICall();
            try
            {
                JObject joinedLobbyJObject = await lobbyClient.AddPlayerToLobbyAsync(lobbyConfigName, lobbyId, playerId, null);
                if (joinedLobbyJObject == null) throw new Exception("AddPlayerToLobbyAsync returned null");
                var joinedLobby = joinedLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                if (joinedLobby == null) throw new Exception("Failed to deserialize joined lobby");

                logger($"Joined lobby: {joinedLobby.name}");
                return joinedLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to join lobby: {e.Message}");
                systemEvents.InvokeError($"Failed to join lobby: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task LeaveLobbyAsync(string currentLobbyId)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));
                
            systemEvents.InvokePreAPICall();
            try
            {
                await lobbyClient.RemovePlayerFromLobbyAsync(lobbyConfigName, currentLobbyId, playerId, playerId, false);
                logger("Left lobby successfully");
            }
            catch (Exception e)
            {
                errorLogger($"Failed to leave lobby: {e.Message}");
                systemEvents.InvokeError($"Failed to leave lobby: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<Lobby> JoinLobbyByCodeAsync(string code)
        {
            systemEvents.InvokePreAPICall();
            try
            {
                JObject joinedLobbyJObject = await lobbyClient.JoinLobbyByCodeAsync(lobbyConfigName, code, playerId, null);
                if (joinedLobbyJObject == null) throw new Exception("JoinLobbyByCodeAsync returned null");
                var joinedLobby = joinedLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                if (joinedLobby == null) throw new Exception("Failed to deserialize joined lobby by code");

                logger($"Joined lobby by code: {joinedLobby.name}");
                return joinedLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to join lobby by code: {e.Message}");
                systemEvents.InvokeError($"Failed to join lobby by code: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<Lobby> KickPlayerAsync(string currentLobbyId, string playerToKick)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            systemEvents.InvokePreAPICall();
            try
            {
                JObject updatedLobbyJObject = await lobbyClient.RemovePlayerFromLobbyAsync(lobbyConfigName, currentLobbyId, playerToKick, playerId, true);
                if (updatedLobbyJObject == null) throw new Exception("RemovePlayerFromLobbyAsync (kick) returned null");
                var updatedLobby = updatedLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                if (updatedLobby == null) throw new Exception("Failed to deserialize kicked lobby");

                logger($"Kicked player: {playerToKick}");
                return updatedLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to kick player: {e.Message}");
                systemEvents.InvokeError($"Failed to kick player: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<Lobby> UpdateAllLobbySettingsAsync(
            string currentLobbyId,
            Dictionary<string, object> newSettings = null,
            string newName = null,
            int? newMaxPlayers = null,
            bool? newIsPrivate = null,
            bool? newAllowLateJoin = null)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            systemEvents.InvokePreAPICall();
            try
            {
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

                JObject updatedLobbyJObject = await lobbyClient.UpdateLobbyAsync(lobbyConfigName, currentLobbyId, playerId, payload);
                if (updatedLobbyJObject == null) throw new Exception("UpdateLobbyAsync (AllSettings) returned null");
                var updatedLobby = updatedLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                if (updatedLobby == null) throw new Exception("Failed to deserialize updated lobby (AllSettings)");

                return updatedLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to update lobby settings: {e.Message}");
                systemEvents.InvokeError($"Failed to update lobby settings: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<Lobby> UpdateLobbySettingsAsync(string currentLobbyId, Dictionary<string, object> lobbySettings)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            systemEvents.InvokePreAPICall();
            try
            {
                JObject innerCustomSettings = new JObject();
                if (lobbySettings != null && lobbySettings.Count > 0) innerCustomSettings = JObject.FromObject(lobbySettings);
                
                JObject payload = new JObject
                {
                    ["settings"] = new JObject
                    {
                        ["settings"] = innerCustomSettings
                    }
                };
                
                JObject updatedLobbyJObject = await lobbyClient.UpdateLobbyAsync(lobbyConfigName, currentLobbyId, playerId, payload);
                if (updatedLobbyJObject == null) throw new Exception("UpdateLobbyAsync (Settings dictionary) returned null");
                var updatedLobby = updatedLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                if (updatedLobby == null) throw new Exception("Failed to deserialize updated lobby (Settings dictionary)");

                return updatedLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to update lobby settings: {e.Message}");
                systemEvents.InvokeError($"Failed to update lobby settings: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<Lobby> StartMatchAsync(string currentLobbyId)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            systemEvents.InvokePreAPICall();
            try
            {
                var payload = new JObject { ["status"] = "in_game" };
                JObject updatedLobbyJObject = await lobbyClient.UpdateLobbyAsync(lobbyConfigName, currentLobbyId, playerId, payload);
                if (updatedLobbyJObject == null) throw new Exception("UpdateLobbyAsync (StartMatch) returned null");
                var updatedLobby = updatedLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                if (updatedLobby == null) throw new Exception("Failed to deserialize updated lobby (StartMatch)");

                return updatedLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to start game: {e.Message}");
                systemEvents.InvokeError($"Failed to start game: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<Lobby> EndGameAsync(string currentLobbyId)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            systemEvents.InvokePreAPICall();
            try
            {
                var payload = new JObject { ["status"] = "waiting" };
                JObject updatedLobbyJObject = await lobbyClient.UpdateLobbyAsync(lobbyConfigName, currentLobbyId, playerId, payload);
                if (updatedLobbyJObject == null) throw new Exception("UpdateLobbyAsync (EndGame) returned null");
                var updatedLobby = updatedLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                if (updatedLobby == null) throw new Exception("Failed to deserialize updated lobby (EndGame)");

                return updatedLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to end game: {e.Message}");
                systemEvents.InvokeError($"Failed to end game: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<Lobby> TransferHostAsync(string currentLobbyId, string newHostId)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));

            systemEvents.InvokePreAPICall();
            try
            {
                var payload = new JObject { ["host"] = newHostId };
                JObject updatedLobbyJObject = await lobbyClient.UpdateLobbyAsync(lobbyConfigName, currentLobbyId, playerId, payload);
                if (updatedLobbyJObject == null) throw new Exception("UpdateLobbyAsync (TransferHost) returned null");
                var updatedLobby = updatedLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                if (updatedLobby == null) throw new Exception("Failed to deserialize updated lobby (TransferHost)");

                return updatedLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to transfer host: {e.Message}");
                systemEvents.InvokeError($"Failed to transfer host: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<Lobby> SendPlayerStateUpdateAsync(string currentLobbyId, Dictionary<string, object> state)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) 
                throw new ArgumentException("Lobby ID cannot be null or empty", nameof(currentLobbyId));
                
            if (state == null)
                throw new ArgumentNullException(nameof(state), "State cannot be null");

            systemEvents.InvokePreAPICall();
            try
            {
                var payload = new JObject { ["playerState"] = JObject.FromObject(state) };
                JObject updatedLobbyJObject = await lobbyClient.UpdateLobbyAsync(lobbyConfigName, currentLobbyId, playerId, payload);
                if (updatedLobbyJObject == null) throw new Exception("UpdateLobbyAsync (SendPlayerStateUpdate) returned null");
                var updatedLobby = updatedLobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                if (updatedLobby == null) throw new Exception("Failed to deserialize updated lobby (SendPlayerStateUpdate)");

                return updatedLobby;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to update player state: {e.Message}");
                systemEvents.InvokeError($"Failed to update player state: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<JArray> ListLobbiesAsync()
        {
            systemEvents.InvokePreAPICall();
            try
            {
                JArray lobbiesJArray = await lobbyClient.ListLobbiesAsync(lobbyConfigName);
                return lobbiesJArray;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to list lobbies: {e.Message}");
                systemEvents.InvokeError($"Failed to list lobbies: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        public async Task<JObject> GetLobbyAsync(string lobbyId)
        {
            systemEvents.InvokePreAPICall();
            try
            {
                JObject lobbyJObject = await lobbyClient.GetLobbyAsync(lobbyConfigName, lobbyId);
                return lobbyJObject;
            }
            catch (Exception e)
            {
                errorLogger($"Failed to get lobby: {e.Message}");
                systemEvents.InvokeError($"Failed to get lobby: {e.Message}");
                throw;
            }
            finally
            {
                systemEvents.InvokePostAPICall();
            }
        }

        // Update the playerId used for API calls
        public void SetPlayerId(string newPlayerId)
        {
            playerId = newPlayerId;
        }
    }
} 
