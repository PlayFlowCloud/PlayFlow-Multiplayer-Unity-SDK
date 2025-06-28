using System.Collections.Generic;
using Newtonsoft.Json;
using System;

namespace PlayFlow
{
    [Serializable]
    public class Lobby
    {
        public string id;
        public string name;
        public int currentPlayers;
        public int maxPlayers;
        public string[] players;
        public string host;
        public string status;
        public bool isPrivate;
        public string inviteCode;
        public bool allowLateJoin;
        public string region;
        public string createdAt;
        public string updatedAt;
        public Dictionary<string, object> settings;
        public Dictionary<string, object> gameServer;
        public Dictionary<string, Dictionary<string, object>> lobbyStateRealTime;
        
        // Helper method to convert region to a display-friendly string
        public string GetRegionDisplayName()
        {
            if (string.IsNullOrEmpty(region))
                return "Unknown";
                
            return region switch
            {
                "us-east" => "US East",
                "us-west" => "US West",
                "eu-west" => "Europe West",
                "eu-central" => "Europe Central",
                "asia-east" => "Asia East",
                "asia-south" => "Asia South",
                "australia" => "Australia",
                _ => region // Return as-is if not recognized
            };
        }
        
        // Helper method to get the game server status as a string
        public string GetGameServerStatus()
        {
            if (gameServer == null || !gameServer.TryGetValue("status", out object statusObj))
                return "N/A";
                
            return statusObj?.ToString() ?? "N/A";
        }
        
        // Deep clone method to create a copy of the lobby
        public Lobby Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<Lobby>(json);
        }
        
        public override string ToString()
        {
            return $"Lobby[{name}({id}), Players: {currentPlayers}/{maxPlayers}, Status: {status}]";
        }
    }
} 