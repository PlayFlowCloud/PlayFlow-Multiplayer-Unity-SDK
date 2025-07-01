using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayFlow
{
    /// <summary>
    /// Internal state manager to ensure consistency and prevent desyncs
    /// Platform-safe implementation for all Unity platforms including WebGL
    /// </summary>
    internal class PlayFlowLobbyStateManager
    {
        private Lobby currentLobby;
        private int stateVersion = 0;
        private Dictionary<string, int> playerStateVersions = new Dictionary<string, int>();
        private bool isProcessingUpdate = false;
        private readonly object updateLock = new object(); // Add lock for thread safety
        
        // Version tracking for deduplication
        private string lastProcessedLobbyId = "";
        private string lastProcessedUpdateTime = "";
        
        public event Action<Lobby, Lobby> OnStateChanged;
        
        public Lobby CurrentLobby 
        { 
            get 
            { 
                lock (updateLock)
                {
                    return currentLobby;
                }
            }
        }
        
        public bool IsProcessingUpdate
        {
            get 
            { 
                lock (updateLock)
                {
                    return isProcessingUpdate;
                }
            }
        }
        
        /// <summary>
        /// Attempt to update the lobby state with version checking
        /// </summary>
        public bool TryUpdateState(Lobby newLobby, bool forceUpdate = false)
        {
            if (newLobby == null) return false;
            
            lock (updateLock)
            {
                // If we're already processing an update, reject this one unless forced
                if (isProcessingUpdate && !forceUpdate)
                {
                    return false;
                }
                
                // Check for duplicate update
                if (!forceUpdate && IsDuplicateUpdate(newLobby))
                {
                    return false;
                }
                
                isProcessingUpdate = true;
            }
            
            try
            {
                Lobby oldLobby = null;
                lock (updateLock)
                {
                    oldLobby = currentLobby;
                    currentLobby = newLobby;
                    stateVersion++;
                    
                    // Track this update
                    lastProcessedLobbyId = newLobby.id;
                    lastProcessedUpdateTime = newLobby.updatedAt;
                    
                    // Update player state versions
                    UpdatePlayerStateVersions(newLobby);
                }
                
                // Fire state changed event outside of lock to prevent deadlocks
                OnStateChanged?.Invoke(oldLobby, newLobby);
                
                return true;
            }
            finally
            {
                lock (updateLock)
                {
                    isProcessingUpdate = false;
                }
            }
        }
        
        /// <summary>
        /// Check if this is a duplicate update we've already processed
        /// </summary>
        private bool IsDuplicateUpdate(Lobby newLobby)
        {
            // First time update
            if (currentLobby == null) return false;
            
            // Different lobby
            if (newLobby.id != lastProcessedLobbyId) return false;
            
            // Same update timestamp means duplicate
            if (newLobby.updatedAt == lastProcessedUpdateTime) return true;
            
            // Try to parse timestamps for comparison
            try
            {
                if (DateTime.TryParse(currentLobby.updatedAt, out DateTime currentTime) &&
                    DateTime.TryParse(newLobby.updatedAt, out DateTime newTime))
                {
                    // New update is older or same as current
                    if (newTime <= currentTime) return true;
                }
            }
            catch
            {
                // If parsing fails, assume it's not a duplicate to be safe
            }
            
            return false;
        }
        
        /// <summary>
        /// Update player state version tracking
        /// </summary>
        private void UpdatePlayerStateVersions(Lobby lobby)
        {
            if (lobby.lobbyStateRealTime == null) return;
            
            foreach (var kvp in lobby.lobbyStateRealTime)
            {
                if (!playerStateVersions.ContainsKey(kvp.Key))
                {
                    playerStateVersions[kvp.Key] = 0;
                }
                playerStateVersions[kvp.Key]++;
            }
        }
        
        /// <summary>
        /// Get the current state version
        /// </summary>
        public int GetStateVersion()
        {
            return stateVersion;
        }
        
        /// <summary>
        /// Get player state version
        /// </summary>
        public int GetPlayerStateVersion(string playerId)
        {
            if (playerStateVersions.TryGetValue(playerId, out var version))
            {
                return version;
            }
            return 0;
        }
        
        /// <summary>
        /// Check if we have a specific player's state
        /// </summary>
        public bool HasPlayerState(string playerId)
        {
            return currentLobby != null && 
                   currentLobby.lobbyStateRealTime != null && 
                   currentLobby.lobbyStateRealTime.ContainsKey(playerId);
        }
        
        /// <summary>
        /// Clear all state
        /// </summary>
        public void Clear()
        {
            currentLobby = null;
            stateVersion = 0;
            playerStateVersions.Clear();
            isProcessingUpdate = false;
            lastProcessedLobbyId = "";
            lastProcessedUpdateTime = "";
        }
    }
} 