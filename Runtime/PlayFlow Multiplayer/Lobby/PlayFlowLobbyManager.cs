using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Events;
using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFlow;
using System.Threading;

namespace PlayFlow
{
    public class PlayFlowLobbyManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string baseUrl = "https://backend.computeflow.cloud";
        [SerializeField] private string apiKey;
        [SerializeField] private string lobbyConfigName = "firstLobby";
        [SerializeField, Tooltip("Minimum refresh interval is 3 seconds")] private float refreshInterval = 3f;
        [SerializeField] private bool autoRefresh = true;
        [SerializeField] private bool debugLogging = false;

        // Public property to access debugLogging
        public bool DebugLogging => debugLogging;

        // Public property to access and set refresh interval with minimum enforcement
        public float RefreshInterval
        {
            get => refreshInterval;
            set => refreshInterval = Mathf.Max(3f, value);
        }

        // Component references
        private LobbyClient lobbyClient;
        private PlayFlowLobbyActions lobbyActions;
        private PlayFlowLobbyComparer lobbyComparer;
        private PlayFlowLobbyRefresher lobbyRefresher;
        private PlayFlowGameServerUtility gameServerUtility;
        
        // Event components
        private PlayFlowLobbyEvents.EventLogger eventLogger;

        [Header("Events")]
        public PlayFlowLobbyEvents.LobbyListEvents lobbyListEvents = new PlayFlowLobbyEvents.LobbyListEvents();
        public PlayFlowLobbyEvents.IndividualLobbyEvents individualLobbyEvents = new PlayFlowLobbyEvents.IndividualLobbyEvents();
        public PlayFlowLobbyEvents.PlayerEvents playerEvents = new PlayFlowLobbyEvents.PlayerEvents();
        public PlayFlowLobbyEvents.MatchEvents matchEvents = new PlayFlowLobbyEvents.MatchEvents();
        public PlayFlowLobbyEvents.SystemEvents systemEvents = new PlayFlowLobbyEvents.SystemEvents();

        // Lobby creation settings
        private string playerId = "testPlayerId";
        private string newLobbyName = "New Lobby";
        private int maxPlayers = 4;
        private bool isPrivate = false;
        private Dictionary<string, object> lobbySettings = new Dictionary<string, object>();
        private bool newLobbyAllowLateJoin = true;
        private string newLobbyRegion = "us-east";
        
        // Tracking variables
        private float lastRefreshTime;

        private void Awake()
        {
            lobbyClient = new LobbyClient(baseUrl, apiKey);

            // Initialize event loggers
            eventLogger = new PlayFlowLobbyEvents.EventLogger();
            eventLogger.Initialize(debugLogging);
            
            lobbyListEvents.Initialize(eventLogger);
            individualLobbyEvents.Initialize(eventLogger);
            playerEvents.Initialize(eventLogger);
            matchEvents.Initialize(eventLogger);
            systemEvents.Initialize(eventLogger);
            
            // Initialize components
            lobbyActions = new PlayFlowLobbyActions(
                lobbyClient,
                lobbyConfigName,
                playerId,
                this,
                systemEvents,
                individualLobbyEvents,
                (msg) => { if (debugLogging) Debug.Log(msg); },
                Debug.LogError
            );
            
            lobbyComparer = new PlayFlowLobbyComparer(
                lobbyListEvents,
                individualLobbyEvents,
                playerEvents,
                matchEvents,
                debugLogging
            );
            
            lobbyRefresher = new PlayFlowLobbyRefresher(
                lobbyActions,
                lobbyComparer,
                lobbyListEvents,
                debugLogging
            );
            
            gameServerUtility = new PlayFlowGameServerUtility(
                individualLobbyEvents,
                debugLogging
            );
        }

        private void Start()
        {
            _ = RefreshLobbiesAsync(); // Fire and forget for initial load
        }

        private void Update()
        {
            if (!autoRefresh || Time.time - lastRefreshTime < refreshInterval) return;
            
            lastRefreshTime = Time.time;
            
            if (IsInLobby())
            {
                _ = RefreshCurrentLobbyAsync();
            }
            else
            {
                _ = RefreshLobbiesAsync();
            }
        }

        // ---------------------------------------------------------
        //  REFRESH METHODS
        // ---------------------------------------------------------
        private async Task RefreshCurrentLobbyAsync()
        {
            if (!IsInLobby() || string.IsNullOrEmpty(GetCurrentLobbyId())) return;
            
            try { await lobbyRefresher.RefreshCurrentLobbyAsync(GetCurrentLobbyId()); }
            catch (Exception e) { if (debugLogging) Debug.LogError($"Error in RefreshCurrentLobbyAsync: {e.Message}"); }
        }

        public async Task RefreshLobbiesAsync()
        {
            try { await lobbyRefresher.RefreshLobbiesAsync(); }
            catch (Exception e) { 
                if (debugLogging) Debug.LogError($"Error in RefreshLobbiesAsync: {e.Message}");
                systemEvents.InvokeError($"Failed to refresh lobbies: {e.Message}"); // Keep system event for broader errors
                throw; // Re-throw for caller to handle
            }
        }

        // ---------------------------------------------------------
        //  CREATING / JOINING / LEAVING / UPDATING
        // ---------------------------------------------------------
        public async Task<Lobby> CreateLobbyAsync()
        {
            // Calls the new overload with null, which will use the internal lobbySettings
            return await CreateLobbyAsync(null);
        }

        /// <summary>
        /// Creates a new lobby with the specified or default settings.
        /// </summary>
        /// <param name="customLobbySettings">Optional. Custom settings for the lobby. If null, uses the manager's internal lobbySettings.</param>
        /// <returns>The created lobby.</returns>
        public async Task<Lobby> CreateLobbyAsync(Dictionary<string, object> customLobbySettings = null)
        {
            try
            {
                // Use provided customLobbySettings if not null, otherwise use the internal lobbySettings field
                Dictionary<string, object> settingsToUse = customLobbySettings ?? this.lobbySettings;

                var createdLobby = await lobbyActions.CreateLobbyAsync(
                    newLobbyName,
                    maxPlayers,
                    isPrivate,
                    newLobbyAllowLateJoin,
                    newLobbyRegion,
                    settingsToUse // Use the determined settings
                );

                // We'll compare "null" with the new lobby, so it will fire the "joined" event, etc.
                lobbyComparer.CompareAndFireLobbyEvents(null, createdLobby);
                
                individualLobbyEvents.InvokeLobbyCreated(createdLobby);

                _ = RefreshLobbiesAsync();
                return createdLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create lobby: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        public async Task<Lobby> JoinLobbyAsync(string lobbyId)
        {
            try
            {
                var joinedLobby = await lobbyActions.JoinLobbyAsync(lobbyId);

                if (debugLogging) Debug.Log($"Joined lobby: {joinedLobby.name}");

                lobbyComparer.CompareAndFireLobbyEvents(null, joinedLobby);   // "null" → new lobby

                _ = RefreshLobbiesAsync();
                return joinedLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to join lobby: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        public async Task LeaveLobbyAsync()
        {
            if (!IsInLobby() || string.IsNullOrEmpty(GetCurrentLobbyId())) throw new InvalidOperationException("Not in a lobby or lobby ID is invalid.");

            try
            {
                string lobbyIdToLeave = GetCurrentLobbyId();
                Lobby oldLobby = lobbyComparer.GetCurrentLobby()?.Clone(); // Clone for accurate event firing after state reset

                await lobbyActions.LeaveLobbyAsync(lobbyIdToLeave);
                if (debugLogging) Debug.Log("Left lobby successfully via actions.");

                lobbyComparer.CompareAndFireLobbyEvents(oldLobby, null); // Pass the cloned oldLobby
                lobbyComparer.ResetCurrentLobbyStatus(); // Reset local state after firing events based on old state
                individualLobbyEvents.InvokeLobbyLeft(); // Explicitly ensure this fires if CompareAndFire didn't handle it due to prior state reset
                
                _ = RefreshLobbiesAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to leave lobby: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        public async Task<Lobby> JoinLobbyByCodeAsync(string code)
        {
            try
            {
                var joinedLobby = await lobbyActions.JoinLobbyByCodeAsync(code);

                if (debugLogging) Debug.Log($"Joined lobby by code: {joinedLobby.name}");

                lobbyComparer.CompareAndFireLobbyEvents(null, joinedLobby); // "null" → new lobby

                _ = RefreshLobbiesAsync();
                return joinedLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to join lobby by code: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        public async Task<Lobby> KickPlayerAsync(string playerToKick)
        {
            if (!IsInLobby() || string.IsNullOrEmpty(GetCurrentLobbyId())) throw new InvalidOperationException("Not in a lobby or lobby ID is invalid.");

            try
            {
                var updatedLobby = await lobbyActions.KickPlayerAsync(GetCurrentLobbyId(), playerToKick);

                if (debugLogging) Debug.Log($"Kicked player: {playerToKick}");

                lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), updatedLobby);

                _ = RefreshLobbiesAsync();
                return updatedLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to kick player: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        public async Task<Lobby> UpdateAllLobbySettingsAsync(
            Dictionary<string, object> newSettings = null,
            string newName = null,
            int? newMaxPlayers = null,
            bool? newIsPrivate = null,
            bool? newAllowLateJoin = null)
        {
            if (!IsInLobby() || string.IsNullOrEmpty(GetCurrentLobbyId())) throw new InvalidOperationException("Not in a lobby or lobby ID is invalid.");
            if (!IsHost())
            {
                systemEvents.InvokeError("Only the host can update lobby settings");
                throw new InvalidOperationException("Only the host can update lobby settings.");
            }

            try
            {
                var updatedLobby = await lobbyActions.UpdateAllLobbySettingsAsync(
                    GetCurrentLobbyId(),
                    newSettings,
                    newName,
                    newMaxPlayers,
                    newIsPrivate,
                    newAllowLateJoin);

                lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), updatedLobby);
                return updatedLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to update lobby settings: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        public async Task<Lobby> UpdateLobbySettingsAsync()
        {
            if (!IsInLobby() || string.IsNullOrEmpty(GetCurrentLobbyId())) throw new InvalidOperationException("Not in a lobby or lobby ID is invalid.");
            if (!IsHost())
            {
                systemEvents.InvokeError("Only the host can update lobby settings");
                throw new InvalidOperationException("Only the host can update lobby settings.");
            }

            try
            {
                var updatedLobby = await lobbyActions.UpdateLobbySettingsAsync(GetCurrentLobbyId(), lobbyComparer.GetLobbySettings());
                
                lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), updatedLobby);
                return updatedLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to update lobby settings: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        // ---------------------------------------------------------
        //  MATCH ACTIONS
        // ---------------------------------------------------------
        public async Task<Lobby> StartMatchAsync()
        {
            if (!IsInLobby() || string.IsNullOrEmpty(GetCurrentLobbyId())) throw new InvalidOperationException("Not in a lobby or lobby ID is invalid.");
            if (!IsHost())
            {
                systemEvents.InvokeError("Only the host can start the match");
                throw new InvalidOperationException("Only the host can start the match.");
            }

            try
            {
                var updatedLobby = await lobbyActions.StartMatchAsync(GetCurrentLobbyId());
                
                lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), updatedLobby);

                _ = RefreshLobbiesAsync();
                return updatedLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start game: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        public async Task<Lobby> EndGameAsync()
        {
            if (!IsInLobby() || string.IsNullOrEmpty(GetCurrentLobbyId())) throw new InvalidOperationException("Not in a lobby or lobby ID is invalid.");
            if (!IsHost())
            {
                systemEvents.InvokeError("Only the host can end the match");
                throw new InvalidOperationException("Only the host can end the match.");
            }

            try
            {
                var updatedLobby = await lobbyActions.EndGameAsync(GetCurrentLobbyId());
                
                lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), updatedLobby);

                _ = RefreshLobbiesAsync();
                return updatedLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to end game: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        public async Task<Lobby> TransferHostAsync(string newHostId)
        {
            if (!IsInLobby() || string.IsNullOrEmpty(GetCurrentLobbyId())) throw new InvalidOperationException("Not in a lobby or lobby ID is invalid.");
            
            try
            {
                var updatedLobby = await lobbyActions.TransferHostAsync(GetCurrentLobbyId(), newHostId);
                
                lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), updatedLobby);

                _ = RefreshLobbiesAsync();
                return updatedLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to transfer host: {e.Message}");
                // Error event is already fired in the lobbyActions method
                throw;
            }
        }

        // ---------------------------------------------------------
        //  CHECKING EXISTING LOBBY AT STARTUP
        // ---------------------------------------------------------
        /// <summary>
        /// Manually check if the player is already in a lobby and join it if they are.
        /// Use this if you want to explicitly check, for example, at game startup.
        /// </summary>
        public async Task<bool> CheckAndJoinExistingLobbyAsync()
        {
            try
            {
                JObject playerLobbyJObject = await lobbyActions.GetLobbyAsync(playerId);
                
                if (playerLobbyJObject != null)
                {
                    var playerLobby = playerLobbyJObject.ToObject<Lobby>(Newtonsoft.Json.JsonSerializer.CreateDefault());
                    if (playerLobby != null) 
                    {                       
                        // If we already are in a lobby, compare "null" to force the "joined" events
                        lobbyComparer.CompareAndFireLobbyEvents(null, playerLobby);

                        // If it's in match state, you can do your custom logic
                        if (playerLobby.status == "in_game") // Changed from "match" to "in_game" to align with typical status values
                        {
                            if (debugLogging) Debug.Log("Joined an ongoing match");
                        }
                        else
                        {
                            if (debugLogging) Debug.Log($"Joined lobby in {playerLobby.status} state");
                        }
                        return true;
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                // LobbyClient throws an exception on HTTP error status (e.g. 404 Not Found)
                // Check if the error message indicates a "not found" type of error for the player/lobby.
                if (e.Message.Contains("404") || e.Message.ToLower().Contains("not found")) 
                {
                    // This is an expected case if the player is not in a lobby, so don't log as error.
                    if (debugLogging) Debug.Log($"Player {playerId} is not currently in a lobby.");
                }
                else
                {
                    Debug.LogError($"Error checking existing lobby: {e.Message}");
                }
                lobbyComparer.ResetCurrentLobbyStatus();
                return false;
            }
        }

        // ---------------------------------------------------------
        //  PUBLIC GETTERS & SETTERS
        // ---------------------------------------------------------
        public Lobby GetCurrentLobby() => lobbyComparer.GetCurrentLobby();
        public List<Lobby> GetAvailableLobbies() => lobbyRefresher.GetAvailableLobbies();
        public bool IsInLobby() => lobbyComparer.GetCurrentLobby() != null;
        public string GetPlayerId() => playerId;
        public string GetCurrentLobbyId() => lobbyComparer.GetCurrentLobby()?.id;
        public string GetCurrentLobbyName() => lobbyComparer.GetCurrentLobby()?.name;
        public int GetCurrentPlayerCount() => lobbyComparer.GetCurrentLobby()?.currentPlayers ?? 0;
        public string GetCurrentLobbyStatus() => lobbyComparer.GetCurrentLobby()?.status;
        public bool IsHost() => lobbyComparer.GetCurrentLobby() != null && lobbyComparer.GetCurrentLobby().host == playerId;
        public string[] GetCurrentPlayers() => lobbyComparer.GetCurrentLobby()?.players;
        public Dictionary<string, object> GetGameServerInfo() => lobbyComparer.GetCurrentLobby()?.gameServer as Dictionary<string, object>;
        public string GetInviteCode() => lobbyComparer.GetCurrentLobby()?.inviteCode;

        /// <summary>
        /// Sends a real-time state update for the current player to the lobby.
        /// The state can include any JSON-serializable data like position, health, score, etc.
        /// </summary>
        /// <param name="state">Dictionary containing the player's state data</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        public async Task<bool> SendPlayerStateUpdateAsync(Dictionary<string, object> state)
        {
            if (!IsInLobby() || string.IsNullOrEmpty(GetCurrentLobbyId()))
            {
                systemEvents.InvokeError("Cannot update player state: Not in a lobby");
                return false;
            }

            if (state == null)
            {
                systemEvents.InvokeError("Cannot update player state: State is null");
                return false;
            }

            try
            {
                var updatedLobby = await lobbyActions.SendPlayerStateUpdateAsync(GetCurrentLobbyId(), state);
                
                lobbyComparer.CompareAndFireLobbyEvents(lobbyComparer.GetCurrentLobby(), updatedLobby);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to update player state: {e.Message}");
                // Error event is already fired in the lobbyActions method
                return false;
            }
        }

        public bool GetAutoRefresh() => autoRefresh;
        public void SetAutoRefresh(bool value) => autoRefresh = value;

        public void SetPlayerInfo(string id)
        {
            playerId = id;
            // Update the player ID in the lobbyActions as well
            if (lobbyActions != null)
            {
                lobbyActions.SetPlayerId(id);
            }
        }

        public void SetLobbyCreationSettings(string name, int maxPlayerCount, bool makePrivate, bool allowLateJoin = true, string region = "us-east")
        {
            newLobbyName = name;
            maxPlayers = maxPlayerCount;
            isPrivate = makePrivate;
            newLobbyAllowLateJoin = allowLateJoin;
            newLobbyRegion = region;
        }

        public Dictionary<string, object> GetLobbySettings() => lobbyComparer.GetLobbySettings();
        public void AddLobbySetting(string key, object value)
        {
            var settings = lobbyComparer.GetLobbySettings();
            settings[key] = value;
            if (IsInLobby() && IsHost())
            {
                _ = UpdateLobbySettingsAsync();
            }
        }
        public void RemoveLobbySetting(string key)
        {
            var settings = lobbyComparer.GetLobbySettings();
            if (settings.Remove(key) && IsInLobby() && IsHost())
            {
                _ = UpdateLobbySettingsAsync();
            }
        }
        public void ClearLobbySettings()
        {
            var settings = lobbyComparer.GetLobbySettings();
            settings.Clear();
            if (IsInLobby() && IsHost())
            {
                _ = UpdateLobbySettingsAsync();
            }
        }

        /// <summary>
        /// Asynchronously waits until the game server for the current lobby reports its status as "running".
        /// </summary>
        /// <param name="timeoutSeconds">Maximum time to wait in seconds.</param>
        /// <returns>True if the game server becomes "running" within the timeout, false otherwise.</returns>
        public async Task<bool> WaitForGameServerRunningAsync(float timeoutSeconds = 30f)
        {
            if (!IsInLobby())
            {
                if (debugLogging) Debug.LogWarning("[WaitForGameServerRunningAsync] Not in a lobby. Cannot wait for game server.");
                return false;
            }

            return await gameServerUtility.WaitForGameServerRunningAsync(GetCurrentLobby(), timeoutSeconds);
        }

        // Method to print game server details, similar to MatchmakerHelloWorld.cs
        public void PrintGameServerDetails()
        {
            if (!IsInLobby())
            {
                if (debugLogging) Debug.Log("[PrintGameServerDetails] Not currently in a lobby.");
                return;
            }

            gameServerUtility.PrintGameServerDetails(GetCurrentLobby());
        }

        /// <summary>
        /// Retrieves specific network port details (host, external port) for the current lobby's game server.
        /// </summary>
        /// <param name="internalPort">The internal port number to search for.</param>
        /// <param name="protocol">Optional: The protocol (e.g., "udp", "tcp") to match. Case-insensitive.</param>
        /// <param name="portName">Optional: The specific name of the port (e.g., "game_udp") to match. Case-insensitive.</param>
        /// <returns>A NetworkPortDetails struct if a match is found; otherwise, null.</returns>
        public NetworkPortDetails? GetGameServerPortInfo(int internalPort, string protocol = null, string portName = null)
        {
            Lobby currentLobby = GetCurrentLobby();
            if (currentLobby == null)
            {
                if (debugLogging) Debug.LogWarning("[GetGameServerPortInfo] Cannot get port info: Not in a lobby.");
                return null;
            }
            if (gameServerUtility == null)
            {
                 if (debugLogging) Debug.LogError("[GetGameServerPortInfo] gameServerUtility is null. This should not happen.");
                return null;
            }
            return gameServerUtility.GetNetworkPort(currentLobby, internalPort, protocol, portName);
        }
    }
}

