using System.Collections.Generic;
using System.Linq;

namespace PlayFlow
{
    public class LobbyCacheImpl : ILobbyCache
    {
        private Lobby _currentLobby;
        private List<Lobby> _availableLobbies = new List<Lobby>();
        private readonly object _lockObject = new object();
        
        public void SetCurrentLobby(Lobby lobby)
        {
            lock (_lockObject)
            {
                _currentLobby = lobby;
            }
        }
        
        public Lobby GetCurrentLobby()
        {
            lock (_lockObject)
            {
                return _currentLobby;
            }
        }
        
        public void ClearCurrentLobby()
        {
            lock (_lockObject)
            {
                _currentLobby = null;
            }
        }
        
        public void SetAvailableLobbies(List<Lobby> lobbies)
        {
            lock (_lockObject)
            {
                _availableLobbies = lobbies?.ToList() ?? new List<Lobby>();
            }
        }
        
        public List<Lobby> GetAvailableLobbies()
        {
            lock (_lockObject)
            {
                return _availableLobbies.ToList();
            }
        }
        
        public bool TryGetLobby(string lobbyId, out Lobby lobby)
        {
            lock (_lockObject)
            {
                // Check current lobby first
                if (_currentLobby != null && _currentLobby.id == lobbyId)
                {
                    lobby = _currentLobby;
                    return true;
                }
                
                // Check available lobbies
                lobby = _availableLobbies.FirstOrDefault(l => l.id == lobbyId);
                return lobby != null;
            }
        }
    }
} 