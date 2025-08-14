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
        public bool useInviteCode;
        public string inviteCode;
        public bool allowLateJoin;
        public int timeout;
        public string region;
        public string createdAt;
        public string updatedAt;
        public Dictionary<string, object> settings;
        public Dictionary<string, object> gameServer;
        public Dictionary<string, Dictionary<string, object>> lobbyStateRealTime;
        
        // Matchmaking fields
        public string matchmakingMode;
        public string matchmakingStartedAt;
        public string matchmakingTicketId;
        public Dictionary<string, object> matchmakingData;
        
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

        public static ConnectionInfo? GetPrimaryConnectionInfo(Lobby lobby)
        {
            if (lobby?.gameServer == null) return null;

            var gameServer = lobby.gameServer;

            // New simplified logic: take the first port from network_ports if it exists.
            if (gameServer.ContainsKey("network_ports") && gameServer["network_ports"] is Newtonsoft.Json.Linq.JArray ports && ports.Count > 0)
            {
                var firstPort = ports[0];
                string host = firstPort["host"]?.ToString();
                int? port = firstPort["external_port"]?.ToObject<int>();

                if (!string.IsNullOrEmpty(host) && port.HasValue)
                {
                    return new ConnectionInfo { Ip = host, Port = port.Value };
                }
            }

            // Fallback to simple ip/port fields for older server configs
            if (gameServer.ContainsKey("ip") && gameServer.ContainsKey("port"))
            {
                string ip = gameServer["ip"].ToString();
                int portNum = Convert.ToInt32(gameServer["port"]);
                return new ConnectionInfo { Ip = ip, Port = portNum };
            }

            return null;
        }
    }
} 