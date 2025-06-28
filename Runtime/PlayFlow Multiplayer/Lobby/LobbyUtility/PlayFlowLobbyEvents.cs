using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;

namespace PlayFlow
{
    public class PlayFlowLobbyEvents
    {
        [System.Serializable]
        public class EventLogger
        {
            private bool debugLogging;
            
            public void Initialize(bool debugLogging)
            {
                this.debugLogging = debugLogging;
            }
            
            public void LogEvent(string eventName, object data = null)
            {
                if (!debugLogging) return;
                
                if (data == null)
                {
                    Debug.Log($"[PlayFlow Event] {eventName}");
                    return;
                }

                string dataStr = data switch
                {
                    string s => s,
                    Lobby l => $"Lobby[{l.name}({l.id}), Players: {l.currentPlayers}/{l.maxPlayers}, Status: {l.status}]",
                    List<Lobby> lobbies => $"Lobbies[Count: {lobbies.Count}]",
                    Dictionary<string, object> dict => JsonConvert.SerializeObject(dict, Formatting.None),
                    _ => data.ToString()
                };
                
                Debug.Log($"[PlayFlow Event] {eventName} - Data: {dataStr}");
            }
        }

        [System.Serializable]
        public class LobbyListEvents
        {
            private EventLogger logger;
            
            [Header("Lobby List Events")]
            [Tooltip("Fired when the list of available lobbies is refreshed")]
            public UnityEvent<List<Lobby>> onLobbiesRefreshed = new UnityEvent<List<Lobby>>();
            [Tooltip("Fired when a new lobby is added to the list")]
            public UnityEvent<Lobby> onLobbyAdded = new UnityEvent<Lobby>();
            [Tooltip("Fired when a lobby is removed from the list")]
            public UnityEvent<Lobby> onLobbyRemoved = new UnityEvent<Lobby>();
            [Tooltip("Fired when a lobby in the list is modified")]
            public UnityEvent<Lobby> onLobbyModified = new UnityEvent<Lobby>();

            public void Initialize(EventLogger logger)
            {
                this.logger = logger;
            }

            public void InvokeLobbiesRefreshed(List<Lobby> lobbies)
            {
                logger.LogEvent("onLobbiesRefreshed", lobbies);
                onLobbiesRefreshed.Invoke(lobbies);
            }

            public void InvokeLobbyAdded(Lobby lobby)
            {
                logger.LogEvent("onLobbyAdded", lobby);
                onLobbyAdded.Invoke(lobby);
            }

            public void InvokeLobbyRemoved(Lobby lobby)
            {
                logger.LogEvent("onLobbyRemoved", lobby);
                onLobbyRemoved.Invoke(lobby);
            }

            public void InvokeLobbyModified(Lobby lobby)
            {
                logger.LogEvent("onLobbyModified", lobby);
                onLobbyModified.Invoke(lobby);
            }
        }

        [System.Serializable]
        public class IndividualLobbyEvents
        {
            private EventLogger logger;
            
            [Header("Individual Lobby Events")]
            [Tooltip("Fired when a new lobby is created")]
            public UnityEvent<Lobby> onLobbyCreated = new UnityEvent<Lobby>();
            [Tooltip("Fired when successfully joining a lobby")]
            public UnityEvent<Lobby> onLobbyJoined = new UnityEvent<Lobby>();
            [Tooltip("Fired when leaving a lobby")]
            public UnityEvent onLobbyLeft = new UnityEvent();
            [Tooltip("Fired when the current lobby is updated")]
            public UnityEvent<Lobby> onLobbyUpdated = new UnityEvent<Lobby>();
            [Tooltip("Fired when lobby settings are updated")]
            public UnityEvent<Dictionary<string, object>> onLobbySettingsUpdated = new UnityEvent<Dictionary<string, object>>();

            public void Initialize(EventLogger logger)
            {
                this.logger = logger;
            }

            public void InvokeLobbyCreated(Lobby lobby)
            {
                logger.LogEvent("onLobbyCreated", lobby);
                onLobbyCreated.Invoke(lobby);
            }

            public void InvokeLobbyJoined(Lobby lobby)
            {
                logger.LogEvent("onLobbyJoined", lobby);
                onLobbyJoined.Invoke(lobby);
            }

            public void InvokeLobbyLeft()
            {
                logger.LogEvent("onLobbyLeft");
                onLobbyLeft.Invoke();
            }

            public void InvokeLobbyUpdated(Lobby lobby)
            {
                logger.LogEvent("onLobbyUpdated", lobby);
                onLobbyUpdated.Invoke(lobby);
            }

            public void InvokeLobbySettingsUpdated(Dictionary<string, object> settings)
            {
                logger.LogEvent("onLobbySettingsUpdated", settings);
                onLobbySettingsUpdated.Invoke(settings);
            }
        }

        [System.Serializable]
        public class PlayerEvents
        {
            private EventLogger logger;
            
            [Header("Player Events")]
            [Tooltip("Fired when a new player joins the lobby")]
            public UnityEvent<string> onPlayerJoined = new UnityEvent<string>();
            [Tooltip("Fired when a player leaves the lobby")]
            public UnityEvent<string> onPlayerLeft = new UnityEvent<string>();
            [Tooltip("Fired when host role is transferred to another player")]
            public UnityEvent<string> onHostTransferred = new UnityEvent<string>();
            [Tooltip("Fired when a specific player\'s real-time state data is updated.")]
            public UnityEvent<string, Dictionary<string, object>> onPlayerStateRealTimeUpdated = new UnityEvent<string, Dictionary<string, object>>();

            public void Initialize(EventLogger logger)
            {
                this.logger = logger;
            }

            public void InvokePlayerJoined(string playerId)
            {
                logger.LogEvent("onPlayerJoined", playerId);
                onPlayerJoined.Invoke(playerId);
            }

            public void InvokePlayerLeft(string playerId)
            {
                logger.LogEvent("onPlayerLeft", playerId);
                onPlayerLeft.Invoke(playerId);
            }

            public void InvokeHostTransferred(string newHostId)
            {
                logger.LogEvent("onHostTransferred", newHostId);
                onHostTransferred.Invoke(newHostId);
            }

            public void InvokePlayerStateRealTimeUpdated(string playerId, Dictionary<string, object> newState)
            {
                logger.LogEvent($"onPlayerStateRealTimeUpdated (Player: {playerId})", newState);
                onPlayerStateRealTimeUpdated.Invoke(playerId, newState);
            }
        }

        [System.Serializable]
        public class MatchEvents
        {
            private EventLogger logger;
            
            [Header("Match Events")]
            [Tooltip("Fired when a match starts")]
            public UnityEvent<Lobby> onMatchStarted = new UnityEvent<Lobby>();
            [Tooltip("Fired when a match ends")]
            public UnityEvent<Lobby> onMatchEnded = new UnityEvent<Lobby>();

            public void Initialize(EventLogger logger)
            {
                this.logger = logger;
            }

            public void InvokeMatchStarted(Lobby lobby)
            {
                logger.LogEvent("onMatchStarted", lobby);
                onMatchStarted.Invoke(lobby);
            }

            public void InvokeMatchEnded(Lobby lobby)
            {
                logger.LogEvent("onMatchEnded", lobby);
                onMatchEnded.Invoke(lobby);
            }
        }

        [System.Serializable]
        public class SystemEvents
        {
            private EventLogger logger;
            
            [Header("System Events")]
            [Tooltip("Fired before making an API call")]
            public UnityEvent onPreAPICall = new UnityEvent();
            [Tooltip("Fired after an API call completes")]
            public UnityEvent onPostAPICall = new UnityEvent();
            [Tooltip("Fired when an error occurs")]
            public UnityEvent<string> onError = new UnityEvent<string>();

            public void Initialize(EventLogger logger)
            {
                this.logger = logger;
            }

            public void InvokePreAPICall()
            {
                logger.LogEvent("onPreAPICall");
                onPreAPICall.Invoke();
            }

            public void InvokePostAPICall()
            {
                logger.LogEvent("onPostAPICall");
                onPostAPICall.Invoke();
            }

            public void InvokeError(string error)
            {
                // Always log errors regardless of debug setting
                Debug.LogError($"[PlayFlow Error] {error}");
                logger.LogEvent("onError", error);
                onError.Invoke(error);
            }
        }
    }
} 