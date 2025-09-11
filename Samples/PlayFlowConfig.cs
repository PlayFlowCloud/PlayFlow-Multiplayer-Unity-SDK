using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayFlow
{
    [System.Serializable]
    public class PlayFlowServerConfig
    {
        [JsonProperty("instance_id")]
        public string instance_id { get; set; }
        public string region { get; set; }
        [JsonProperty("api-key")]
        public string api_key { get; set; }
        [JsonProperty("startup_args")]
        public string startup_args { get; set; }
        [JsonProperty("version_tag")]
        public string version_tag { get; set; }
        public string match_id { get; set; }
        public string arguments { get; set; }
        
        // Store as JToken internally for proper deserialization
        [JsonProperty("custom_data")]
        private JToken _customDataRaw { get; set; }
        
        // Backward compatible property that returns Dictionary<string, object>
        [JsonIgnore]
        public Dictionary<string, object> custom_data 
        { 
            get 
            {
                if (_customDataRaw == null) return null;
                
                // For simple objects, convert to dictionary
                if (_customDataRaw is JObject jObj)
                {
                    return jObj.ToObject<Dictionary<string, object>>();
                }
                
                return null;
            }
            set
            {
                _customDataRaw = value != null ? JObject.FromObject(value) : null;
            }
        }
        
        // New property for accessing raw JSON data
        [JsonIgnore]
        public JToken custom_data_json => _customDataRaw;

        private static string ConfigPath => Path.Combine(Application.dataPath, "..", "playflow.json");
        private static string ResourcePath = "playflow"; // The JSON file should be named playflow_local.json in Resources

        public static PlayFlowServerConfig LoadConfig(bool useLocalConfig = false)
        {
            try
            {
                if (useLocalConfig)
                {
                    // Try to load from Resources folder first
                    var textAsset = Resources.Load<TextAsset>(ResourcePath);
                    if (textAsset == null)
                    {
                        Debug.LogError($"Local PlayFlow config not found in Resources/{ResourcePath}.json");
                        return null;
                    }

                    var config = JsonConvert.DeserializeObject<PlayFlowServerConfig>(textAsset.text);
                    Debug.Log($"PlayFlow config loaded from Resources: Match ID: {config.match_id}, Region: {config.region}");
                    
                    // Example of accessing custom_data - works with both simple and complex data
                    if (config.custom_data != null)
                    {
                        // Backward compatible: Access simple values via dictionary
                        if (config.custom_data.ContainsKey("match_id"))
                            Debug.Log($"Custom match_id: {config.custom_data["match_id"]}");
                    }
                    
                    // For complex nested data (like matchmaking), use custom_data_json
                    if (config.custom_data_json != null)
                    {
                        // Access nested arrays (e.g., teams)
                        var teams = config.custom_data_json["teams"] as JArray;
                        if (teams != null)
                            Debug.Log($"Number of teams: {teams.Count}");
                        
                        // Access all players
                        var players = config.custom_data_json["all_players"]?.ToObject<List<string>>();
                        if (players != null)
                            Debug.Log($"All players: {string.Join(", ", players)}");
                    }
                    
                    return config;
                }
                else
                {
                    // Load from server directory
                    if (!File.Exists(ConfigPath))
                    {
                        Debug.LogError("PlayFlow config file not found at: " + ConfigPath);
                        return null;
                    }

                    string jsonContent = File.ReadAllText(ConfigPath);
                    var config = JsonConvert.DeserializeObject<PlayFlowServerConfig>(jsonContent);
                    Debug.Log($"PlayFlow config loaded from server directory: Match ID: {config.match_id}, Region: {config.region}");
                    
                    // Log matchmaking data if present (use custom_data_json for complex data)
                    if (config.custom_data_json != null && config.custom_data_json["matchmaking_mode"] != null)
                    {
                        Debug.Log($"Matchmaking mode: {config.custom_data_json["matchmaking_mode"]}");
                    }
                    
                    return config;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading PlayFlow config: {e.Message}");
                return null;
            }
        }

        // Helper methods to access common matchmaking data (uses custom_data_json for complex nested data)
        public string GetMatchmakingMode()
        {
            return custom_data_json?["matchmaking_mode"]?.ToString();
        }
        
        public List<string> GetAllPlayers()
        {
            return custom_data_json?["all_players"]?.ToObject<List<string>>() ?? new List<string>();
        }
        
        public JArray GetTeams()
        {
            return custom_data_json?["teams"] as JArray;
        }
        
        public JObject GetMatchConfiguration()
        {
            return custom_data_json?["match_configuration"] as JObject;
        }
        
        public JObject GetLobbySettings()
        {
            return custom_data_json?["lobby_settings"] as JObject;
        }
        
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
} 
