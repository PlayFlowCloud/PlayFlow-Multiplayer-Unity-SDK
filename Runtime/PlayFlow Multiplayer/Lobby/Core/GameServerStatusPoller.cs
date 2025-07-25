using System;
using System.Collections;
using UnityEngine;

namespace PlayFlow
{
    /// <summary>
    /// Handles polling for game server status during the launching phase
    /// </summary>
    internal class GameServerStatusPoller : MonoBehaviour
    {
        private Coroutine _pollingCoroutine;
        private string _currentLobbyId;
        private float _pollInterval = 2f; // Poll every 2 seconds during launching
        private Action<Lobby> _onServerReady;
        private Action<string> _onError;
        private LobbyOperations _operations;
        
        public void Initialize(LobbyOperations operations)
        {
            _operations = operations;
        }
        
        /// <summary>
        /// Start polling for game server status when it's launching
        /// </summary>
        public void StartPollingForServerLaunch(string lobbyId, Action<Lobby> onServerReady, Action<string> onError)
        {
            if (_operations == null)
            {
                Debug.LogError("[GameServerStatusPoller] Not initialized with LobbyOperations!");
                onError?.Invoke("GameServerStatusPoller not properly initialized");
                return;
            }
            
            StopPolling();
            
            _currentLobbyId = lobbyId;
            _onServerReady = onServerReady;
            _onError = onError;
            
            Debug.Log($"[GameServerStatusPoller] Starting to poll for game server launch status for lobby {lobbyId}");
            _pollingCoroutine = StartCoroutine(PollServerStatus());
        }
        
        /// <summary>
        /// Stop polling
        /// </summary>
        public void StopPolling()
        {
            if (_pollingCoroutine != null)
            {
                StopCoroutine(_pollingCoroutine);
                _pollingCoroutine = null;
                Debug.Log($"[GameServerStatusPoller] Stopped polling for lobby {_currentLobbyId}");
            }
            
            _currentLobbyId = null;
            _onServerReady = null;
            _onError = null;
        }
        
        private IEnumerator PollServerStatus()
        {
            var wait = new WaitForSeconds(_pollInterval);
            
            if (_operations == null)
            {
                Debug.LogError("[GameServerStatusPoller] LobbyOperations not set");
                yield break;
            }
            
            int attempts = 0;
            const int maxAttempts = 30; // Max 60 seconds of polling (30 * 2s)
            
            while (attempts < maxAttempts)
            {
                bool requestComplete = false;
                bool serverReady = false;
                Lobby updatedLobby = null;
                
                yield return _operations.GetLobbyCoroutine(_currentLobbyId, 
                    lobby => 
                    {
                        updatedLobby = lobby;
                        
                        // Check if game server exists and its status
                        if (lobby.gameServer != null && lobby.gameServer.ContainsKey("status"))
                        {
                            string serverStatus = lobby.gameServer["status"]?.ToString();
                            Debug.Log($"[GameServerStatusPoller] Game server status: {serverStatus} for lobby {lobby.id}");
                            
                            if (serverStatus == "running")
                            {
                                serverReady = true;
                            }
                            else if (serverStatus == "failed" || serverStatus == "stopped")
                            {
                                _onError?.Invoke($"Game server failed to launch: {serverStatus}");
                                requestComplete = true;
                                return;
                            }
                        }
                        
                        requestComplete = true;
                    },
                    error => 
                    {
                        Debug.LogError($"[GameServerStatusPoller] Failed to get lobby status: {error}");
                        _onError?.Invoke(error);
                        requestComplete = true;
                    });
                
                // Wait for request to complete
                yield return new WaitUntil(() => requestComplete);
                
                if (serverReady && updatedLobby != null)
                {
                    Debug.Log($"[GameServerStatusPoller] ✅ Game server is running for lobby {_currentLobbyId}!");
                    _onServerReady?.Invoke(updatedLobby);
                    StopPolling();
                    yield break;
                }
                
                attempts++;
                
                // Only wait if we're going to poll again
                if (attempts < maxAttempts && !serverReady)
                {
                    yield return wait;
                }
            }
            
            // Timeout
            Debug.LogError($"[GameServerStatusPoller] Timeout waiting for game server to launch for lobby {_currentLobbyId}");
            _onError?.Invoke("Timeout waiting for game server to launch");
            StopPolling();
        }
    }
}