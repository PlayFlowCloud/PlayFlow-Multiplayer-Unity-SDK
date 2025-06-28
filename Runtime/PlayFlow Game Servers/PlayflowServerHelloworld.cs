// PlayflowServerHelloworld.cs
// This script demonstrates basic interactions with the PlayFlow Server API.

using UnityEngine;
using System.Threading.Tasks;
using PlayFlow.SDK.Servers; // Import the namespace where PlayflowServerApiClient and models are defined
using System.Collections.Generic; // Required for Dictionary
using Newtonsoft.Json; // Required for JsonConvert if you want to pretty print JSON

public class PlayflowServerHelloworld : MonoBehaviour
{
    [Header("PlayFlow API Configuration")]
    [Tooltip("Your PlayFlow API Key. Get this from your PlayFlow dashboard.")]
    public string playflowApiKey = "YOUR_API_KEY_HERE"; // IMPORTANT: Replace with your actual API key

    private PlayflowServerApiClient _apiClient;
    private string _lastStartedInstanceId = null; // To store the ID of the server started by this script

    void Start()
    {
        if (string.IsNullOrEmpty(playflowApiKey) || playflowApiKey == "YOUR_API_KEY_HERE")
        {
            Debug.LogError("PlayFlow API Key is not set in the Inspector for PlayflowServerHelloworld script. Please set it to run API commands.");
            enabled = false; // Disable the script if API key is not set
            return;
        }
        _apiClient = new PlayflowServerApiClient(playflowApiKey);
        Debug.Log("PlayflowServerApiClient initialized. Press S to Start, D to Stop, L to List servers.");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            StartNewServer();
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            StopLastStartedServer();
        }
        else if (Input.GetKeyDown(KeyCode.L))
        {
            ListAllServers();
        }
    }

    private async void StartNewServer()
    {
        Debug.Log("Attempting to start a new server...");

        // IMPORTANT: Adjust these parameters as needed, especially 'region'.
        // Ensure the region is valid for your PlayFlow project and desired provider.
        var serverCreateRequest = new ServerCreateRequest
        {
            name = $"MyExampleServer_{System.DateTime.UtcNow:yyyyMMddHHmmss}",
            region = "us-east", // EXAMPLE REGION: Change to a valid region for your project!
            compute_size = ComputeSizes.Small,
            version_tag = "default", // Optional: specify a build version tag if needed
            // ttl = 300, // Optional: Time To Live in seconds (e.g., 5 minutes for testing)
            custom_data = new Dictionary<string, object> 
            {
                { "purpose", "helloworld_test" },
                { "started_by_script", true }
            }
        };

        try
        {
            Debug.Log($"Sending StartServer request: {JsonConvert.SerializeObject(serverCreateRequest, Formatting.Indented)}");
            ServerStartResponse response = await _apiClient.StartServerAsync(serverCreateRequest);
            
            _lastStartedInstanceId = response.instance_id; // Store for potential stop operation
            
            Debug.Log($"Server Start successful! Response:\n{JsonConvert.SerializeObject(response, Formatting.Indented)}");
            Debug.Log($"Instance ID: {response.instance_id}, Name: {response.name}, Status: {response.status}, Region: {response.region}");
            if (response.network_ports != null && response.network_ports.Count > 0)
            {
                foreach(var port in response.network_ports)
                {
                    Debug.Log($"  Port Mapping: Name={port.name}, Host={port.host}, External={port.external_port}, Internal={port.internal_port}, Protocol={port.protocol}");
                }
            }
        }
        catch (PlayFlowApiException apiEx)
        {
            Debug.LogError($"PlayFlow API Error starting server: {apiEx.Message}\nStatus Code: {apiEx.StatusCode}\nResponse Body: {apiEx.ResponseBody}");
            _lastStartedInstanceId = null; // Clear if start failed
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"An unexpected error occurred starting server: {ex.Message}\n{ex.StackTrace}");
            _lastStartedInstanceId = null; // Clear if start failed
        }
    }

    private async void StopLastStartedServer()
    {
        if (string.IsNullOrEmpty(_lastStartedInstanceId))
        {
            Debug.LogWarning("No server instance ID stored from a previous start operation. Cannot stop server. Start one with 'S' first.");
            return;
        }

        Debug.Log($"Attempting to stop server with Instance ID: {_lastStartedInstanceId}...");
        try
        {
            ServerStopResponse response = await _apiClient.StopServerAsync(_lastStartedInstanceId);
            Debug.Log($"Server Stop successful! Response:\n{JsonConvert.SerializeObject(response, Formatting.Indented)}");
            Debug.Log($"Status for stopping {_lastStartedInstanceId}: {response.status}");
            // Optionally clear the ID after successful stop, or if it implies it's no longer valid.
            // _lastStartedInstanceId = null; 
        }
        catch (PlayFlowApiException apiEx)
        {
            Debug.LogError($"PlayFlow API Error stopping server {_lastStartedInstanceId}: {apiEx.Message}\nStatus Code: {apiEx.StatusCode}\nResponse Body: {apiEx.ResponseBody}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"An unexpected error occurred stopping server {_lastStartedInstanceId}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void ListAllServers()
    {
        Debug.Log("Attempting to list all servers...");
        try
        {
            ServerList response = await _apiClient.ListServersAsync(includeLaunching: true); // Set to true to see launching servers
            Debug.Log($"Server List successful! Response:\n{JsonConvert.SerializeObject(response, Formatting.Indented)}");
            Debug.Log($"Total Servers: {response.total_servers}");
            if (response.servers != null && response.servers.Count > 0)
            {
                foreach (var server in response.servers)
                {
                    Debug.Log($"--- Server Found ---");
                    Debug.Log($"  Instance ID: {server.instance_id}");
                    Debug.Log($"  Name: {server.name}");
                    Debug.Log($"  Status: {server.status}");
                    Debug.Log($"  Region: {server.region}");

                    if (server.network_ports != null && server.network_ports.Count > 0)
                    {
                        Debug.Log("  Network Ports:");
                        foreach (var port in server.network_ports)
                        {
                            Debug.Log($"    Port Name: \"{port.name}\" - Connection: {port.host}:{port.external_port} ({port.protocol.ToUpper()})");
                        }
                    }
                    else
                    {
                        Debug.Log("  Network Ports: None reported.");
                    }

                    if (server.custom_data != null && server.custom_data.Count > 0)
                    {
                         Debug.Log($"    Custom Data: {JsonConvert.SerializeObject(server.custom_data)}");
                    }
                    else
                    {
                        Debug.Log("    Custom Data: None");
                    }
                    Debug.Log($"----------------------");
                }
            }
            else
            {
                Debug.Log("No active servers found.");
            }
        }
        catch (PlayFlowApiException apiEx)
        {
            Debug.LogError($"PlayFlow API Error listing servers: {apiEx.Message}\nStatus Code: {apiEx.StatusCode}\nResponse Body: {apiEx.ResponseBody}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"An unexpected error occurred listing servers: {ex.Message}\n{ex.StackTrace}");
        }
    }
} 