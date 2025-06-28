// PlayFlowMatchmakerSDK.cs
// This script requires Newtonsoft.Json and depends on MatchmakerClient.cs

using UnityEngine;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace PlayFlow
{
    public class PlayFlowMatchmakerManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string baseUrl = "https://backend.computeflow.cloud"; // Updated default URL
        [SerializeField] private string apiKey = "your_api_key_here";     // Example default, adjust as needed

        private MatchmakerClient _matchmakerClient;
        private const int DefaultPollIntervalMilliseconds = 2000; // 2 seconds

        public class MatchRequest
        {
            public string MatchmakerName { get; }
            public string PlayerId { get; }
            public List<string> Regions { get; set; } = null;
            public int? Elo { get; set; } = 1000;
            public List<string> PreferredModes { get; set; } = null;
            public Dictionary<string, JToken> CustomFields { get; set; } = null;

            public MatchRequest(string matchmakerName, string playerId)
            {
                if (string.IsNullOrEmpty(matchmakerName))
                    throw new ArgumentNullException(nameof(matchmakerName));
                if (string.IsNullOrEmpty(playerId))
                    throw new ArgumentNullException(nameof(playerId));

                MatchmakerName = matchmakerName;
                PlayerId = playerId;
            }
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                Debug.LogError("[PlayFlowMatchmakerManager] Base URL is not set in the Inspector.");
                enabled = false; // Disable component if not configured
                return;
            }
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[PlayFlowMatchmakerManager] API Key is not set in the Inspector.");
                enabled = false; // Disable component if not configured
                return;
            }
            _matchmakerClient = new MatchmakerClient(baseUrl, apiKey);
            Debug.Log($"[PlayFlowMatchmakerManager] Initialized with Base URL: {baseUrl}");
        }

        /// <summary>
        /// Creates a ticket and polls for match status until a match is found or timeout occurs.
        /// </summary>
        /// <param name="request">The match request details.</param>
        /// <param name="timeout">Maximum time to wait for a match.</param>
        /// <param name="cancellationToken">Optional cancellation token to abort the operation.</param>
        /// <param name="pollIntervalMilliseconds">How often to poll for status updates.</param>
        /// <returns>The JObject of the matched ticket, including match details.</returns>
        /// <exception cref="TimeoutException">Thrown if no match is found within the timeout period.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the cancellationToken is triggered.</exception>
        /// <exception cref="Exception">Thrown for other API or network errors.</exception>
        public async Task<JObject> FindMatchAsync(
            MatchRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default,
            int pollIntervalMilliseconds = DefaultPollIntervalMilliseconds)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (pollIntervalMilliseconds <= 0)
                pollIntervalMilliseconds = DefaultPollIntervalMilliseconds;

            Debug.Log($"[PlayFlowMatchmakerSDK] Creating ticket for Player ID: {request.PlayerId} in Matchmaker: {request.MatchmakerName}");

            JObject ticketCreationResponse = await _matchmakerClient.CreateTicketAsync(
                request.MatchmakerName,
                request.PlayerId,
                request.Regions,
                request.Elo,
                request.PreferredModes,
                request.CustomFields,
                cancellationToken
            );

            string ticketId = ticketCreationResponse?["ticket_id"]?.ToString();
            if (string.IsNullOrEmpty(ticketId))
            {
                throw new Exception("[PlayFlowMatchmakerSDK] Failed to create ticket or retrieve ticket_id.");
            }

            Debug.Log($"[PlayFlowMatchmakerSDK] Ticket {ticketId} created. Polling for match status...");

            DateTime startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                JObject ticketStatusResponse;
                try
                {
                    ticketStatusResponse = await _matchmakerClient.GetTicketOrPlayerStatusAsync(ticketId, "ticket", request.MatchmakerName, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Debug.Log($"[PlayFlowMatchmakerSDK] Polling for ticket {ticketId} was canceled.");
                    throw;
                }
                catch (Exception ex)
                {
                    // Log API errors during polling but continue polling unless it's a fatal error type or timeout
                    Debug.LogWarning($"[PlayFlowMatchmakerSDK] Error polling ticket {ticketId} status: {ex.Message}. Retrying...");
                    await Task.Delay(pollIntervalMilliseconds, cancellationToken);
                    continue;
                }
                
                string status = ticketStatusResponse?["status"]?.ToString();
                Debug.Log($"[PlayFlowMatchmakerSDK] Ticket {ticketId} status: {status}");

                if (status == "matched")
                {
                    Debug.Log($"[PlayFlowMatchmakerSDK] Match found for ticket {ticketId}!");
                    // The ticketStatusResponse should contain the full ticket data, including match details
                    return ticketStatusResponse; 
                }

                // Wait for the poll interval before checking again
                await Task.Delay(pollIntervalMilliseconds, cancellationToken);
            }

            Debug.LogWarning($"[PlayFlowMatchmakerSDK] Timeout reached for ticket {ticketId} after {timeout.TotalSeconds} seconds.");
            throw new TimeoutException($"[PlayFlowMatchmakerSDK] Matchmaking timed out for ticket {ticketId} after {timeout.TotalSeconds} seconds.");
        }

        /// <summary>
        /// Leaves a match or cancels a ticket.
        /// </summary>
        /// <param name="ticketId">The ID of the ticket to cancel or use to leave a match.</param>
        /// <param name="matchmakerName">The name of the matchmaker.</param>
        /// <param name="cancellationToken">Optional cancellation token to abort the operation.</param>
        /// <returns>A JObject containing the response with status information.</returns>
        public async Task<JObject> LeaveMatchAsync(string ticketId, string matchmakerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(ticketId))
                throw new ArgumentNullException(nameof(ticketId));
            if (string.IsNullOrEmpty(matchmakerName))
                throw new ArgumentNullException(nameof(matchmakerName));

            Debug.Log($"[PlayFlowMatchmakerSDK] Canceling ticket {ticketId} or leaving associated match...");
            JObject response = await _matchmakerClient.CancelTicketOrLeaveMatchAsync(ticketId, matchmakerName, cancellationToken);
            Debug.Log($"[PlayFlowMatchmakerSDK] Ticket {ticketId} canceled/abandoned with status: {response?["status"]}");
            
            return response;
        }

        /// <summary>
        /// Gets the underlying MatchmakerClient instance for direct API access if needed.
        /// </summary>
        /// <returns>The MatchmakerClient instance.</returns>
        public MatchmakerClient GetMatchmakerClient()
        {
            return _matchmakerClient;
        }
    }
} 