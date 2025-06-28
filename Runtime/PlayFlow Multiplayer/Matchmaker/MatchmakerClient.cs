// MatchmakerClient.cs
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
    public class MatchmakerClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private const string ApiKeyHeaderName = "api-key";
        private const string MatchmakerServicePath = "/matchmaker"; // Added service path

        public MatchmakerClient(string baseUrl, string apiKey)
        {
            if (string.IsNullOrEmpty(baseUrl))
                throw new ArgumentNullException(nameof(baseUrl));
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentNullException(nameof(apiKey));

            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
        }

        private async Task<T> SendRequestAsync<T>(string path, string method, Dictionary<string, string> queryParams = null, JObject body = null, int timeoutSeconds = 10, CancellationToken cancellationToken = default) where T : JToken
        {
            string queryString = "";
            if (queryParams != null && queryParams.Count > 0)
            {
                queryString = "?" + string.Join("&", queryParams.Select(kvp => $"{UnityWebRequest.EscapeURL(kvp.Key)}={UnityWebRequest.EscapeURL(kvp.Value)}"));
            }
            
            string fullUrl = _baseUrl + MatchmakerServicePath + path + queryString; // New way, prepends /matchmaker

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

        /// <summary>
        /// Create a new matchmaking ticket.
        /// </summary>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> CreateTicketAsync(string matchmakerName, string playerId, List<string> regions = null, int? elo = 1000, List<string> preferredModes = null, Dictionary<string, JToken> customFields = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(matchmakerName))
                throw new ArgumentNullException(nameof(matchmakerName));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentNullException(nameof(playerId));

            string path = "/tickets";
            JObject payload = new JObject
            {
                ["matchmaker_name"] = matchmakerName,
                ["player_id"] = playerId
            };

            if (regions != null && regions.Count > 0) payload["regions"] = new JArray(regions);
            
            // Spec default is 1000. If elo is null, use 1000. Otherwise, use provided value.
            payload["elo"] = elo ?? 1000; 
            
            if (preferredModes != null && preferredModes.Count > 0) payload["preferred_modes"] = new JArray(preferredModes);

            if (customFields != null)
            {
                foreach (var field in customFields)
                {
                    // Ensure we don't overwrite existing specifically handled fields
                    // or allow reserved names if any (none explicitly mentioned yet for this scenario)
                    if (!payload.ContainsKey(field.Key))
                    {
                        payload[field.Key] = field.Value;
                    }
                    else
                    {
                        // Optionally, log a warning or decide on overwrite behavior
                        Debug.LogWarning($"Custom field '{field.Key}' conflicts with an existing payload field and will be ignored.");
                    }
                }
            }

            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbPOST, body: payload, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// List all active (queued) tickets for a matchmaker.
        /// </summary>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JArray> ListTicketsAsync(string matchmakerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(matchmakerName))
                throw new ArgumentNullException(nameof(matchmakerName));

            string path = "/tickets";
            var queryParams = new Dictionary<string, string>
            {
                ["matchmaker_name"] = matchmakerName
            };
            return await SendRequestAsync<JArray>(path, UnityWebRequest.kHttpVerbGET, queryParams: queryParams, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Cancel a ticket or leave a match.
        /// </summary>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> CancelTicketOrLeaveMatchAsync(string ticketId, string matchmakerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(ticketId))
                throw new ArgumentNullException(nameof(ticketId));
            if (string.IsNullOrEmpty(matchmakerName))
                throw new ArgumentNullException(nameof(matchmakerName));
            
            string path = $"/tickets/{Uri.EscapeDataString(ticketId)}";
            var queryParams = new Dictionary<string, string>
            {
                ["matchmaker_name"] = matchmakerName
            };
            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbDELETE, queryParams: queryParams, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Get ticket or player status.
        /// </summary>
        /// <param name="id">ID of the ticket or player.</param>
        /// <param name="idType">'ticket' or 'player'. Defaults to 'ticket'.</param>
        /// <param name="matchmakerName">Required if idType is 'ticket'. Name of the matchmaker.</param>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> GetTicketOrPlayerStatusAsync(string id, string idType = "ticket", string matchmakerName = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));
            if (idType != "ticket" && idType != "player")
                throw new ArgumentException("idType must be 'ticket' or 'player'.", nameof(idType));
            if (idType == "ticket" && string.IsNullOrEmpty(matchmakerName))
                throw new ArgumentException("matchmakerName is required when idType is 'ticket'.", nameof(matchmakerName));

            string path = $"/tickets/{Uri.EscapeDataString(id)}";
            var queryParams = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(matchmakerName))
            {
                queryParams["matchmaker_name"] = matchmakerName;
            }
            if (idType == "player") // Only send if not the default "ticket"
            {
                queryParams["type"] = idType;
            }
            
            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbGET, queryParams: queryParams, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// List all active matches for a matchmaker.
        /// </summary>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JArray> ListMatchesAsync(string matchmakerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(matchmakerName))
                throw new ArgumentNullException(nameof(matchmakerName));

            string path = "/matches";
            var queryParams = new Dictionary<string, string>
            {
                ["matchmaker_name"] = matchmakerName
            };
            return await SendRequestAsync<JArray>(path, UnityWebRequest.kHttpVerbGET, queryParams: queryParams, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Delete a match.
        /// </summary>
        /// <param name="cancellationToken">Optional: Token to cancel the request.</param>
        public async Task<JObject> DeleteMatchAsync(string matchId, string matchmakerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(matchId))
                throw new ArgumentNullException(nameof(matchId));
            if (string.IsNullOrEmpty(matchmakerName))
                throw new ArgumentNullException(nameof(matchmakerName));

            string path = $"/matches/{Uri.EscapeDataString(matchId)}";
            var queryParams = new Dictionary<string, string>
            {
                ["matchmaker_name"] = matchmakerName
            };
            return await SendRequestAsync<JObject>(path, UnityWebRequest.kHttpVerbDELETE, queryParams: queryParams, cancellationToken: cancellationToken);
        }
    }
} 