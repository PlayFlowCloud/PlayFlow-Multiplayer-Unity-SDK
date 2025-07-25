using UnityEngine;

namespace PlayFlow
{
    /// <summary>
    /// Handles mobile app lifecycle events to properly manage SSE connections
    /// </summary>
    internal class MobileLifecycleHandler : MonoBehaviour
    {
        private LobbySseManager _sseManager;
        
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
                Debug.Log("[MobileLifecycleHandler] App pausing - pausing SSE connection.");
                _sseManager.Pause();
            }
            else
            {
                // App is coming back to foreground
                Debug.Log("[MobileLifecycleHandler] App resuming - resuming SSE connection.");
                _sseManager.Resume();
            }
        }
#endif
    }
}
