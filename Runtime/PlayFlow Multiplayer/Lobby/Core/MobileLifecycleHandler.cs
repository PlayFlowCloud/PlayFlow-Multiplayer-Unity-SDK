using UnityEngine;

namespace PlayFlow
{
    /// <summary>
    /// Handles mobile app lifecycle events to properly manage SSE connections
    /// </summary>
    internal class MobileLifecycleHandler : MonoBehaviour
    {
        private LobbySseManager _sseManager;
        private bool _wasConnected = false;
        
        void Start()
        {
            _sseManager = LobbySseManager.Instance;
            DontDestroyOnLoad(gameObject);
        }
        
#if UNITY_IOS || UNITY_ANDROID
        void OnApplicationPause(bool pauseStatus)
        {
            if (_sseManager == null) return;
            
            if (pauseStatus)
            {
                // App is going to background
                Debug.Log("[MobileLifecycleHandler] App pausing - disconnecting SSE");
                _wasConnected = _sseManager.IsConnected;
                _sseManager.Pause();
            }
            else
            {
                // App is coming back to foreground
                Debug.Log("[MobileLifecycleHandler] App resuming - reconnecting SSE");
                if (_wasConnected)
                {
                    _sseManager.Resume();
                }
            }
        }
        
        void OnApplicationFocus(bool hasFocus)
        {
            if (_sseManager == null) return;
            
            // iOS sometimes uses focus instead of pause
            if (!hasFocus && _sseManager.IsConnected)
            {
                Debug.Log("[MobileLifecycleHandler] App lost focus - may disconnect SSE");
            }
            else if (hasFocus && _wasConnected && !_sseManager.IsConnected)
            {
                Debug.Log("[MobileLifecycleHandler] App regained focus - reconnecting SSE");
                _sseManager.Resume();
            }
        }
#endif
    }
}