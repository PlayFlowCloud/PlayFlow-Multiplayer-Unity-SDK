// LobbyClient.cs
// This script requires Newtonsoft.Json to be imported into your Unity project.
// You can get it from the Unity Asset Store or via NuGet.

using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // Added for CancellationToken

namespace PlayFlow
{
    public class LobbyClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private const string ApiKeyHeaderName = "api-key";

        public LobbyClient(string baseUrl, string apiKey)
        {
            if (string.IsNullOrEmpty(baseUrl))
                throw new ArgumentNullException(nameof(baseUrl));
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentNullException(nameof(apiKey));

            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
        }

        private async Task<T> SendRequestAsync<T>(string path, string method, Dictionary<string, string> queryParams = null, JObject body = null, int timeoutSeconds = 20, CancellationToken cancellationToken = default) where T : JToken
        {
            string queryString = "";
            if (queryParams != null && queryParams.Count > 0)
            {
                queryString = "?" + string.Join("&", queryParams.Select(kvp => $"{UnityWebRequest.EscapeURL(kvp.Key)}={UnityWebRequest.EscapeURL(kvp.Value)}"));
            }
            
            string fullUrl = _baseUrl + path + queryString;

            using (var request = new UnityWebRequest(fullUrl, method))
            {
                request.timeout = timeoutSeconds; // Works in 2020.3+
                request.SetRequestHeader(ApiKeyHeaderName, _apiKey);
                request.downloadHandler = new DownloadHandlerBuffer();

                if (body != null)
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(body.ToString());
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        request.Abort(); // Abort the UnityWebRequest
                        cancellationToken.ThrowIfCancellationRequested(); // Propagate the cancellation
                    }
                    await Task.Yield(); // Yield control to allow Unity to process
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    if (string.IsNullOrEmpty(responseText))
                    {
                        // Handle cases like 204 No Content, if applicable, or return null for JToken
                        if (typeof(T) == typeof(JValue) || typeof(T) == typeof(JToken)) return null; // Or an empty JObject/JArray
                        if (typeof(T) == typeof(JObject)) return JObject.Parse("{}") as T;
                        if (typeof(T) == typeof(JArray)) return JArray.Parse("[]") as T;
                    }
                    return JToken.Parse(responseText) as T;
                }
                else
                {
                    string errorDetails = $"Error: {request.error}, Response Code: {request.responseCode}";
                    if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                    {
                        errorDetails += $", Response Body: {request.downloadHandler.text}";
                    }
                    Debug.LogError($"API Request Failed for {method} {fullUrl}: {errorDetails}");
                    throw new Exception($"API Error ({request.responseCode}) for {method} {fullUrl}: {request.error}. Details: {request.downloadHandler?.text}");
                }
            }
        }

        // Lobby methods will be added here

        /// <summary>
        /// List all lobbies for a specific lobby configuration.
        /// </summary>
        /// <param name="lobbyConfigName">Name of the lobby configuration to list lobbies for.</param>
        /// <param name="listPublicOnly">Optional: Filter to only public lobbies.</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JArray> ListLobbiesAsync(string lobbyConfigName, bool? listPublicOnly = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(lobbyConfigName))
                throw new ArgumentNullException(nameof(lobbyConfigName));

            string path = "/lobbies";
            var queryParams = new Dictionary<string, string>
            {
                ["name"] = lobbyConfigName
            };

            if (listPublicOnly.HasValue)
            {
                queryParams["public"] = listPublicOnly.Value.ToString().ToLower();
            }

            return await SendRequestAsync<JArray>(path, UnityWebRequest.kHttpVerbGET, queryParams: queryParams, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Create a new lobby under a specific lobby configuration.
        /// </summary>
        /// <param name="lobbyConfigName">Name of the lobby configuration (from project settings) to use.</param>
        /// <param name="lobbyName">The display name of the lobby.</param>
        /// <param name="maxPlayers">Maximum number of players.</param>
        /// <param name="isPrivate">Whether the lobby is private.</param>
        /// <param name="useInviteCode">Whether to generate an invite code.</param>
        /// <param name="allowLateJoin">Whether late joining is allowed.</param>
        /// <param name="region">Geographic region for the lobby server.</param>
        /// <param name="settings">Custom game settings.</param>
        /// <param name="hostPlayerId">ID of the player creating the lobby.</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> CreateLobbyAsync(string lobbyConfigName, string lobbyName, int maxPlayers, bool isPrivate, bool useInviteCode, bool allowLateJoin, string region, JObject settings, string hostPlayerId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(lobbyConfigName))
                throw new ArgumentNullException(nameof(lobbyConfigName));
            if (string.IsNullOrEmpty(lobbyName))
                throw new ArgumentNullException(nameof(lobbyName));
             if (string.IsNullOrEmpty(hostPlayerId))
                throw new ArgumentNullException(nameof(hostPlayerId));
            // Other validations for maxPlayers, region, etc., could be added here based on openapi.json constraints

            string path = "/lobbies";
            var queryParams = new Dictionary<string, string>
            {
                ["name"] = lobbyConfigName
            };

            JObject payload = new JObject
            {
                ["name"] = lobbyName,
                ["maxPlayers"] = maxPlayers,
                ["isPrivate"] = isPrivate,
                ["useInviteCode"] = useInviteCode,
                ["allowLateJoin"] = allowLateJoin,
                ["region"] = region,
                ["settings"] = settings, // Assuming settings is already a JObject
                ["host"] = hostPlayerId
            };

            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbPOST, queryParams: queryParams, body: payload, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Get a specific lobby by ID or find a player's lobby by player ID.
        /// </summary>
        /// <param name="lobbyConfigName">Name of the lobby configuration to search within.</param>
        /// <param name="id">Lobby ID or Player ID.</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> GetLobbyAsync(string lobbyConfigName, string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(lobbyConfigName))
                throw new ArgumentNullException(nameof(lobbyConfigName));
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));

            string path = $"/lobbies/{Uri.EscapeDataString(id)}";
            var queryParams = new Dictionary<string, string>
            {
                ["name"] = lobbyConfigName
            };

            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbGET, queryParams: queryParams, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Update a lobby resource.
        /// </summary>
        /// <param name="lobbyConfigName">Name of the lobby configuration this lobby belongs to.</param>
        /// <param name="lobbyId">Lobby ID.</param>
        /// <param name="requesterId">ID of the player making the request.</param>
        /// <param name="updatedLobbyDetails">JObject containing fields to update (e.g., host, status, settings, playerState).</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> UpdateLobbyAsync(string lobbyConfigName, string lobbyId, string requesterId, JObject updatedLobbyDetails, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(lobbyConfigName))
                throw new ArgumentNullException(nameof(lobbyConfigName));
            if (string.IsNullOrEmpty(lobbyId))
                throw new ArgumentNullException(nameof(lobbyId));
            if (string.IsNullOrEmpty(requesterId))
                throw new ArgumentNullException(nameof(requesterId));
            if (updatedLobbyDetails == null)
                throw new ArgumentNullException(nameof(updatedLobbyDetails));

            string path = $"/lobbies/{Uri.EscapeDataString(lobbyId)}";
            var queryParams = new Dictionary<string, string>
            {
                ["name"] = lobbyConfigName
            };
            
            // The UpdateLobby schema has requesterId as a top-level field in the body.
            // We'll ensure it's part of the updatedLobbyDetails or add it.
            if (!updatedLobbyDetails.ContainsKey("requesterId"))
            {
                updatedLobbyDetails["requesterId"] = requesterId;
            }
            else
            {
                // If it exists but is different, it might be an issue, or we honor the one in updatedLobbyDetails.
                // For now, let's assume the one in updatedLobbyDetails takes precedence if provided, 
                // otherwise, we use the method parameter.
                // Alternatively, always enforce the method parameter:
                updatedLobbyDetails["requesterId"] = requesterId;
            }

            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbPUT, queryParams: queryParams, body: updatedLobbyDetails, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Delete a lobby (host or admin only), scoped to a lobby configuration.
        /// </summary>
        /// <param name="lobbyConfigName">Name of the lobby configuration this lobby belongs to.</param>
        /// <param name="lobbyId">Lobby ID.</param>
        /// <param name="requesterPlayerId">ID of the player performing the action (required for validation in request body).</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> DeleteLobbyAsync(string lobbyConfigName, string lobbyId, string requesterPlayerId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(lobbyConfigName))
                throw new ArgumentNullException(nameof(lobbyConfigName));
            if (string.IsNullOrEmpty(lobbyId))
                throw new ArgumentNullException(nameof(lobbyId));
            if (string.IsNullOrEmpty(requesterPlayerId))
                throw new ArgumentNullException(nameof(requesterPlayerId)); // Required for the body

            string path = $"/lobbies/{Uri.EscapeDataString(lobbyId)}";
            var queryParams = new Dictionary<string, string>
            {
                ["name"] = lobbyConfigName
            };

            JObject payload = null;
            // The OpenAPI spec indicates an optional requestBody for DELETE /lobbies/{id} of type LobbyAction
            // LobbyAction has one required field: playerId.
            payload = new JObject
            {
                ["playerId"] = requesterPlayerId
            };

            // Expects 204 No Content, SendRequestAsync should handle this by returning null or empty JObject.
            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbDELETE, queryParams: queryParams, body: payload, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Join a lobby by invite code, scoped to a lobby configuration.
        /// </summary>
        /// <param name="lobbyConfigName">Name of the lobby configuration to attempt joining.</param>
        /// <param name="inviteCode">Lobby invite code.</param>
        /// <param name="playerId">ID of the player to add to the lobby.</param>
        /// <param name="playerMetadata">Optional metadata about the player.</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> JoinLobbyByCodeAsync(string lobbyConfigName, string inviteCode, string playerId, JObject playerMetadata = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(lobbyConfigName))
                throw new ArgumentNullException(nameof(lobbyConfigName));
            if (string.IsNullOrEmpty(inviteCode))
                throw new ArgumentNullException(nameof(inviteCode));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentNullException(nameof(playerId));

            string path = $"/lobbies/code/{Uri.EscapeDataString(inviteCode)}/players";
            var queryParams = new Dictionary<string, string>
            {
                ["name"] = lobbyConfigName
            };

            JObject payload = new JObject
            {
                ["playerId"] = playerId
            };
            if (playerMetadata != null)
            {
                payload["metadata"] = playerMetadata;
            }

            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbPOST, queryParams: queryParams, body: payload, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// List all players in a lobby.
        /// </summary>
        /// <param name="lobbyConfigName">Name of the lobby configuration this lobby belongs to.</param>
        /// <param name="lobbyId">Lobby ID.</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JArray> ListPlayersInLobbyAsync(string lobbyConfigName, string lobbyId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(lobbyConfigName))
                throw new ArgumentNullException(nameof(lobbyConfigName));
            if (string.IsNullOrEmpty(lobbyId))
                throw new ArgumentNullException(nameof(lobbyId));

            string path = $"/lobbies/{Uri.EscapeDataString(lobbyId)}/players";
            var queryParams = new Dictionary<string, string>
            {
                ["name"] = lobbyConfigName
            };

            return await SendRequestAsync<JArray>(path, UnityWebRequest.kHttpVerbGET, queryParams: queryParams, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Add a player to a lobby (join).
        /// </summary>
        /// <param name="lobbyConfigName">Name of the lobby configuration this lobby belongs to.</param>
        /// <param name="lobbyId">Lobby ID.</param>
        /// <param name="playerId">ID of the player to add.</param>
        /// <param name="playerMetadata">Optional metadata about the player.</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> AddPlayerToLobbyAsync(string lobbyConfigName, string lobbyId, string playerId, JObject playerMetadata = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(lobbyConfigName))
                throw new ArgumentNullException(nameof(lobbyConfigName));
            if (string.IsNullOrEmpty(lobbyId))
                throw new ArgumentNullException(nameof(lobbyId));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentNullException(nameof(playerId));

            string path = $"/lobbies/{Uri.EscapeDataString(lobbyId)}/players";
            var queryParams = new Dictionary<string, string>
            {
                ["name"] = lobbyConfigName
            };

            JObject payload = new JObject
            {
                ["playerId"] = playerId
            };
            if (playerMetadata != null)
            {
                payload["metadata"] = playerMetadata;
            }

            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbPOST, queryParams: queryParams, body: payload, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Remove a player from a lobby (leave or kick).
        /// </summary>
        /// <param name="lobbyConfigName">Name of the lobby configuration this lobby belongs to.</param>
        /// <param name="lobbyId">Lobby ID.</param>
        /// <param name="playerIdToRemove">Player ID to remove.</param>
        /// <param name="requesterId">ID of the player making the request.</param>
        /// <param name="isKick">Whether this is a kick operation (requires host permissions).</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> RemovePlayerFromLobbyAsync(string lobbyConfigName, string lobbyId, string playerIdToRemove, string requesterId, bool? isKick = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(lobbyConfigName))
                throw new ArgumentNullException(nameof(lobbyConfigName));
            if (string.IsNullOrEmpty(lobbyId))
                throw new ArgumentNullException(nameof(lobbyId));
            if (string.IsNullOrEmpty(playerIdToRemove))
                throw new ArgumentNullException(nameof(playerIdToRemove));
            if (string.IsNullOrEmpty(requesterId))
                throw new ArgumentNullException(nameof(requesterId));

            string path = $"/lobbies/{Uri.EscapeDataString(lobbyId)}/players/{Uri.EscapeDataString(playerIdToRemove)}";
            var queryParams = new Dictionary<string, string>
            {
                ["name"] = lobbyConfigName,
                ["requesterId"] = requesterId
            };

            if (isKick.HasValue)
            {
                queryParams["isKick"] = isKick.Value.ToString().ToLower();
            }

            // Expects 200 Lobby object or 204 No Content (if lobby also deleted)
            // SendRequestAsync handles 204 by returning null or empty JObject.
            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbDELETE, queryParams: queryParams, cancellationToken: cancellationToken);
        }
    }
} 