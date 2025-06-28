using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

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
        public Dictionary<string, object> custom_data { get; set; }

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
                    //Print the entire config
                    //Debug.Log(config.ToString());
                    //also get the custom data (which is a JSON object)
                    //Debug.Log(config.custom_data.ToString());
                    //get a specific key from the custom data
                    //Debug.Log(config.custom_data["test"]);
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
                    return config;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading PlayFlow config: {e.Message}");
                return null;
            }
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
} 
