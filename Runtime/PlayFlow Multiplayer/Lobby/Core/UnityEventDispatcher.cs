using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayFlow
{
    public class UnityEventDispatcher : MonoBehaviour, IEventDispatcher
    {
        private readonly Dictionary<string, List<Action<object>>> _handlers = new Dictionary<string, List<Action<object>>>();
        private readonly object _lockObject = new object();
        
        public void Dispatch(string eventName, object data = null)
        {
            List<Action<object>> handlersToCall;
            
            lock (_lockObject)
            {
                if (!_handlers.TryGetValue(eventName, out var handlers))
                {
                    return;
                }
                
                // Create a copy to avoid collection modified exceptions
                handlersToCall = new List<Action<object>>(handlers);
            }
            
            // Call handlers outside of lock
            foreach (var handler in handlersToCall)
            {
                try
                {
                    handler?.Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventDispatcher] Error in event handler for '{eventName}': {e.Message}");
                }
            }
        }
        
        public void Subscribe(string eventName, Action<object> handler)
        {
            if (handler == null) return;
            
            lock (_lockObject)
            {
                if (!_handlers.TryGetValue(eventName, out var handlers))
                {
                    handlers = new List<Action<object>>();
                    _handlers[eventName] = handlers;
                }
                
                if (!handlers.Contains(handler))
                {
                    handlers.Add(handler);
                }
            }
        }
        
        public void Unsubscribe(string eventName, Action<object> handler)
        {
            if (handler == null) return;
            
            lock (_lockObject)
            {
                if (_handlers.TryGetValue(eventName, out var handlers))
                {
                    handlers.Remove(handler);
                    
                    if (handlers.Count == 0)
                    {
                        _handlers.Remove(eventName);
                    }
                }
            }
        }
        
        private void OnDestroy()
        {
            lock (_lockObject)
            {
                _handlers.Clear();
            }
        }
    }
} 