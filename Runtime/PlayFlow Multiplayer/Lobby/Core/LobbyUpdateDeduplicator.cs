using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayFlow
{
    /// <summary>
    /// Prevents duplicate lobby update events when both API response and SSE deliver the same update
    /// </summary>
    internal class LobbyUpdateDeduplicator
    {
        private class UpdateRecord
        {
            public string UpdatedAt { get; set; }
            public float ReceivedTime { get; set; }
        }
        
        private readonly Dictionary<string, UpdateRecord> _recentUpdates = new Dictionary<string, UpdateRecord>();
        private const float DEDUP_WINDOW = 2.0f; // 2 second window to deduplicate
        
        /// <summary>
        /// Check if this lobby update is a duplicate
        /// </summary>
        public bool IsDuplicateUpdate(Lobby lobby)
        {
            if (lobby == null) return false;
            
            // Clean old entries
            CleanOldEntries();
            
            string key = lobby.id;
            string updateTimestamp = lobby.updatedAt;
            
            if (_recentUpdates.TryGetValue(key, out var record))
            {
                // If same update timestamp within dedup window, it's a duplicate
                if (record.UpdatedAt == updateTimestamp && 
                    (Time.time - record.ReceivedTime) < DEDUP_WINDOW)
                {
                    return true;
                }
            }
            
            // Record this update
            _recentUpdates[key] = new UpdateRecord
            {
                UpdatedAt = updateTimestamp,
                ReceivedTime = Time.time
            };
            
            return false;
        }
        
        private void CleanOldEntries()
        {
            var keysToRemove = new List<string>();
            foreach (var kvp in _recentUpdates)
            {
                if (Time.time - kvp.Value.ReceivedTime > DEDUP_WINDOW)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _recentUpdates.Remove(key);
            }
        }
    }
}