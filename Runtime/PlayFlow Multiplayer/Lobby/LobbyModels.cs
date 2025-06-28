// using System;
// using System.Collections.Generic;
// // using Newtonsoft.Json; // Not strictly needed here if only used for [Serializable] and basic types

// namespace PlayFlow
// {
//     [Serializable]
//     public class Player
//     {
//         public string id;
//         public string name;
//         public string status;  // "active" | "ready" | "away"
//         public string joinedAt;
//         public Dictionary<string, object> customState; // This might be JObject if directly from new API
//     }

//     [Serializable]
//     public class Lobby
//     {
//         public string id;
//         public string name;
//         public string host;
//         public int maxPlayers;
//         public string region;
//         public int currentPlayers;
//         public string status;  // "waiting" | "in_game" | "finished"
//         public string createdAt;
//         public Dictionary<string, object> settings; // This might be JObject 
//         public bool isPrivate;
//         public string inviteCode;
//         public bool allowLateJoin;
//         public string[] players;  // List of player IDs in the lobby
//         public Dictionary<string, object> gameServer; // This might be JObject
//         public Dictionary<string, Dictionary<string, object>> lobbyStateRealTime;  // Player real-time states. This might be Dictionary<string, JObject>
//     }
// } 