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
        private PlayFlowSession _session;
        private Coroutine _refreshCoroutine;
        
        public event Action<List<Lobby>> OnLobbyListRefreshed;
        
        public void Initialize(PlayFlowSettings settings, LobbyOperations operations)
        {
            _settings = settings;
            _operations = operations;
            _session = GetComponent<PlayFlowSession>();
            
            // Start the refresh loop here, after settings are assigned
            if (_settings != null && _settings.autoRefresh && _session != null && _refreshCoroutine == null)
            {
                _refreshCoroutine = StartCoroutine(RefreshLoop());
            }
        }
        
        private void OnEnable()
        {
            // The loop is now started in Initialize, but we can add a safety check here
            // in case the component is disabled and re-enabled at runtime.
            if (_settings != null && _settings.autoRefresh && _session != null && _refreshCoroutine == null)
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
        }
        
        private IEnumerator RefreshLoop()
        {
            var wait = new WaitForSeconds(_settings.refreshInterval);
            
            while (enabled)
            {
                yield return wait;
                
                if (_session != null && _session.IsInLobby && _session.CurrentLobby != null)
                {
                    yield return RefreshCurrentLobby(_session.CurrentLobby.id);
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
            yield return _operations.GetLobbyCoroutine(lobbyId, 
                lobby => 
                {
                    if (_session != null && _session.CurrentLobby?.id == lobby.id)
                    {
                        _session.UpdateCurrentLobby(lobby);
                    }
                },
                error => 
                {
                    if (error.Contains("404") || error.Contains("Not Found"))
                    {
                        // Lobby no longer exists, clear it
                        if (_session != null)
                        {
                            _session.ClearCurrentLobby();
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
            if (_session != null && _session.IsInLobby && _session.CurrentLobby != null)
            {
                StartCoroutine(RefreshCurrentLobby(_session.CurrentLobby.id));
            }
            else
            {
                StartCoroutine(RefreshLobbyList());
            }
        }
    }
} 