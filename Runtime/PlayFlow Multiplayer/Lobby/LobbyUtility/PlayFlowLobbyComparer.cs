using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayFlow
{
    public class PlayFlowLobbyComparer
    {
        private readonly PlayFlowLobbyEvents.LobbyListEvents lobbyListEvents;
        private readonly PlayFlowLobbyEvents.IndividualLobbyEvents individualLobbyEvents;
        private readonly PlayFlowLobbyEvents.PlayerEvents playerEvents;
        private readonly PlayFlowLobbyEvents.MatchEvents matchEvents;
        private readonly bool debugLogging;
        
        private Lobby currentLobby;
        private List<string> lastPlayerIds = new List<string>();
        private Dictionary<string, object> lobbySettings = new Dictionary<string, object>();

        public PlayFlowLobbyComparer(
            PlayFlowLobbyEvents.LobbyListEvents lobbyListEvents,
            PlayFlowLobbyEvents.IndividualLobbyEvents individualLobbyEvents,
            PlayFlowLobbyEvents.PlayerEvents playerEvents,
            PlayFlowLobbyEvents.MatchEvents matchEvents,
            bool debugLogging)
        {
            this.lobbyListEvents = lobbyListEvents;
            this.individualLobbyEvents = individualLobbyEvents;
            this.playerEvents = playerEvents;
            this.matchEvents = matchEvents;
            this.debugLogging = debugLogging;
        }

        public Lobby GetCurrentLobby() => currentLobby;
        
        public List<string> GetLastPlayerIds() => lastPlayerIds;
        
        public Dictionary<string, object> GetLobbySettings() => lobbySettings;
        
        public void SetLobbySettings(Dictionary<string, object> settings)
        {
            lobbySettings = new Dictionary<string, object>(settings ?? new Dictionary<string, object>());
        }
        
        public void ResetCurrentLobbyStatus()
        {
            currentLobby = null;
            lobbySettings.Clear();
            lastPlayerIds.Clear();
        }

        private JToken DictionaryToJToken(Dictionary<string, object> dict)
        {
            if (dict == null) return JValue.CreateNull();
            return JObject.FromObject(dict);
        }

        /// <summary>
        /// Compare two Lobby objects and fire only the relevant events if something actually changed.
        /// If oldLobby is null and newLobby is not, that means we joined a new lobby.
        /// If oldLobby is not null and newLobby is null, that means we left or the lobby was destroyed.
        /// Otherwise, compare property-by-property.
        /// </summary>
        public void CompareAndFireLobbyEvents(Lobby oldLobby, Lobby newLobby)
        {
            if (oldLobby == null && newLobby == null)
            {
                if (debugLogging) Debug.Log("[CompareAndFireLobbyEvents] Both lobbies null, no changes.");
                return;
            }

            if (oldLobby == null && newLobby != null)
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] New lobby joined: {newLobby.id}.");
                currentLobby = newLobby;
                lastPlayerIds = newLobby.players?.ToList() ?? new List<string>();
                if (newLobby.settings != null) lobbySettings = new Dictionary<string, object>(newLobby.settings); else lobbySettings.Clear();
                individualLobbyEvents.InvokeLobbyJoined(newLobby);
                return;
            }

            if (oldLobby != null && newLobby == null)
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Lobby {oldLobby.id} left or destroyed.");
                // currentLobby is already oldLobby or null. Resetting related state if it was our lobby.
                if (this.currentLobby != null && this.currentLobby.id == oldLobby.id) ResetCurrentLobbyStatus();
                individualLobbyEvents.InvokeLobbyLeft();
                return;
            }

            // At this point, both oldLobby and newLobby are non-null.
            bool wasOurCurrentLobbyUpdate = this.currentLobby != null && this.currentLobby.id == oldLobby.id && oldLobby.id == newLobby.id;
            if (wasOurCurrentLobbyUpdate)
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Current lobby {oldLobby.id} is being updated. Setting manager's currentLobby reference to newLobby instance before firing detail events.");
                this.currentLobby = newLobby; 
            }

            bool anythingChanged = false;
            bool hostChanged = false;
            bool statusChanged = false;
            bool settingsDictionaryChanged = false;
            bool gameServerDictionaryChanged = false;

            // 1) Host changed
            if (oldLobby.host != newLobby.host)
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Host changed from {oldLobby.host} to {newLobby.host}.");
                playerEvents.InvokeHostTransferred(newLobby.host);
                hostChanged = true;
                anythingChanged = true;
            }

            // 2) Status changed (e.g., "waiting" to "in_game")
            if (oldLobby.status != newLobby.status)
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Lobby status changed from {oldLobby.status} to {newLobby.status}.");
                if (newLobby.status == "in_game" && oldLobby.status == "waiting") matchEvents.InvokeMatchStarted(newLobby);
                else if (oldLobby.status == "in_game" && newLobby.status == "waiting") matchEvents.InvokeMatchEnded(newLobby);
                statusChanged = true;
                anythingChanged = true;
            }

            // 3) Lobby settings (custom dictionary) changed
            JToken oldSettingsToken = DictionaryToJToken(oldLobby.settings);
            JToken newSettingsToken = DictionaryToJToken(newLobby.settings);
            if (!JToken.DeepEquals(oldSettingsToken, newSettingsToken))
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Lobby settings dictionary changed. Old: {oldSettingsToken.ToString(Formatting.None)}, New: {newSettingsToken.ToString(Formatting.None)}.");
                if (wasOurCurrentLobbyUpdate && newLobby.settings != null)
                {
                    lobbySettings = new Dictionary<string, object>(newLobby.settings); // Update local cache
                    individualLobbyEvents.InvokeLobbySettingsUpdated(lobbySettings);
                }
                else if (wasOurCurrentLobbyUpdate && newLobby.settings == null)
                {
                     lobbySettings.Clear();
                     individualLobbyEvents.InvokeLobbySettingsUpdated(new Dictionary<string, object>()); // Invoke with empty if cleared
                }
                settingsDictionaryChanged = true;
                anythingChanged = true;
            }

            // 4) Player list changes
            var oldPlayerList = oldLobby.players?.ToList() ?? new List<string>();
            var newPlayerList = newLobby.players?.ToList() ?? new List<string>();
            foreach (var playerId in newPlayerList.Except(oldPlayerList))
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Player joined: {playerId}.");
                playerEvents.InvokePlayerJoined(playerId);
                anythingChanged = true;
            }
            foreach (var playerId in oldPlayerList.Except(newPlayerList))
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Player left: {playerId}.");
                playerEvents.InvokePlayerLeft(playerId);
                anythingChanged = true;
            }
            if (wasOurCurrentLobbyUpdate) lastPlayerIds = newPlayerList; // Update local cache for our current lobby
            
            // 5) Game server data changed (entire dictionary)
            JToken oldGameServerToken = DictionaryToJToken(oldLobby.gameServer);
            JToken newGameServerToken = DictionaryToJToken(newLobby.gameServer);
            if (!JToken.DeepEquals(oldGameServerToken, newGameServerToken))
            {
                 if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Game server data changed. Old: {oldGameServerToken.ToString(Formatting.None)}, New: {newGameServerToken.ToString(Formatting.None)}.");
                gameServerDictionaryChanged = true;
                anythingChanged = true;
            }
            
            // Specific logging for game server status string change if it occurred.
            string oldGameServerStatus = oldLobby.GetGameServerStatus();
            string newGameServerStatus = newLobby.GetGameServerStatus();
            if (oldGameServerStatus != newGameServerStatus) {
                 if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Game server status string changed from '{oldGameServerStatus}' to '{newGameServerStatus}'.");
                 // Note: gameServerDictionaryChanged would already be true if only status changed, leading to anythingChanged=true
            }

            // 6) Player real-time state changes (lobbyStateRealTime)
            var oldRealTimeStates = oldLobby.lobbyStateRealTime ?? new Dictionary<string, Dictionary<string, object>>();
            var newRealTimeStates = newLobby.lobbyStateRealTime ?? new Dictionary<string, Dictionary<string, object>>();

            // Check for updated or new player states
            foreach (var newPlayerStateEntry in newRealTimeStates)
            {
                string playerId = newPlayerStateEntry.Key;
                Dictionary<string, object> newPlayerState = newPlayerStateEntry.Value;

                if (oldRealTimeStates.TryGetValue(playerId, out Dictionary<string, object> oldPlayerState))
                {
                    // Player existed, compare states
                    if (!JToken.DeepEquals(DictionaryToJToken(oldPlayerState), DictionaryToJToken(newPlayerState)))
                    {
                        if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Player {playerId} real-time state UPDATED.");
                        playerEvents.InvokePlayerStateRealTimeUpdated(playerId, newPlayerState);
                        anythingChanged = true; // Consider this a general lobby update too
                    }
                }
                else
                {
                    // New player state added
                    if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Player {playerId} real-time state ADDED.");
                    playerEvents.InvokePlayerStateRealTimeUpdated(playerId, newPlayerState);
                    anythingChanged = true; // Consider this a general lobby update too
                }
            }
            // Check for removed player states (if a player who had state is no longer in newRealTimeStates but still in lobby?)
            // This case is less common as player leaving the lobby should clear their state or be handled by player left events.
            // For simplicity, we are currently focusing on updates and additions to lobbyStateRealTime.

            // 7) If anything overall changed, fire general update events.
            if (anythingChanged)
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] Overall changes detected for lobby {newLobby.id} - Host: {hostChanged}, Status: {statusChanged}, Settings: {settingsDictionaryChanged}, GameServer: {gameServerDictionaryChanged}, RealTimeStateChanged: {anythingChanged && !hostChanged && !statusChanged && !settingsDictionaryChanged && !gameServerDictionaryChanged}. Firing general update events.");
                lobbyListEvents.InvokeLobbyModified(newLobby); 
                if (wasOurCurrentLobbyUpdate)
                {
                    individualLobbyEvents.InvokeLobbyUpdated(newLobby); 
                }
            }
            else
            {
                if (debugLogging) Debug.Log($"[CompareAndFireLobbyEvents] No effective changes detected for lobby {newLobby.id} after detailed comparison.");
            }
        }
    }
} 