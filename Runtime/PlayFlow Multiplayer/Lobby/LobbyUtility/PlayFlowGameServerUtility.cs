using UnityEngine;
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq; // Added for Linq FirstOrDefault

namespace PlayFlow
{
    // New struct to hold network port details
    public struct NetworkPortDetails
    {
        public string Name { get; }
        public string Host { get; }
        public int ExternalPort { get; }
        public string Protocol { get; }
        public int InternalPort { get; }

        public NetworkPortDetails(string name, string host, int externalPort, string protocol, int internalPort)
        {
            Name = name;
            Host = host;
            ExternalPort = externalPort;
            Protocol = protocol;
            InternalPort = internalPort;
        }

        public override string ToString()
        {
            return $"Name: {Name}, Host: {Host}, ExternalPort: {ExternalPort}, Protocol: {Protocol}, InternalPort: {InternalPort}";
        }
    }

    public class PlayFlowGameServerUtility
    {
        private readonly PlayFlowLobbyEvents.IndividualLobbyEvents individualLobbyEvents;
        private readonly bool debugLogging;
        
        public PlayFlowGameServerUtility(
            PlayFlowLobbyEvents.IndividualLobbyEvents individualLobbyEvents,
            bool debugLogging)
        {
            this.individualLobbyEvents = individualLobbyEvents;
            this.debugLogging = debugLogging;
        }
        
        /// <summary>
        /// Asynchronously waits until the game server for the current lobby reports its status as "running".
        /// </summary>
        /// <param name="timeoutSeconds">Maximum time to wait in seconds.</param>
        /// <returns>True if the game server becomes "running" within the timeout, false otherwise.</returns>
        public async Task<bool> WaitForGameServerRunningAsync(Lobby currentLobby, float timeoutSeconds = 30f)
        {
            if (currentLobby == null)
            {
                if (debugLogging) Debug.LogWarning("[WaitForGameServerRunningAsync] Not in a lobby. Cannot wait for game server.");
                return false;
            }

            // Check initial state: If server is already running, no need to wait.
            if (currentLobby?.gameServer is Dictionary<string, object> initialGsInfo && 
                initialGsInfo.TryGetValue("status", out object initialStatusObj) && 
                initialStatusObj?.ToString() == "running")
            {
                if (debugLogging) Debug.Log("[WaitForGameServerRunningAsync] Game server is already 'running'.");
                return true;
            }

            if (debugLogging) Debug.Log($"[WaitForGameServerRunningAsync] Waiting for game server to be 'running' for lobby {currentLobby?.id}. Timeout: {timeoutSeconds}s");

            var tcs = new TaskCompletionSource<bool>();
            var timeoutCts = new CancellationTokenSource();

            // Define the event handler as a local function
            void LobbyUpdateHandler(Lobby updatedLobby)
            {
                // We rely on onLobbyUpdated to only fire for *our* current lobby
                if (updatedLobby?.gameServer is not Dictionary<string, object> gsInfo) return;

                if (gsInfo.TryGetValue("status", out object statusObj))
                {
                    string status = statusObj?.ToString();
                    if (debugLogging) Debug.Log($"[WaitForGameServerRunningAsync] Lobby {updatedLobby.id} updated. Game server status: {status}");
                    
                    if (status == "running")
                    {
                        if (debugLogging) Debug.Log($"[WaitForGameServerRunningAsync] Game server for lobby {updatedLobby.id} is now 'running'. Completing wait.");
                        tcs.TrySetResult(true); // Signal successful completion
                    }
                }
            }

            individualLobbyEvents.onLobbyUpdated.AddListener(LobbyUpdateHandler);

            try
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), timeoutCts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == tcs.Task)
                {
                    timeoutCts.Cancel(); // Cancel the timeout as we completed successfully
                    return await tcs.Task; // Return true (or propagate exception if tcs was faulted)
                }
                else // Timeout occurred
                {
                    if (debugLogging) Debug.LogWarning($"[WaitForGameServerRunningAsync] Timed out after {timeoutSeconds}s waiting for game server of lobby {currentLobby?.id} to be 'running'.");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                // This typically occurs if the timeoutTask itself is cancelled externally before completion,
                // or if tcs.Task was somehow cancelled and awaited.
                if (debugLogging) Debug.LogWarning("[WaitForGameServerRunningAsync] Wait operation was cancelled (likely timeout or external cancellation).");
                return false;
            }
            finally
            {
                individualLobbyEvents.onLobbyUpdated.RemoveListener(LobbyUpdateHandler);
                if (!timeoutCts.IsCancellationRequested) // Ensure the timeout CancellationTokenSource is cancelled
                {
                    timeoutCts.Cancel();
                }
                timeoutCts.Dispose(); // Dispose of the CancellationTokenSource
            }
        }

        // Method to print game server details, similar to MatchmakerHelloWorld.cs
        public void PrintGameServerDetails(Lobby currentLobby)
        {
            if (currentLobby == null)
            {
                Debug.LogWarning("[PrintGameServerDetails] Cannot print details: Not currently in a lobby (currentLobby is null).");
                return;
            }

            object gameServerObj = currentLobby.gameServer;
            if (gameServerObj == null)
            {
                Debug.LogWarning("[PrintGameServerDetails] Game server information is not available or is null for the current lobby.");
                return;
            }
            
            JObject gameServerJson;
            // The gameServer property is always Dictionary<string, object>, not JObject
            if (gameServerObj is Dictionary<string, object> dict)
            {
                if (debugLogging) Debug.Log("[PrintGameServerDetails] Converting gameServer Dictionary to JObject for parsing.");
                try { gameServerJson = JObject.FromObject(gameServerObj); }
                catch (Exception ex) { Debug.LogError($"[PrintGameServerDetails] Failed to convert game server info (Dictionary<string, object>) to JObject: {ex.Message}. Raw data: {JsonConvert.SerializeObject(gameServerObj)}"); return; }
            }
            else
            {
                Debug.LogError($"[PrintGameServerDetails] Game server information is of an unexpected type: {gameServerObj.GetType().FullName}. Expected Dictionary<string, object>. Raw data: {JsonConvert.SerializeObject(gameServerObj)}");
                return;
            }

            Debug.Log("========== GAME SERVER DETAILS ==========");

            // Print all top-level properties from JObject
            foreach (var property in gameServerJson.Properties())
            {
                if (property.Name.ToLower() != "network_ports" && property.Name.ToLower() != "custom_data") // Case-insensitive compare
                {
                    Debug.Log($"{property.Name}: {property.Value?.ToString()}");
                }
            }

            // Handle network_ports (expecting JArray)
            JToken networkPortsToken = gameServerJson.GetValue("network_ports", StringComparison.OrdinalIgnoreCase); // Case-insensitive get
            if (networkPortsToken is JArray networkPortsArray)
            {
                Debug.Log("Network Ports:");
                if (networkPortsArray.Count == 0)
                {
                    Debug.Log("  (No network ports listed)");
                }
                foreach (JToken portToken in networkPortsArray)
                {
                    if (portToken is JObject portObj)
                    {
                        string name = portObj["name"]?.ToString();
                        string host = portObj["host"]?.ToString();
                        // MatchmakerHelloWorld.cs uses ToObject<int?>() which is more type-safe if the port is always an int.
                        // Using ToString() here for general compatibility as in your example.
                        string externalPort = portObj["external_port"]?.ToString();
                        string internalPort = portObj["internal_port"]?.ToString();
                        string protocol = portObj["protocol"]?.ToString();
                        bool? tlsEnabled = portObj["tls_enabled"]?.ToObject<bool?>(); 
                        string tlsInfo = tlsEnabled.HasValue ? (tlsEnabled.Value ? "TLS Enabled" : "TLS Disabled") : "TLS N/A";

                        Debug.Log($"  - Name: {name ?? "N/A"}, Host: {host ?? "N/A"}, External Port: {externalPort ?? "N/A"}, Internal Port: {internalPort ?? "N/A"}, Protocol: {protocol ?? "N/A"}, TLS: {tlsInfo}");
                    }
                    else
                    {
                        Debug.LogWarning($"  - Found a network port item that is not a JSON object: {portToken.GetType().FullName}");
                    }
                }
            }
            else if (networkPortsToken != null && networkPortsToken.Type != JTokenType.Null && networkPortsToken.Type != JTokenType.Undefined)
            {
                Debug.LogWarning($"[PrintGameServerDetails] 'network_ports' field found but is not an array. Type: {networkPortsToken.Type}. Value: {networkPortsToken.ToString(Formatting.None)}");
            }
            else
            {
                Debug.Log("[PrintGameServerDetails] No 'network_ports' field found or it is null/undefined.");
            }

            // Handle custom_data (expecting JObject)
            JToken customDataToken = gameServerJson.GetValue("custom_data", StringComparison.OrdinalIgnoreCase); // Case-insensitive get
            if (customDataToken is JObject customDataObj)
            {
                Debug.Log("Custom Data:");
                if (!customDataObj.HasValues)
                {
                    Debug.Log("  (No custom data entries)");
                }
                foreach (var prop in customDataObj.Properties())
                {
                    Debug.Log($"  - {prop.Name}: {prop.Value?.ToString()}");
                }
            }
            else if (customDataToken != null && customDataToken.Type != JTokenType.Null && customDataToken.Type != JTokenType.Undefined)
            {
                Debug.LogWarning($"[PrintGameServerDetails] 'custom_data' field found but is not an object. Type: {customDataToken.Type}. Value: {customDataToken.ToString(Formatting.None)}");
            }
            else
            {
                Debug.Log("[PrintGameServerDetails] No 'custom_data' field found or it is null/undefined.");
            }

            Debug.Log("======== END GAME SERVER DETAILS ========");
        }

        /// <summary>
        /// Extracts specific network port information from the lobby's game server details.
        /// </summary>
        /// <param name="lobby">The lobby object containing game server data.</param>
        /// <param name="targetInternalPort">The internal port number to search for.</param>
        /// <param name="targetProtocol">Optional: The protocol (e.g., "udp", "tcp") to match. Case-insensitive.</param>
        /// <param name="targetPortName">Optional: The specific name of the port (e.g., "game_udp") to match. Case-insensitive.</param>
        /// <returns>A NetworkPortDetails struct if a match is found; otherwise, null.</returns>
        public NetworkPortDetails? GetNetworkPort(Lobby lobby, int targetInternalPort, string targetProtocol = null, string targetPortName = null)
        {
            if (lobby?.gameServer == null)
            {
                if (debugLogging) Debug.LogWarning("[GetNetworkPort] Lobby or gameServer data is null.");
                return null;
            }

            JObject gameServerJson = null;
            if (lobby.gameServer is Dictionary<string, object> dict)
            {
                gameServerJson = JObject.FromObject(dict);
            }
            else
            {
                if (debugLogging) Debug.LogWarning($"[GetNetworkPort] gameServer data is not a Dictionary<string, object>: {lobby.gameServer.GetType().FullName}");
                return null;
            }

            JToken networkPortsToken = gameServerJson.GetValue("network_ports", StringComparison.OrdinalIgnoreCase);
            if (networkPortsToken is JArray networkPortsArray)
            {
                foreach (JToken portToken in networkPortsArray)
                {
                    if (portToken is JObject portObj)
                    {
                        // Primary match: Internal Port
                        if (portObj["internal_port"]?.ToObject<int>() != targetInternalPort) continue;

                        // Optional match: Protocol (case-insensitive)
                        if (!string.IsNullOrEmpty(targetProtocol) && 
                            !string.Equals(portObj["protocol"]?.ToString(), targetProtocol, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Optional match: Port Name (case-insensitive)
                        if (!string.IsNullOrEmpty(targetPortName) && 
                            !string.Equals(portObj["name"]?.ToString(), targetPortName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // If all conditions met, extract and return details
                        string name = portObj["name"]?.ToString();
                        string host = portObj["host"]?.ToString();
                        string protocol = portObj["protocol"]?.ToString(); // Get the actual protocol for the struct
                        int? externalPort = portObj["external_port"]?.ToObject<int?>();
                        int internalPortVal = portObj["internal_port"].ToObject<int>(); // Already checked it's targetInternalPort

                        if (externalPort.HasValue && !string.IsNullOrEmpty(host))
                        {
                            if(debugLogging) Debug.Log($"[GetNetworkPort] Found matching port: Name='{name}', Host='{host}', ExternalPort={externalPort.Value}, Protocol='{protocol}', InternalPort={internalPortVal}");
                            return new NetworkPortDetails(name, host, externalPort.Value, protocol, internalPortVal);
                        }
                        else
                        {
                             if(debugLogging) Debug.LogWarning($"[GetNetworkPort] Matched port criteria for internalPort {targetInternalPort} but host or external_port was missing/invalid. PortName: {name}, Host: {host}, ExternalPort: {externalPort}");
                        }
                    }
                }
                if (debugLogging) Debug.Log($"[GetNetworkPort] No port found matching InternalPort={targetInternalPort}, Protocol={targetProtocol ?? "any"}, PortName={targetPortName ?? "any"} after checking {networkPortsArray.Count} ports.");
            }
            else
            {
                if (debugLogging) Debug.LogWarning("[GetNetworkPort] 'network_ports' field is missing, null, or not an array in gameServer data.");
            }
            return null;
        }
    }
} 