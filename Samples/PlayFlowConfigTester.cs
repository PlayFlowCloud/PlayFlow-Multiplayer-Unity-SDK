using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PlayFlow
{
    public class PlayFlowConfigTester : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Set to true to load from Resources folder, false to load from project root")]
        public bool useLocalConfig = true;
        
        [Header("Loaded Config Data")]
        [Space(10)]
        [SerializeField] private string instanceId;
        [SerializeField] private string region;
        [SerializeField] private string apiKey;
        [SerializeField] private string versionTag;
        [SerializeField] private string matchId;
        
        [Header("Matchmaking Data")]
        [Space(10)]
        [SerializeField] private string matchmakingMode;
        [SerializeField] private int teamsCount;
        [SerializeField] private List<string> allPlayers = new List<string>();
        
        [Header("Team Details")]
        [Space(10)]
        [SerializeField] private List<TeamInfo> teams = new List<TeamInfo>();
        
        [Header("Match Configuration")]
        [Space(10)]
        [SerializeField] private int playersPerTeam;
        [SerializeField] private int minPlayersPerTeam;
        [SerializeField] private int timeout;
        
        [Header("Lobby Settings")]
        [Space(10)]
        [SerializeField] private string gameMode;
        [SerializeField] private string matchType;
        
        [System.Serializable]
        public class TeamInfo
        {
            public int teamId;
            public string lobbyId;
            public string lobbyName;
            public string hostPlayerId;
            public List<PlayerInfo> players = new List<PlayerInfo>();
        }
        
        [System.Serializable]
        public class PlayerInfo
        {
            public string playerId;
            public string playerName;
            public int mmr;
            public bool ready;
        }
        
        void Start()
        {
            LoadAndDisplayConfig();
        }
        
        [ContextMenu("Reload Config")]
        public void LoadAndDisplayConfig()
        {
            var config = PlayFlowServerConfig.LoadConfig(useLocalConfig);
            
            if (config == null)
            {
                Debug.LogError("[PlayFlowConfigTester] Failed to load config!");
                return;
            }
            
            Debug.Log("====================================");
            Debug.Log("PLAYFLOW CONFIG LOADED SUCCESSFULLY");
            Debug.Log("====================================");
            
            // Basic config data
            instanceId = config.instance_id;
            region = config.region;
            apiKey = config.api_key;
            versionTag = config.version_tag;
            matchId = config.match_id;
            
            Debug.Log($"Instance ID: {instanceId}");
            Debug.Log($"Region: {region}");
            Debug.Log($"API Key: {apiKey?.Substring(0, Mathf.Min(10, apiKey.Length))}... (truncated)");
            Debug.Log($"Version Tag: {versionTag}");
            Debug.Log($"Match ID: {matchId}");
            
            // Check if we have custom data
            if (config.custom_data_json == null)
            {
                Debug.Log("No custom_data found in config");
                return;
            }
            
            Debug.Log("\n--- CUSTOM DATA ---");
            
            // Matchmaking mode
            matchmakingMode = config.GetMatchmakingMode();
            if (!string.IsNullOrEmpty(matchmakingMode))
            {
                Debug.Log($"Matchmaking Mode: {matchmakingMode}");
            }
            
            // All players
            allPlayers = config.GetAllPlayers();
            if (allPlayers.Count > 0)
            {
                Debug.Log($"Total Players: {allPlayers.Count}");
                foreach (var player in allPlayers)
                {
                    Debug.Log($"  - {player}");
                }
            }
            
            // Teams
            teams.Clear();
            var teamsArray = config.GetTeams();
            if (teamsArray != null)
            {
                teamsCount = teamsArray.Count;
                Debug.Log($"\nTeams Count: {teamsCount}");
                
                foreach (JObject teamObj in teamsArray)
                {
                    var teamInfo = new TeamInfo();
                    teamInfo.teamId = teamObj["team_id"]?.Value<int>() ?? 0;
                    
                    Debug.Log($"\n  Team {teamInfo.teamId}:");
                    
                    var lobbies = teamObj["lobbies"] as JArray;
                    if (lobbies != null && lobbies.Count > 0)
                    {
                        var lobby = lobbies[0] as JObject;
                        teamInfo.lobbyId = lobby["lobby_id"]?.ToString();
                        teamInfo.lobbyName = lobby["lobby_name"]?.ToString();
                        teamInfo.hostPlayerId = lobby["host"]?.ToString();
                        
                        Debug.Log($"    Lobby ID: {teamInfo.lobbyId}");
                        Debug.Log($"    Lobby Name: {teamInfo.lobbyName}");
                        Debug.Log($"    Host: {teamInfo.hostPlayerId}");
                        
                        // Player states
                        var playerStates = lobby["player_states"] as JObject;
                        if (playerStates != null)
                        {
                            Debug.Log($"    Players:");
                            foreach (var kvp in playerStates)
                            {
                                var playerInfo = new PlayerInfo();
                                playerInfo.playerId = kvp.Key;
                                
                                var playerData = kvp.Value as JObject;
                                playerInfo.playerName = playerData["playerName"]?.ToString();
                                playerInfo.mmr = playerData["mmr"]?.Value<int>() ?? 0;
                                playerInfo.ready = playerData["ready"]?.Value<bool>() ?? false;
                                
                                teamInfo.players.Add(playerInfo);
                                
                                Debug.Log($"      - {playerInfo.playerId}:");
                                Debug.Log($"          Name: {playerInfo.playerName}");
                                Debug.Log($"          MMR: {playerInfo.mmr}");
                                Debug.Log($"          Ready: {playerInfo.ready}");
                            }
                        }
                    }
                    
                    teams.Add(teamInfo);
                }
            }
            
            // Match configuration
            var matchConfig = config.GetMatchConfiguration();
            if (matchConfig != null)
            {
                playersPerTeam = matchConfig["players_per_team"]?.Value<int>() ?? 0;
                minPlayersPerTeam = matchConfig["min_players_per_team"]?.Value<int>() ?? 0;
                timeout = matchConfig["timeout"]?.Value<int>() ?? 0;
                
                Debug.Log($"\nMatch Configuration:");
                Debug.Log($"  Players per team: {playersPerTeam}");
                Debug.Log($"  Min players per team: {minPlayersPerTeam}");
                Debug.Log($"  Timeout: {timeout} seconds");
            }
            
            // Lobby settings
            var lobbySettings = config.GetLobbySettings();
            if (lobbySettings != null)
            {
                gameMode = lobbySettings["gameMode"]?.ToString();
                matchType = lobbySettings["matchType"]?.ToString();
                
                Debug.Log($"\nLobby Settings:");
                Debug.Log($"  Game Mode: {gameMode}");
                Debug.Log($"  Match Type: {matchType}");
                
                // Print any additional settings
                foreach (var kvp in lobbySettings)
                {
                    if (kvp.Key != "gameMode" && kvp.Key != "matchType")
                    {
                        Debug.Log($"  {kvp.Key}: {kvp.Value}");
                    }
                }
            }
            
            // Print the full JSON for debugging
            Debug.Log($"\n--- FULL CUSTOM DATA JSON ---");
            Debug.Log(config.custom_data_json?.ToString(Newtonsoft.Json.Formatting.Indented));
            
            Debug.Log("\n====================================");
            Debug.Log("END OF CONFIG");
            Debug.Log("====================================");
        }
        
        void OnValidate()
        {
            // Reload config when useLocalConfig changes in the inspector
            if (Application.isPlaying)
            {
                LoadAndDisplayConfig();
            }
        }
    }
}