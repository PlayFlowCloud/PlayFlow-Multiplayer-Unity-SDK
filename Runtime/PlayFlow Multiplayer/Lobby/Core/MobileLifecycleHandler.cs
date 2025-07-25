using UnityEngine;

namespace PlayFlow
{
    /// <summary>
    /// Handles mobile app lifecycle events to properly manage SSE connections
    /// </summary>
    internal class MobileLifecycleHandler : MonoBehaviour
    {
        private LobbySseManager _sseManager;
        private bool _debugLogging;
        
        void Start()
        {
            _sseManager = LobbySseManager.Instance;
            DontDestroyOnLoad(gameObject);
            // Get debug logging setting from the lobby manager
            _debugLogging = PlayFlowLobbyManagerV2.Instance?.Debugging ?? false;
        }
        
#if UNITY_IOS || UNITY_ANDROID
        void OnApplicationPause(bool pauseStatus)
        {
            if (_sseManager == null) return;
            
            if (pauseStatus)
            {
                // App is going to background
                if (_debugLogging)
                {
                    Debug.Log("[MobileLifecycleHandler] App pausing - pausing SSE connection.");
                }
                _sseManager.Pause();
            }
            else
            {
                // App is coming back to foreground
                if (_debugLogging)
                {
                    Debug.Log("[MobileLifecycleHandler] App resuming - resuming SSE connection.");
                }
                _sseManager.Resume();
            }
        }
#endif
    }
}
