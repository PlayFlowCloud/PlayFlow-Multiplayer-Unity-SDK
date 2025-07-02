using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace PlayFlow
{
    public class PlayFlowSession : MonoBehaviour
    {
        [SerializeField] private PlayFlowSettings _settings;
        
        private LobbyState _currentState = LobbyState.Disconnected;
        private string _playerId;
        private Lobby _currentLobby;
        private Coroutine _refreshCoroutine;
        
        // Properties
        public LobbyState CurrentState => _currentState;
        public string PlayerId => _playerId;
        public bool IsInitialized => !string.IsNullOrEmpty(_playerId);
        public bool IsInLobby => _currentLobby != null && _currentState == LobbyState.InLobby;
        public Lobby CurrentLobby => _currentLobby;
        
        // Events
        [Header("Session Events")]
        public StateChangedEvent OnStateChanged = new StateChangedEvent();
        public LobbyEvent OnLobbyUpdated = new LobbyEvent();
        public UnityEvent<string> OnError = new UnityEvent<string>();
        
        public void Initialize(string playerId, PlayFlowSettings settings = null)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogError("[PlayFlowSession] PlayerId cannot be empty");
                OnError?.Invoke("PlayerId cannot be empty");
                return;
            }
            
            if (settings != null)
            {
                _settings = settings;
            }
            
            if (_settings == null)
            {
                _settings = PlayFlowCore.Instance.Settings;
            }
            
            _playerId = playerId;
            ChangeState(LobbyState.Connected);
            
            if (_settings.debugLogging)
            {
                Debug.Log($"[PlayFlowSession] Initialized with player ID: {playerId}");
            }
        }
        
        public void SetCurrentLobby(Lobby lobby)
        {
            if (lobby == null)
            {
                ClearCurrentLobby();
                return;
            }
            
            var wasInLobby = IsInLobby;
            _currentLobby = lobby;
            
            if (!wasInLobby)
            {
                ChangeState(LobbyState.InLobby);
            }
            
            OnLobbyUpdated?.Invoke(lobby);
            
            // Start auto-refresh if not already running
            if (_refreshCoroutine == null && _settings != null)
            {
                _refreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
            }
        }
        
        public void UpdateCurrentLobby(Lobby newLobby)
        {
            if (newLobby == null || _currentLobby == null || newLobby.id != _currentLobby.id)
            {
                return; // Not the same lobby
            }
            
            // Perform a more thorough comparison than just the timestamp.
            bool hasChanged = false;
            
            // 1. Check for direct database updates via timestamp
            if (_currentLobby.updatedAt != newLobby.updatedAt)
            {
                hasChanged = true;
            }
            
            // 2. Check for changes in game server status (which doesn't change the timestamp)
            string oldServerStatus = _currentLobby.gameServer != null && _currentLobby.gameServer.ContainsKey("status") 
                ? _currentLobby.gameServer["status"]?.ToString() : null;
            
            string newServerStatus = newLobby.gameServer != null && newLobby.gameServer.ContainsKey("status")
                ? newLobby.gameServer["status"]?.ToString() : null;
            
            if (oldServerStatus != newServerStatus)
            {
                hasChanged = true;
            }

            // 3. Check for player count changes
            if ((_currentLobby.players?.Length ?? 0) != (newLobby.players?.Length ?? 0))
            {
                hasChanged = true;
            }

            if (!hasChanged)
            {
                return; // No meaningful change detected
            }
            
            _currentLobby = newLobby;
            OnLobbyUpdated?.Invoke(newLobby);
        }
        
        public void ClearCurrentLobby()
        {
            _currentLobby = null;
            
            if (_currentState == LobbyState.InLobby)
            {
                ChangeState(LobbyState.Connected);
            }
            
            // Stop auto-refresh
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }
        
        public void Disconnect()
        {
            ClearCurrentLobby();
            _playerId = null;
            ChangeState(LobbyState.Disconnected);
        }
        
        private void ChangeState(LobbyState newState)
        {
            if (_currentState == newState) return;
            
            var oldState = _currentState;
            _currentState = newState;
            
            OnStateChanged?.Invoke(oldState, newState);
            
            if (_settings?.debugLogging ?? false)
            {
                Debug.Log($"[PlayFlowSession] State changed from {oldState} to {newState}");
            }
        }
        
        private IEnumerator AutoRefreshCoroutine()
        {
            if (_settings == null) yield break;
            
            var wait = new WaitForSeconds(_settings.refreshInterval);
            
            while (_currentState == LobbyState.InLobby && _currentLobby != null)
            {
                yield return wait;
                
                // Double-check we're still in a lobby
                if (_currentLobby != null && PlayFlowCore.Instance?.LobbyAPI != null)
                {
                    yield return RefreshCurrentLobby();
                }
            }
            
            _refreshCoroutine = null;
        }
        
        private IEnumerator RefreshCurrentLobby()
        {
            if (_currentLobby == null) yield break;
            
            var lobbyId = _currentLobby.id;
            yield return PlayFlowCore.Instance.LobbyAPI.GetLobby(lobbyId,
                lobby => UpdateCurrentLobby(lobby),
                error =>
                {
                    if (error.Contains("404") || error.Contains("Not Found"))
                    {
                        // Lobby no longer exists
                        ClearCurrentLobby();
                        if (_settings.debugLogging)
                        {
                            Debug.Log($"[PlayFlowSession] Lobby {lobbyId} no longer exists");
                        }
                    }
                    else
                    {
                        OnError?.Invoke($"Failed to refresh lobby: {error}");
                    }
                });
        }
        
        private void OnDestroy()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }
    }
} 