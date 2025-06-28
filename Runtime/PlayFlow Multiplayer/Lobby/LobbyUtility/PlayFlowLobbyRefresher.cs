using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayFlow
{
    public class PlayFlowLobbyRefresher
    {
        private readonly PlayFlowLobbyActions lobbyActions;
        private readonly PlayFlowLobbyComparer lobbyComparer;
        private readonly PlayFlowLobbyEvents.LobbyListEvents lobbyListEvents;
        private readonly bool debugLogging;
        
        private List<Lobby> availableLobbies = new List<Lobby>();
        
        public PlayFlowLobbyRefresher(
            PlayFlowLobbyActions lobbyActions,
            PlayFlowLobbyComparer lobbyComparer,
            PlayFlowLobbyEvents.LobbyListEvents lobbyListEvents,
            bool debugLogging)
        {
            this.lobbyActions = lobbyActions;
            this.lobbyComparer = lobbyComparer;
            this.lobbyListEvents = lobbyListEvents;
            this.debugLogging = debugLogging;
        }
        
        public List<Lobby> GetAvailableLobbies() => availableLobbies;

        public async Task RefreshCurrentLobbyAsync(string currentLobbyId)
        {
            if (string.IsNullOrEmpty(currentLobbyId)) return;

            try
            {
                JObject lobbyJObject = await lobbyActions.GetLobbyAsync(currentLobbyId);
                if (lobbyJObject != null)
                {
                    if (debugLogging) 
                    {
                        Debug.Log($"[RefreshCurrentLobby] Fetched lobby JObject: {lobbyJObject.ToString(Newtonsoft.Json.Formatting.None)}");
                    }
                    var updatedLobby = lobbyJObject.ToObject<Lobby>(JsonSerializer.CreateDefault());
                    if (updatedLobby != null)
                    {
                        if (debugLogging && updatedLobby.gameServer != null && updatedLobby.gameServer.TryGetValue("status", out var statusVal))
                        {
                            Debug.Log($"[RefreshCurrentLobby] Deserialized updatedLobby.gameServer.status: {statusVal?.ToString()}");
                        }
                        lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), updatedLobby);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to refresh current lobby: {e.Message}");
                // Error event is already fired in the lobbyActions method
            }
        }

        public async Task RefreshLobbiesAsync()
        {
            try
            {
                JArray lobbiesJArray = await lobbyActions.ListLobbiesAsync();
                List<Lobby> newLobbies = new List<Lobby>();
                if (lobbiesJArray != null)
                {
                    newLobbies = lobbiesJArray.ToObject<List<Lobby>>(JsonSerializer.CreateDefault());
                }

                // 1) Identify removed lobbies
                var currentLobbyIds = availableLobbies.Select(l => l.id).ToHashSet();
                var newLobbyIds = newLobbies.Select(l => l.id).ToHashSet();
                
                foreach (var oldLobby in availableLobbies)
                {
                    if (!newLobbyIds.Contains(oldLobby.id))
                    {
                        // Lobby removed
                        lobbyListEvents.InvokeLobbyRemoved(oldLobby);
                    }
                }

                // 2) Identify added or modified lobbies
                foreach (var newLobby in newLobbies)
                {
                    var existingLobby = availableLobbies.FirstOrDefault(l => l.id == newLobby.id);
                    if (existingLobby == null)
                    {
                        // Lobby added
                        lobbyListEvents.InvokeLobbyAdded(newLobby);
                    }
                    else
                    {
                        // Lobby might be modified
                        lobbyComparer.CompareAndFireLobbyEvents(existingLobby, newLobby);
                    }
                }

                // 3) Overwrite internal list after firing events
                availableLobbies = newLobbies;

                // 4) If we're currently in a lobby, check for updates on it
                var currentLobby = lobbyComparer.GetCurrentLobby();
                if (currentLobby != null && !string.IsNullOrEmpty(currentLobby.id))
                {
                    var updatedLobby = availableLobbies.Find(l => l.id == currentLobby.id);
                    if (updatedLobby != null)
                    {
                        lobbyComparer.CompareAndFireLobbyEvents(currentLobby, updatedLobby);
                    }
                }

                // 5) Fire the onLobbiesRefreshed event with the new list
                lobbyListEvents.InvokeLobbiesRefreshed(newLobbies);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to refresh lobbies: {e.Message}");
                // Error event is already fired in the lobbyActions method
            }
        }
    }
} 