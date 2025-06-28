using UnityEngine;
using Newtonsoft.Json.Linq; // Required for JObject
using System;                 // Required for TimeSpan, Exception
using System.Collections.Generic; // Required for List, Dictionary
using System.Threading.Tasks;   // Required for Task
using System.Threading;       // Required for CancellationTokenSource
using PlayFlow; // <--- ADD THIS USING DIRECTIVE

public class MatchmakerHelloWorld : MonoBehaviour
{
    [Header("Matchmaking Settings")]
    [Tooltip("The name of the matchmaker configuration to use (from your PlayFlow project settings).")]
    [SerializeField] private string MatchmakerName = "DefaultMatchmaker"; // Example matchmaker name
    [Tooltip("A unique ID for the player entering matchmaking.")]
    [SerializeField] private string PlayerId = "TestPlayer123";
    [Tooltip("Maximum time (in seconds) to wait for a match.")]
    [SerializeField] private int MatchmakingTimeoutSeconds = 30;

    [Header("Assign PlayFlowMatchmakerManager")][Tooltip("Drag your PlayFlowMatchmakerManager GameObject here.")]
    public PlayFlow.PlayFlowMatchmakerManager matchmakerManager; // Assign this in the Unity Inspector

    private CancellationTokenSource _cts;

    // Track the current ticket ID so we can leave the match later
    private string _currentTicketId;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("MatchmakerHelloWorld.cs Started - Looking for PlayFlowMatchmakerManager...");

        if (matchmakerManager == null)
        {
            Debug.LogError("[MatchmakerHelloWorld] PlayFlowMatchmakerManager is not assigned in the Inspector! Please assign it.");
            enabled = false; // Disable the script if the manager isn't assigned
            return;
        }
        
        // _matchmakerSDK = new PlayFlow.PlayFlowMatchmakerManager(ApiBaseUrl, ApiKey); // Old instantiation removed
        // Now, matchmakerManager is the SDK instance we use.
        Debug.Log("[MatchmakerHelloWorld] PlayFlowMatchmakerManager found and assigned.");

        // Example: Automatically start matchmaking when the script starts.
        // You might want to trigger this from a UI button or other game event.
        // Make sure MatchmakerName and PlayerId are set in the Inspector before calling this.
        if (!string.IsNullOrEmpty(MatchmakerName) && !string.IsNullOrEmpty(PlayerId))
        {
            StartMatchmakingProcess();
        }
        else
        {
            Debug.LogWarning("[MatchmakerHelloWorld] MatchmakerName or PlayerId is not set in the Inspector. Matchmaking will not start automatically.");
        }
    }

    public async void StartMatchmakingProcess()
    {
        if (matchmakerManager == null)
        {
            Debug.LogError("[MatchmakerHelloWorld] Cannot start matchmaking, PlayFlowMatchmakerManager is not assigned.");
            return;
        }

        Debug.Log($"[MatchmakerHelloWorld] Starting matchmaking for Player ID: {PlayerId} in Matchmaker: {MatchmakerName}");

        _cts = new CancellationTokenSource();
        try
        {
            var matchRequest = new PlayFlow.PlayFlowMatchmakerManager.MatchRequest(MatchmakerName, PlayerId)
            {
                // Optional: Populate other request.Regions, request.Elo, request.PreferredModes, request.CustomFields here
            };

            // Use the public matchmakerManager field directly
            JObject matchData = await matchmakerManager.FindMatchAsync(matchRequest, TimeSpan.FromSeconds(MatchmakingTimeoutSeconds), _cts.Token);
            Debug.Log($"[MatchmakerHelloWorld] Match Found for ticket {matchData?["ticket_id"]}: {matchData}");
            
            // TODO: Handle successful match (e.g., connect to server using matchData details)
            // Example: string connectCode = matchData?["match"]?["server"]?["connect_code"]?.ToString();

            // Save the ticket ID so we can leave the match later
            _currentTicketId = ExtractTicketId(matchData);
            Debug.Log($"Saved ticket ID: {_currentTicketId} for later use");

            // Example of extracting server details from the matched ticket
            ExtractAndLogServerDetails(matchData);
        }
        catch (TimeoutException)
        {
            Debug.LogWarning("Matchmaking timed out.");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Matchmaking was canceled.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"An error occurred during matchmaking: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
        }
    }

    private void ExtractAndLogServerDetails(JObject matchedTicket)
    {
        JToken matchInfo = matchedTicket?["match"];
        if (matchInfo == null)
        {
            Debug.LogError("No 'match' object found in the ticket details.");
            return;
        }

        JToken serverInfo = matchInfo["server"];
        if (serverInfo == null)
        {
            Debug.LogError("No 'server' details found in the match object.");
            return;
        }

        // Check for network_ports array
        JArray networkPorts = serverInfo["network_ports"] as JArray;
        if (networkPorts == null || networkPorts.Count == 0)
        {
            Debug.LogError("No 'network_ports' found in server details or array is empty.");
            return;
        }

        string connectCode = serverInfo["connect_code"]?.ToString();
        if (!string.IsNullOrEmpty(connectCode))
        {
            Debug.Log($"Server Connect Code: {connectCode}");
        }

        Debug.Log($"Server has {networkPorts.Count} port mappings:");
        
        foreach (JToken portMapping in networkPorts)
        {
            string name = portMapping["name"]?.ToString();
            string host = portMapping["host"]?.ToString();
            int? externalPort = portMapping["external_port"]?.ToObject<int?>();
            string protocol = portMapping["protocol"]?.ToString();
            bool? tlsEnabled = portMapping["tls_enabled"]?.ToObject<bool?>();

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(host) && externalPort.HasValue)
            {
                string tlsInfo = tlsEnabled == true ? "TLS Enabled" : "TLS Disabled";
                Debug.Log($"Port Mapping: {name} - {host}:{externalPort.Value} ({protocol?.ToUpper() ?? "unknown"}) - {tlsInfo}");
                
                // Example of how to format a connection string based on the port mapping
                string connectionString = $"{host}:{externalPort.Value}";
                Debug.Log($"Connection String for {name}: {connectionString}");
            }
            else
            {
                Debug.LogWarning($"Incomplete port mapping data - Name: {name ?? "missing"}, Host: {host ?? "missing"}, External Port: {(externalPort.HasValue ? externalPort.Value.ToString() : "missing")}");
            }
        }

        // If you need to grab the first connection automatically
        if (networkPorts.Count > 0)
        {
            JToken primaryPort = networkPorts[0];
            string primaryHost = primaryPort["host"]?.ToString();
            int? primaryExternalPort = primaryPort["external_port"]?.ToObject<int?>();
            string primaryName = primaryPort["name"]?.ToString();
            
            if (!string.IsNullOrEmpty(primaryHost) && primaryExternalPort.HasValue)
            {
                Debug.Log($"Primary connection ({primaryName}): {primaryHost}:{primaryExternalPort.Value}");
                // TODO: Add your logic to connect to the game server using primaryHost and primaryExternalPort
                // Example: ConnectToServer(primaryHost, primaryExternalPort.Value, primaryName);
            }
        }
    }

    public void CancelCurrentMatchmaking()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            Debug.Log("Canceling matchmaking search...");
            _cts.Cancel();
        }
        else
        {
            Debug.Log("No active matchmaking process to cancel.");
        }
    }

    void OnDestroy()
    {
        // Ensure matchmaking is canceled if the GameObject is destroyed
        CancelCurrentMatchmaking();
    }

    // Update is called once per frame
    void Update()
    {
        // Example: Press 'C' to cancel matchmaking if it's running
        if (Input.GetKeyDown(KeyCode.C))
        {
            CancelCurrentMatchmaking();
        }

        // Example: Press 'M' to start matchmaking if it's not running
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (_cts == null || _cts.IsCancellationRequested)
            {
                StartMatchmakingProcess();
            }
            else
            {
                Debug.LogWarning("Matchmaking is already in progress. Press 'C' to cancel first.");
            }
        }

        // Example: Press 'L' to leave current match (if in one)
        if (Input.GetKeyDown(KeyCode.L) && _currentTicketId != null)
        {
            LeaveCurrentMatch();
        }
    }

    // Example helper method to extract the ticket ID from a matched ticket JObject
    private string ExtractTicketId(JObject matchedTicket)
    {
        return matchedTicket?["ticket_id"]?.ToString();
    }

    // Call this method to leave a match after you've joined one
    public async void LeaveCurrentMatch()
    {
        if (string.IsNullOrEmpty(_currentTicketId))
        {
            Debug.LogWarning("No active match to leave. Must join a match first.");
            return;
        }

        try
        {
            Debug.Log($"Attempting to leave match with ticket ID: {_currentTicketId}...");
            JObject response = await matchmakerManager.LeaveMatchAsync(_currentTicketId, MatchmakerName);
            
            string status = response?["status"]?.ToString();
            string matchId = response?["match_id"]?.ToString();
            
            Debug.Log($"Successfully left match. Ticket status: {status}");
            if (!string.IsNullOrEmpty(matchId))
            {
                Debug.Log($"Match ID: {matchId}, Match status: {response?["match_status"]}");
            }
            
            _currentTicketId = null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error leaving match: {ex.Message}");
        }
    }
}
