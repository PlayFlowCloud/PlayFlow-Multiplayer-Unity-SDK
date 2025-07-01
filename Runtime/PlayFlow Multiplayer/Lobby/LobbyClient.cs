// LobbyClient.cs
// This script requires Newtonsoft.Json to be imported into your Unity project.
// You can get it from the Unity Asset Store or via NuGet.

using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Collections;
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

        private IEnumerator SendRequestCoroutine<T>(string path, string method, Action<T> onSuccess, Action<Exception> onError, Dictionary<string, string> queryParams = null, JObject body = null) where T : JToken
        {
            var uriBuilder = new UriBuilder(_baseUrl) { Path = path };
            
            if (queryParams != null)
            {
                var query = new StringBuilder();
                foreach (var param in queryParams)
                {
                    if (query.Length > 0) query.Append('&');
                    query.Append(Uri.EscapeDataString(param.Key) + "=" + Uri.EscapeDataString(param.Value));
            }
                uriBuilder.Query = query.ToString();
            }

            using (var request = new UnityWebRequest(uriBuilder.Uri, method))
            {
                request.SetRequestHeader(ApiKeyHeaderName, _apiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.downloadHandler = new DownloadHandlerBuffer();

                if (body != null)
                {
                    var bodyBytes = Encoding.UTF8.GetBytes(body.ToString());
                    request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                }

                yield return request.SendWebRequest();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(new Exception($"API Error: {request.error} - {request.downloadHandler.text}"));
                    yield break;
                }
                
                // Handle empty responses (like 204 No Content) gracefully
                if (string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    onSuccess?.Invoke(null);
                    yield break;
                }
                
                try
                {
                    var responseJson = request.downloadHandler.text;
                    T result = JToken.Parse(responseJson) as T;
                    onSuccess?.Invoke(result);
                }
                catch (Exception e)
                {
                    onError?.Invoke(new Exception($"Failed to parse response: {e.Message}"));
                }
            }
        }

        // Coroutine versions of all public methods
        
        public IEnumerator ListLobbiesCoroutine(string lobbyConfigName, Action<JArray> onSuccess, Action<Exception> onError, bool? listPublicOnly = null)
        {
            var queryParams = new Dictionary<string, string> { { "name", lobbyConfigName } };
            if (listPublicOnly.HasValue)
            {
                queryParams.Add("public", listPublicOnly.Value.ToString().ToLower());
            }

            yield return SendRequestCoroutine("lobbies", UnityWebRequest.kHttpVerbGET, onSuccess, onError, queryParams);
        }
        
        public IEnumerator CreateLobbyCoroutine(string lobbyConfigName, string lobbyName, int maxPlayers, bool isPrivate, bool useInviteCode, bool allowLateJoin, string region, JObject settings, string hostPlayerId, Action<JObject> onSuccess, Action<Exception> onError)
        {
            var queryParams = new Dictionary<string, string> { { "name", lobbyConfigName } };
            var body = new JObject
            {
                ["name"] = lobbyName,
                ["maxPlayers"] = maxPlayers,
                ["isPrivate"] = isPrivate,
                ["useInviteCode"] = useInviteCode,
                ["allowLateJoin"] = allowLateJoin,
                ["region"] = region,
                ["settings"] = settings,
                ["host"] = hostPlayerId
            };

            yield return SendRequestCoroutine("/lobbies", UnityWebRequest.kHttpVerbPOST, onSuccess, onError, queryParams, body);
        }

        public IEnumerator GetLobbyCoroutine(string lobbyConfigName, string id, Action<JObject> onSuccess, Action<Exception> onError)
        {
            var queryParams = new Dictionary<string, string> { { "name", lobbyConfigName } };
            yield return SendRequestCoroutine($"/lobbies/{id}", UnityWebRequest.kHttpVerbGET, onSuccess, onError, queryParams);
        }

        public IEnumerator UpdateLobbyCoroutine(string lobbyConfigName, string lobbyId, string requesterId, JObject updatedLobbyDetails, Action<JObject> onSuccess, Action<Exception> onError)
        {
            var queryParams = new Dictionary<string, string> { { "name", lobbyConfigName } };
            var body = new JObject
            {
                ["requesterId"] = requesterId
            };
            
            // Merge updatedLobbyDetails into body
            if (updatedLobbyDetails != null)
            {
                foreach (var property in updatedLobbyDetails.Properties())
                {
                    body[property.Name] = property.Value;
            }
            }
            
            yield return SendRequestCoroutine($"/lobbies/{lobbyId}", UnityWebRequest.kHttpVerbPUT, onSuccess, onError, queryParams, body);
        }

        public IEnumerator DeleteLobbyCoroutine(string lobbyConfigName, string lobbyId, string requesterPlayerId, Action<JObject> onSuccess, Action<Exception> onError)
        {
            var queryParams = new Dictionary<string, string> { { "name", lobbyConfigName } };
            var body = new JObject
            {
                ["playerId"] = requesterPlayerId
            };

            yield return SendRequestCoroutine($"/lobbies/{lobbyId}", UnityWebRequest.kHttpVerbDELETE, onSuccess, onError, queryParams, body);
        }

        public IEnumerator JoinLobbyByCodeCoroutine(string lobbyConfigName, string inviteCode, string playerId, JObject playerMetadata, Action<JObject> onSuccess, Action<Exception> onError)
        {
            var queryParams = new Dictionary<string, string> { { "name", lobbyConfigName } };
            var body = new JObject { ["playerId"] = playerId };
            if (playerMetadata != null)
            {
                body["metadata"] = playerMetadata;
            }

            yield return SendRequestCoroutine($"/lobbies/code/{inviteCode}/players", UnityWebRequest.kHttpVerbPOST, onSuccess, onError, queryParams, body);
        }

        public IEnumerator ListPlayersInLobbyCoroutine(string lobbyConfigName, string lobbyId, Action<JArray> onSuccess, Action<Exception> onError)
        {
            var queryParams = new Dictionary<string, string> { { "name", lobbyConfigName } };
            yield return SendRequestCoroutine($"/lobbies/{lobbyId}/players", UnityWebRequest.kHttpVerbGET, onSuccess, onError, queryParams);
        }

        public IEnumerator AddPlayerToLobbyCoroutine(string lobbyConfigName, string lobbyId, string playerId, JObject playerMetadata, Action<JObject> onSuccess, Action<Exception> onError)
        {
            var queryParams = new Dictionary<string, string> { { "name", lobbyConfigName } };
            var body = new JObject { ["playerId"] = playerId };
            if (playerMetadata != null)
            {
                body["metadata"] = playerMetadata;
            }

            yield return SendRequestCoroutine($"/lobbies/{lobbyId}/players", UnityWebRequest.kHttpVerbPOST, onSuccess, onError, queryParams, body);
        }

        public IEnumerator RemovePlayerFromLobbyCoroutine(string lobbyConfigName, string lobbyId, string playerIdToRemove, string requesterId, bool? isKick, Action<JObject> onSuccess, Action<Exception> onError)
        {
            var queryParams = new Dictionary<string, string>
            {
                { "name", lobbyConfigName },
                { "requesterId", requesterId }
            };
            if (isKick.HasValue)
            {
                queryParams.Add("isKick", isKick.Value.ToString().ToLower());
            }

            yield return SendRequestCoroutine($"/lobbies/{lobbyId}/players/{playerIdToRemove}", UnityWebRequest.kHttpVerbDELETE, onSuccess, onError, queryParams);
        }
    }
} 