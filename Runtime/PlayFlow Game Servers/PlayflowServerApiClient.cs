// PlayflowServerApiClient.cs
// This file contains the API client for PlayFlow's server management.

using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json; // Added for Newtonsoft.Json

namespace PlayFlow.SDK.Servers
{
    // ------------- String constants for Enum-like fields -------------

    /// <summary>
    /// Defines possible states for a game server instance.
    /// </summary>
    public static class InstanceStates
    {
        public const string Launching = "launching";
        public const string Running = "running";
        public const string Stopped = "stopped";
    }

    /// <summary>
    /// Defines possible service types for a game server.
    /// </summary>
    public static class ServiceTypes
    {
        public const string MatchBased = "match_based";
        public const string PersistentWorld = "persistent_world";
    }

    /// <summary>
    /// Defines network protocol types.
    /// </summary>
    public static class ProtocolTypes
    {
        public const string Udp = "udp";
        public const string Tcp = "tcp";
    }

    /// <summary>
    /// Defines compute sizes for server instances, based on OpenAPI specification.
    /// </summary>
    public static class ComputeSizes 
    {
        public const string Small = "small";
        public const string Medium = "medium";
        public const string Large = "large";
        public const string XLarge = "xlarge";
        public const string DedicatedSmall = "dedicated-small";
        public const string DedicatedMedium = "dedicated-medium";
        public const string DedicatedLarge = "dedicated-large";
        public const string DedicatedXLarge = "dedicated-xlarge";
    }

    // ------------- Data Models -------------

    /// <summary>
    /// Represents network port mapping configuration.
    /// </summary>
    public class PortMapping
    {
        /// <summary>Friendly name for this port (e.g., 'game_udp', 'game_tcp').</summary>
        public string name;
        /// <summary>The port the application listens on inside the VM/container.</summary>
        public int internal_port;
        /// <summary>The public-facing port clients connect to.</summary>
        public int external_port;
        /// <summary>Network protocol. Use ProtocolTypes constants (udp, tcp).</summary>
        public string protocol;
        /// <summary>The public IP address or domain name clients connect to.</summary>
        public string host;
        /// <summary>Whether TLS termination is enabled (only applicable for TCP). Default: false.</summary>
        public bool tls_enabled;
    }

    /// <summary>
    /// Represents a game server instance's data, based on the "Instance" schema.
    /// </summary>
    public class InstanceData 
    {
        public string instance_id;
        public string name; 
        public List<PortMapping> network_ports;
        /// <summary>Server status. Use InstanceStates constants (launching, running, stopped).</summary>
        public string status; 
        public string startup_args; 
        /// <summary>Service type. Use ServiceTypes constants (match_based, persistent_world).</summary>
        public string service_type; 
        /// <summary>Compute size. Refer to ComputeSizes for possible values if setting on creation.</summary>
        public string compute_size; 
        public string region; 
        public string version_tag; 
        /// <summary>ISO 8601 date-time string when the server started, nullable.</summary>
        public string started_at; 
        /// <summary>ISO 8601 date-time string when the server stopped, nullable.</summary>
        public string stopped_at; 
        
        /// <summary>
        /// Custom data associated with the server instance.
        /// Example: new Dictionary<string, object> { { "map_name", "level1" }, { "max_players", 16 } }
        /// </summary>
        public Dictionary<string, object> custom_data; 
        
        /// <summary>
        /// Time to live in seconds. Valid range 60-86400 if set. Nullable.
        /// </summary>
        public int? ttl; 
    }

    /// <summary>
    /// Represents a list of server instances.
    /// </summary>
    public class ServerList
    {
        public int total_servers;
        public List<InstanceData> servers;
    }

    /// <summary>
    /// Request schema for creating a new server.
    /// </summary>
    public class ServerCreateRequest
    {
        /// <summary>The name of the server instance. Required.</summary>
        public string name; 
        public string startup_args; 
        /// <summary>Region for the server deployment. Must be a valid PlayFlow region. Required.</summary>
        public string region; 
        
        /// <summary>
        /// Server size type. Use ComputeSizes constants.
        /// Defaults to "small" if not specified, as per OpenAPI spec.
        /// </summary>
        public string compute_size; 
        
        public string version_tag; 
        
        /// <summary>
        /// Time to live for the server in seconds (valid range: 60-86400). Nullable.
        /// </summary>
        public int? ttl; 
        
        /// <summary>
        /// Custom data to pass to the server.
        /// Example: new Dictionary<string, object> { { "game_mode", "ffa" } }
        /// </summary>
        public Dictionary<string, object> custom_data; 

        public ServerCreateRequest()
        {
            // Apply default compute_size as per OpenAPI specification.
            this.compute_size = ComputeSizes.Small; 
        }
    }

    /// <summary>
    /// Response schema for a server start operation.
    /// </summary>
    public class ServerStartResponse 
    {
        public string instance_id; 
        public string name; 
        
        /// <summary>
        /// Server status. Use InstanceStates constants.
        /// Defaults to "launching" for new servers as per OpenAPI spec.
        /// </summary>
        public string status; 
        
        public string region; 
        public List<PortMapping> network_ports;
        
        /// <summary>
        /// Server compute size. Defaults to "small" as per OpenAPI spec.
        /// </summary>
        public string compute_size; 
        
        public string version_tag; 
        /// <summary>ISO 8601 date-time string when the server started, nullable.</summary>
        public string started_at; 
    }

    /// <summary>
    /// Response schema for a server stop operation.
    /// </summary>
    public class ServerStopResponse
    {
        /// <summary>Status of the server stop operation.</summary>
        public string status; 
    }

    // Internal schemas for parsing API error responses
    internal class HttpValidationErrorResponse 
    {
        public List<ValidationErrorDetail> detail;
    }

    internal class ValidationErrorDetail
    {
        public List<string> loc; 
        public string msg;
        public string type;
    }

    internal class SimpleErrorMessage 
    {
        public string message;
        public string detail; // Some APIs use 'detail' directly for the message
    }


    // ------------- API Client -------------

    /// <summary>
    /// API client for managing PlayFlow game servers using UnityWebRequest.
    /// </summary>
    public class PlayflowServerApiClient
    {
        private readonly string _apiKey;
        private readonly string _baseUrl;

        private const string DefaultProductionUrl = "https://api.computeflow.cloud";
        // For local testing, you might use:
        // private const string DefaultLocalUrl = "http://localhost:8000";

        /// <summary>
        /// Initializes a new instance of the PlayflowServerApiClient.
        /// </summary>
        /// <param name="apiKey">Your PlayFlow API key.</param>
        /// <param name="customBaseUrl">Optional custom base URL for the API (e.g., for testing).</param>
        public PlayflowServerApiClient(string apiKey, string customBaseUrl = null)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey), "PlayFlow API key is required.");
            }
            this._apiKey = apiKey;
            this._baseUrl = string.IsNullOrEmpty(customBaseUrl) ? DefaultProductionUrl : customBaseUrl;
        }

        private async Task<T> SendRequestAsync<T>(string endpoint, string httpMethod, object payload = null, Dictionary<string, string> additionalHeaders = null)
        {
            string url = _baseUrl + endpoint;
            using (UnityWebRequest request = new UnityWebRequest(url, httpMethod))
            {
                request.SetRequestHeader("api-key", _apiKey);
                request.SetRequestHeader("Accept", "application/json"); // We expect JSON responses

                if (payload != null)
                {
                    // Use Newtonsoft.Json for serialization
                    string jsonPayload = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.SetRequestHeader("Content-Type", "application/json"); // Important for POST/PUT
                }
                
                if (additionalHeaders != null)
                {
                    foreach (var header in additionalHeaders)
                    {
                        request.SetRequestHeader(header.Key, header.Value);
                    }
                }

                request.downloadHandler = new DownloadHandlerBuffer();

                await request.SendWebRequestAsync(); // Custom extension method for Task-based async

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorTitle = $"API Error ({(int)request.responseCode} {request.responseCode.ToString()}): {request.error}";
                    string responseBody = request.downloadHandler?.text ?? "No response body.";
                    string detailedMessage = errorTitle;

                    if (!string.IsNullOrEmpty(responseBody) && (request.responseCode == 422 || request.responseCode == 400 || request.responseCode >= 500))
                    {
                        try
                        {
                            // Use Newtonsoft.Json for deserializing error responses
                            HttpValidationErrorResponse validationError = JsonConvert.DeserializeObject<HttpValidationErrorResponse>(responseBody);
                            if (validationError != null && validationError.detail != null && validationError.detail.Count > 0)
                            {
                                var firstDetail = validationError.detail[0];
                                string location = (firstDetail.loc != null && firstDetail.loc.Count > 0) ? string.Join(" -> ", firstDetail.loc) : "N/A";
                                detailedMessage = $"Validation Error: {firstDetail.msg} (Field: {location}, Type: {firstDetail.type})";
                            }
                            else 
                            {
                                SimpleErrorMessage simpleError = JsonConvert.DeserializeObject<SimpleErrorMessage>(responseBody);
                                if (simpleError != null && !string.IsNullOrEmpty(simpleError.message))
                                {
                                    detailedMessage = simpleError.message;
                                } else if (simpleError != null && !string.IsNullOrEmpty(simpleError.detail)) {
                                     detailedMessage = simpleError.detail;
                                } else {
                                    detailedMessage = $"{errorTitle} - Details: {responseBody}";
                                }
                            }
                        }
                        catch (JsonException ex) 
                        {
                            Debug.LogWarning($"Failed to parse API error JSON response with Newtonsoft.Json '{responseBody}': {ex.Message}");
                            detailedMessage = $"{errorTitle} - Raw Error: {responseBody}";
                        }
                        catch (Exception ex) 
                        {
                            Debug.LogWarning($"An unexpected error occurred while parsing API error JSON response '{responseBody}': {ex.Message}");
                            detailedMessage = $"{errorTitle} - Raw Error: {responseBody}";
                        }
                    } else if (!string.IsNullOrEmpty(responseBody)) {
                         detailedMessage = $"{errorTitle} - Details: {responseBody}";
                    }
                
                    Debug.LogError($"PlayFlow API Request Failed for {url}. Error: {detailedMessage}");
                    throw new PlayFlowApiException(detailedMessage, request.responseCode, responseBody);
                }
                else // Success
                {
                    string responseJson = request.downloadHandler.text;
                    
                    if (string.IsNullOrEmpty(responseJson) && typeof(T) != typeof(string) && typeof(T) != typeof(object)) {
                         Debug.LogWarning($"PlayFlow API: Received empty JSON response for type {typeof(T).Name}, though success code {request.responseCode} was returned. URL: {url}");
                    }

                    try
                    {
                        // Use Newtonsoft.Json for deserializing successful responses
                        return JsonConvert.DeserializeObject<T>(responseJson);
                    }
                    catch (JsonException ex) 
                    {
                        Debug.LogError($"PlayFlow API: Failed to parse successful JSON response with Newtonsoft.Json for {typeof(T).Name} from '{responseJson}'. Error: {ex.Message}. URL: {url}");
                        throw new PlayFlowApiException($"Failed to parse response with Newtonsoft.Json: {ex.Message}", request.responseCode, responseJson, ex);
                    }
                    catch (Exception ex)
                    {
                         Debug.LogError($"PlayFlow API: An unexpected error occurred while parsing successful JSON response for {typeof(T).Name} from '{responseJson}'. Error: {ex.Message}. URL: {url}");
                        throw new PlayFlowApiException($"Unexpected error parsing response: {ex.Message}", request.responseCode, responseJson, ex);
                    }
                }
            }
        }

        // ------------- Server Endpoints -------------

        /// <summary>
        /// Retrieves a list of game servers for the authenticated project.
        /// </summary>
        /// <param name="includeLaunching">Include servers in 'launching' state. Default is false.</param>
        /// <returns>A ServerList object containing server details.</returns>
        /// <exception cref="PlayFlowApiException">Thrown if the API request fails.</exception>
        public async Task<ServerList> ListServersAsync(bool includeLaunching = false)
        {
            string endpoint = $"/v2/servers/?include_launching={includeLaunching.ToString().ToLower()}";
            return await SendRequestAsync<ServerList>(endpoint, UnityWebRequest.kHttpVerbGET);
        }

        /// <summary>
        /// Provisions and starts a new game server instance based on the provided configuration.
        /// </summary>
        /// <param name="serverData">Configuration for the new server.</param>
        /// <returns>A ServerStartResponse object with details of the launched server.</returns>
        /// <exception cref="ArgumentNullException">Thrown if serverData is null.</exception>
        /// <exception cref="ArgumentException">Thrown if required fields in serverData (name, region) are missing.</exception>
        /// <exception cref="PlayFlowApiException">Thrown if the API request fails.</exception>
        public async Task<ServerStartResponse> StartServerAsync(ServerCreateRequest serverData)
        {
            if (serverData == null) throw new ArgumentNullException(nameof(serverData));
            if (string.IsNullOrEmpty(serverData.name)) 
                throw new ArgumentException("Server name (serverData.name) is required.", nameof(serverData.name));
            if (string.IsNullOrEmpty(serverData.region)) 
                throw new ArgumentException("Server region (serverData.region) is required.", nameof(serverData.region));
            
            return await SendRequestAsync<ServerStartResponse>("/v2/servers/start", UnityWebRequest.kHttpVerbPOST, serverData);
        }

        /// <summary>
        /// Stops the specified game server instance.
        /// </summary>
        /// <param name="instanceId">Unique identifier (UUID) of the server instance to stop.</param>
        /// <returns>A ServerStopResponse object indicating the status of the operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if instanceId is null or empty.</exception>
        /// <exception cref="PlayFlowApiException">Thrown if the API request fails.</exception>
        public async Task<ServerStopResponse> StopServerAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) throw new ArgumentNullException(nameof(instanceId));
            string endpoint = $"/v2/servers/{instanceId}";
            return await SendRequestAsync<ServerStopResponse>(endpoint, UnityWebRequest.kHttpVerbDELETE);
        }

        /// <summary>
        /// Retrieves details for a specific game server by its ID.
        /// </summary>
        /// <param name="instanceId">Unique identifier (UUID) of the server instance to retrieve.</param>
        /// <returns>An InstanceData object with server details.</returns>
        /// <exception cref="ArgumentNullException">Thrown if instanceId is null or empty.</exception>
        /// <exception cref="PlayFlowApiException">Thrown if the API request fails.</exception>
        public async Task<InstanceData> GetServerDetailsAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) throw new ArgumentNullException(nameof(instanceId));
            string endpoint = $"/v2/servers/{instanceId}";
            return await SendRequestAsync<InstanceData>(endpoint, UnityWebRequest.kHttpVerbGET);
        }

        /// <summary>
        /// Updates server status and IP. Typically called by a game server instance itself upon successful launch.
        /// </summary>
        /// <param name="instanceId">Unique identifier (UUID) of the server instance reporting its status.</param>
        /// <param name="serverStatus">The status the server is reporting (e.g., using InstanceStates.Running).</param>
        /// <returns>An InstanceData object with the updated server details.</returns>
        /// <exception cref="ArgumentNullException">Thrown if instanceId or serverStatus is null or empty.</exception>
        /// <exception cref="PlayFlowApiException">Thrown if the API request fails.</exception>
        public async Task<InstanceData> UpdateServerStatusAsync(string instanceId, string serverStatus)
        {
            if (string.IsNullOrEmpty(instanceId)) throw new ArgumentNullException(nameof(instanceId));
            if (string.IsNullOrEmpty(serverStatus)) throw new ArgumentNullException(nameof(serverStatus));

            string endpoint = $"/v2/servers/{instanceId}/update";
            var headers = new Dictionary<string, string>
            {
                { "X-Server-Status", serverStatus }
            };
            // OpenAPI spec for this PUT endpoint does not define a requestBody.
            return await SendRequestAsync<InstanceData>(endpoint, UnityWebRequest.kHttpVerbPUT, payload: null, additionalHeaders: headers);
        }
    }

    /// <summary>
    /// Custom exception for errors encountered while interacting with the PlayFlow API.
    /// </summary>
    public class PlayFlowApiException : Exception
    {
        /// <summary>Gets the HTTP status code of the error response.</summary>
        public long StatusCode { get; }
        /// <summary>Gets the raw response body, if available.</summary>
        public string ResponseBody { get; }

        public PlayFlowApiException(string message, long statusCode, string responseBody) : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
        public PlayFlowApiException(string message, long statusCode, string responseBody, Exception inner) : base(message, inner)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
    
    /// <summary>
    /// Provides extension methods for UnityWebRequest to support Task-based asynchronous operations.
    /// </summary>
    internal static class UnityWebRequestExtensions
    {
        /// <summary>
        /// Sends the UnityWebRequest and returns a Task that completes when the operation finishes.
        /// </summary>
        public static Task<UnityWebRequest> SendWebRequestAsync(this UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var operation = request.SendWebRequest(); // Initiates the web request and gets the operation
            operation.completed += (asyncOp) => // asyncOp is the UnityWebRequestAsyncOperation
            {
                tcs.SetResult(request); // 'request' is the UnityWebRequest itself
            };
            return tcs.Task;
        }
    }
} 