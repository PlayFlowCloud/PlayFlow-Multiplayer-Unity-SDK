using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace PlayFlow
{
    public class LobbyRefreshManager : MonoBehaviour
    {
        private PlayFlowSettings _settings;
        private LobbyOperations _operations;
        private PlayFlowLobbyManagerV2 _lobbyManager; // Changed from PlayFlowSession
        private PlayFlowEvents _events;
        private Coroutine _refreshCoroutine;
        private LobbySseManager _sseManager;
        private bool _isSSEConnected = false;
        private string _currentSSELobbyId = null;
        private LobbyUpdateDeduplicator _deduplicator = new LobbyUpdateDeduplicator();
        
        public event Action<List<Lobby>> OnLobbyListRefreshed;
        
        public void Initialize(PlayFlowSettings settings, LobbyOperations operations)
        {
            _settings = settings;
            _operations = operations;
            _lobbyManager = PlayFlowLobbyManagerV2.Instance; // Get the singleton instance
            _events = GetComponent<PlayFlowEvents>();
            
            // Initialize SSE manager
            _sseManager = LobbySseManager.Instance;
            
            // Add mobile lifecycle handler if needed
#if UNITY_IOS || UNITY_ANDROID
            if (PlatformSSEHandler.ShouldReconnectOnBackground())
            {
                var lifecycleGO = new GameObject("[MobileLifecycleHandler]");
                lifecycleGO.transform.SetParent(transform);
                lifecycleGO.AddComponent<MobileLifecycleHandler>();
            }
#endif
            
            // Subscribe to SSE events
            _sseManager.OnConnected += HandleSSEConnected;
            _sseManager.OnDisconnected += HandleSSEDisconnected;
            _sseManager.OnLobbyUpdated += HandleSSELobbyUpdate;
            _sseManager.OnLobbyDeleted += HandleSSELobbyDeleted;
            _sseManager.OnError += HandleSSEError;
            
            // Subscribe to session events to know when we join/leave lobbies
            if (_lobbyManager != null)
            {
                _lobbyManager.Events.OnLobbyUpdated.AddListener(HandleSessionLobbyChanged);
            }
            
            // Start the refresh loop here, after settings are assigned
            if (_settings != null && _settings.autoRefresh && _lobbyManager != null && _refreshCoroutine == null)
            {
                _refreshCoroutine = StartCoroutine(RefreshLoop());
            }
        }
        
        private void OnEnable()
        {
            // The loop is now started in Initialize, but we can add a safety check here
            // in case the component is disabled and re-enabled at runtime.
            if (_settings != null && _settings.autoRefresh && _lobbyManager != null && _refreshCoroutine == null)
            {
                _refreshCoroutine = StartCoroutine(RefreshLoop());
            }
        }
        
        private void OnDisable()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
            
            // Disconnect SSE when disabled
            if (_sseManager != null && _currentSSELobbyId != null)
            {
                _sseManager.Disconnect();
                _currentSSELobbyId = null;
                _isSSEConnected = false;
            }
        }
        
        private IEnumerator RefreshLoop()
        {
            var wait = new WaitForSeconds(_settings.refreshInterval);
            
            while (enabled)
            {
                yield return wait;
                
                if (_lobbyManager != null && _lobbyManager.IsInLobby && _lobbyManager.CurrentLobby != null)
                {
                    // Skip regular refresh if SSE is connected
                    bool shouldPoll = !_isSSEConnected || _currentSSELobbyId != _lobbyManager.CurrentLobby.id;
                    
                    if (shouldPoll)
                    {
                        if (_settings.debugLogging)
                        {
                            Debug.Log($"[LobbyRefreshManager] Polling lobby {_lobbyManager.CurrentLobby.id} - SSE Connected: {_isSSEConnected}, SSE Lobby ID: {_currentSSELobbyId}");
                        }
                        yield return RefreshCurrentLobby(_lobbyManager.CurrentLobby.id);
                    }
                    else if (_settings.debugLogging)
                    {
                        Debug.Log($"[LobbyRefreshManager] Skipping regular poll - SSE active for lobby {_lobbyManager.CurrentLobby.id}");
                    }
                }
                else
                {
                    yield return RefreshLobbyList();
                }
            }
        }
        
        private IEnumerator RefreshLobbyList()
        {
            yield return _operations.ListLobbiesCoroutine(
                lobbies => 
                {
                    OnLobbyListRefreshed?.Invoke(lobbies);
                },
                error => 
                {
                     if (_settings.debugLogging)
                    {
                        Debug.LogError($"[LobbyRefreshManager] Failed to refresh lobby list: {error}");
                    }
                }
            );
        }
        
        
        private IEnumerator RefreshCurrentLobby(string lobbyId)
        {
            if (!_isSSEConnected && (_settings?.debugLogging ?? false))
            {
                Debug.LogWarning($"[LobbyRefreshManager] ⚠️ POLLING lobby {lobbyId} via HTTP (SSE not connected)");
            }
            yield return _operations.GetLobbyCoroutine(lobbyId, 
                lobby => 
                {
                    if (_lobbyManager != null && _lobbyManager.CurrentLobby?.id == lobby.id)
                    {
                        // Mark this as a local update to potentially deduplicate SSE
                        _deduplicator.IsDuplicateUpdate(lobby); // Pre-record it
                        _lobbyManager.UpdateCurrentLobby(lobby);
                    }
                },
                error => 
                {
                    if (error.Contains("404") || error.Contains("Not Found"))
                    {
                        // Lobby no longer exists, clear it
                        if (_lobbyManager != null)
                        {
                            _lobbyManager.ClearCurrentLobby();
                        }
                        
                        if (_settings.debugLogging)
                        {
                            Debug.Log($"[LobbyRefreshManager] Lobby {lobbyId} no longer exists");
                        }
                    }
                    else if (_settings.debugLogging)
                    {
                        Debug.LogError($"[LobbyRefreshManager] Failed to refresh lobby: {error}");
                    }
                });
        }
        
        public void ForceRefresh()
        {
            if (_lobbyManager != null && _lobbyManager.IsInLobby && _lobbyManager.CurrentLobby != null)
            {
                StartCoroutine(RefreshCurrentLobby(_lobbyManager.CurrentLobby.id));
            }
            else
            {
                StartCoroutine(RefreshLobbyList());
            }
        }
        
        public void PauseRefresh()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
                
                if (_settings?.debugLogging ?? false)
                {
                    Debug.Log("[LobbyRefreshManager] Refresh paused");
                }
            }
        }
        
        public void ResumeRefresh()
        {
            if (_settings != null && _settings.autoRefresh && _lobbyManager != null && _refreshCoroutine == null)
            {
                _refreshCoroutine = StartCoroutine(RefreshLoop());
                
                if (_settings.debugLogging)
                {
                    Debug.Log("[LobbyRefreshManager] Refresh resumed");
                }
            }
        }
        
        /// <summary>
        /// Mark a lobby update as coming from a local API call to help with deduplication
        /// </summary>
        public void MarkLocalUpdate(Lobby lobby)
        {
            if (lobby != null && _deduplicator != null)
            {
                _deduplicator.IsDuplicateUpdate(lobby); // Pre-record it
                if (_settings?.debugLogging ?? false)
                {
                    Debug.Log($"[LobbyRefreshManager] Marked local API update for lobby {lobby.id}");
                }
            }
        }
        
        // SSE Event Handlers
        
        private void HandleSessionLobbyChanged(Lobby lobby)
        {
            if (lobby == null)
            {
                // Left lobby, disconnect SSE
                if (_currentSSELobbyId != null)
                {
                    if (_settings?.debugLogging ?? false)
                    {
                        Debug.Log($"[LobbyRefreshManager] Left lobby, disconnecting SSE from {_currentSSELobbyId}");
                    }
                    _sseManager?.Disconnect();
                    _currentSSELobbyId = null;
                    _isSSEConnected = false;
                }
            }
            else if (lobby.id != _currentSSELobbyId)
            {
                // Joined a new lobby or changed lobbies
                _currentSSELobbyId = lobby.id;
                _isSSEConnected = false;
                
                // Initialize SSE manager with current session data if not already done
                if (_sseManager != null && _lobbyManager != null && !string.IsNullOrEmpty(_lobbyManager.PlayerId))
                {
                    _sseManager.Initialize(
                        _lobbyManager.PlayerId,
                        _settings.apiKey,
                        _settings.defaultLobbyConfig,
                        _settings.baseUrl
                    );
                    
                    if (_settings?.debugLogging ?? false)
                    {
                        Debug.Log($"[LobbyRefreshManager] Joined lobby {lobby.id}, connecting SSE for player {_lobbyManager.PlayerId}...");
                    }
                    _sseManager.ConnectToLobby(lobby.id);
                }
                else
                {
                    Debug.LogWarning("[LobbyRefreshManager] Cannot connect SSE - session or player ID not ready");
                }
            }
        }
        
        private void HandleSSEConnected()
        {
            _isSSEConnected = true;
            if (_settings?.debugLogging ?? false)
            {
                Debug.Log("[LobbyRefreshManager] ✅ SSE connected successfully - pausing polling for current lobby");
            }
            
            // Force refresh the current lobby state when SSE reconnects to ensure we have latest data
            if (_lobbyManager != null && _lobbyManager.CurrentLobby != null)
            {
                StartCoroutine(RefreshCurrentLobby(_lobbyManager.CurrentLobby.id));
            }
        }
        
        private void HandleSSEDisconnected()
        {
            _isSSEConnected = false;
            
            if (_settings?.debugLogging ?? false)
            {
                Debug.Log("[LobbyRefreshManager] ⚠️ SSE disconnected - resuming polling for lobby updates");
            }
        }
        
        private void HandleSSELobbyUpdate(Lobby lobby)
        {
            // Update the session with the latest lobby data from SSE
            if (_lobbyManager != null && lobby != null && lobby.id == _currentSSELobbyId)
            {
                // Check for duplicate updates (from API response + SSE)
                if (_deduplicator.IsDuplicateUpdate(lobby))
                {
                    if (_settings.debugLogging)
                    {
                        Debug.Log($"[LobbyRefreshManager] Skipping duplicate SSE update for lobby {lobby.id} (API response already processed)");
                    }
                    return;
                }
                
                // Check if this is a server status update from launching to running
                bool wasLaunching = false;
                if (_lobbyManager.CurrentLobby?.gameServer != null && lobby.gameServer != null)
                {
                    var oldStatus = _lobbyManager.CurrentLobby.gameServer.ContainsKey("status") ? 
                        _lobbyManager.CurrentLobby.gameServer["status"]?.ToString() : "";
                    var newStatus = lobby.gameServer.ContainsKey("status") ? 
                        lobby.gameServer["status"]?.ToString() : "";
                    
                    if (oldStatus == "launching" && newStatus == "running")
                    {
                        wasLaunching = true;
                        if (_settings?.debugLogging ?? false)
                        {
                            Debug.Log($"[LobbyRefreshManager] SSE delivered server running status");
                        }
                    }
                }
                
                _lobbyManager.UpdateCurrentLobby(lobby);
                
                if (_settings?.debugLogging ?? false)
                {
                    Debug.Log($"[LobbyRefreshManager] Received SSE update for lobby {lobby.id} - Status: {lobby.status}");
                }
                
                // Fire match running event if server just became ready
                if (wasLaunching && _events != null)
                {
                    if (_settings?.debugLogging ?? false)
                    {
                        Debug.Log($"[LobbyRefreshManager] ✅ Game server is now running! (via SSE)");
                    }
                    _events.InvokeMatchRunning(ExtractConnectionInfo(lobby));
                }
            }
        }
        
        private void HandleSSELobbyDeleted(string lobbyId)
        {
            if (lobbyId == _currentSSELobbyId)
            {
                _lobbyManager?.ClearCurrentLobby();
                _currentSSELobbyId = null;
                _isSSEConnected = false;
                
                if (_settings?.debugLogging ?? false)
                {
                    Debug.Log($"[LobbyRefreshManager] Lobby {lobbyId} was deleted (SSE notification)");
                }
            }
        }
        
        private void HandleSSEError(string error)
        {
            Debug.LogWarning($"[LobbyRefreshManager] ❌ SSE error: {error}");
        }
        
        private ConnectionInfo ExtractConnectionInfo(Lobby lobby)
        {
            if (lobby?.gameServer == null) return new ConnectionInfo();
            
            var gameServer = lobby.gameServer;
            
            // Try to extract connection info from network_ports array first (PlayFlow format)
            if (gameServer.ContainsKey("network_ports") && gameServer["network_ports"] is Newtonsoft.Json.Linq.JArray ports)
            {
                foreach (var port in ports)
                {
                    // Look for the main game port (usually UDP 7770 or similar)
                    if (port["protocol"]?.ToString() == "udp" && port["internal_port"]?.ToString() == "7770")
                    {
                        string host = port["host"]?.ToString() ?? "";
                        int externalPort = port["external_port"] != null ? Convert.ToInt32(port["external_port"]) : 0;
                        
                        if (_settings?.debugLogging ?? false)
                        {
                            Debug.Log($"[LobbyRefreshManager] Extracted connection info: {host}:{externalPort}");
                        }
                        return new ConnectionInfo { Ip = host, Port = externalPort };
                    }
                }
            }
            
            // Fallback to simple ip/port fields
            string ip = gameServer.ContainsKey("ip") ? gameServer["ip"].ToString() : "";
            int portNum = gameServer.ContainsKey("port") ? Convert.ToInt32(gameServer["port"]) : 0;
            
            return new ConnectionInfo { Ip = ip, Port = portNum };
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from SSE events
            if (_sseManager != null)
            {
                _sseManager.OnConnected -= HandleSSEConnected;
                _sseManager.OnDisconnected -= HandleSSEDisconnected;
                _sseManager.OnLobbyUpdated -= HandleSSELobbyUpdate;
                _sseManager.OnLobbyDeleted -= HandleSSELobbyDeleted;
                _sseManager.OnError -= HandleSSEError;
            }
            
            // Unsubscribe from session events
            if (_lobbyManager != null)
            {
                _lobbyManager.Events.OnLobbyUpdated.RemoveListener(HandleSessionLobbyChanged);
            }
        }
    }
} 