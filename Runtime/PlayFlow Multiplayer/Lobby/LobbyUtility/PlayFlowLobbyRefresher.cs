using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PlayFlow
{
    public class PlayFlowLobbyRefresher
    {
        private readonly PlayFlowLobbyActions lobbyActions;
        private readonly PlayFlowLobbyComparer lobbyComparer;
        private readonly PlayFlowLobbyEvents.LobbyListEvents lobbyListEvents;
        private readonly bool debugLogging;
        
        private List<Lobby> availableLobbies = new List<Lobby>();
        private readonly object lobbyListLock = new object(); // Add lock for thread safety
        
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
        
        public List<Lobby> GetAvailableLobbies() 
        {
            lock (lobbyListLock)
            {
                return new List<Lobby>(availableLobbies); // Return a copy to prevent modification
            }
        }

        public IEnumerator RefreshCurrentLobbyCoroutine(string currentLobbyId)
        {
            JObject lobbyJObject = null;
            bool is404Error = false;
            
            yield return lobbyActions.GetLobbyCoroutine(currentLobbyId, response => lobbyJObject = response, error =>
            {
                // Check if this is a 404 error (lobby no longer exists)
                if (error.Message.Contains("404") || error.Message.Contains("Not Found"))
                {
                    is404Error = true;
                    if (debugLogging) Debug.Log($"Lobby {currentLobbyId} no longer exists (404). Clearing current lobby state.");
                    // Clear the current lobby state since it no longer exists
                    lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), null);
                }
                else
                {
                    // Only log as error if it's not a 404
                    if (debugLogging) Debug.LogError($"Error refreshing current lobby: {error.Message}");
                }
                lobbyComparer.ResetCurrentLobbyStatus();
            });

            if (lobbyJObject != null && !is404Error)
            {
                var newLobby = lobbyJObject.ToObject<Lobby>();
                lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), newLobby);
            }
        }

        public IEnumerator RefreshLobbiesCoroutine()
            {
            JArray newLobbiesJArray = null;
            yield return lobbyActions.ListLobbiesCoroutine(response => newLobbiesJArray = response, error =>
            {
                if (debugLogging) Debug.LogError($"Error refreshing lobby list: {error.Message}");
            });

            if (newLobbiesJArray != null)
                {
                var newLobbies = newLobbiesJArray.ToObject<List<Lobby>>();
                List<Lobby> oldLobbies = null;
                
                lock (lobbyListLock)
                {
                    oldLobbies = new List<Lobby>(availableLobbies);
                availableLobbies = newLobbies;
                }
                
                lobbyListEvents?.InvokeLobbiesRefreshed(newLobbies);
                
                // Compare old and new lists to find added/removed/modified lobbies
                var addedLobbies = newLobbies.Where(n => oldLobbies.All(o => o.id != n.id)).ToList();
                var removedLobbies = oldLobbies.Where(o => newLobbies.All(n => n.id != o.id)).ToList();

                foreach (var lobby in addedLobbies) lobbyListEvents?.InvokeLobbyAdded(lobby);
                foreach (var lobby in removedLobbies) lobbyListEvents?.InvokeLobbyRemoved(lobby);
            }
        }
    }
} 