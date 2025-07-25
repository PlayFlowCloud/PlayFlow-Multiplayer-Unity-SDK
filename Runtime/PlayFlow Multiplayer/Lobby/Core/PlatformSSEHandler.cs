using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayFlow
{
    /// <summary>
    /// Platform-specific SSE handling to ensure cross-platform compatibility
    /// </summary>
    internal static class PlatformSSEHandler
    {
        public static bool IsSSESupported()
        {
#if UNITY_EDITOR
            // Always support SSE in editor for testing
            return true;
#elif UNITY_WEBGL
            // WebGL has limitations with streaming downloads
            Debug.LogWarning("[PlatformSSEHandler] SSE may have limited support on WebGL. Falling back to polling recommended.");
            return false; // Force polling on WebGL
#elif UNITY_SWITCH
            // Nintendo Switch has specific networking requirements
            return CheckSwitchNetworkCapabilities();
#else
            return true;
#endif
        }

        public static int GetRecommendedTimeout()
        {
#if UNITY_EDITOR
            // Longer timeout in editor for debugging
            return 120000; // 2 minutes
#elif UNITY_IOS || UNITY_ANDROID
            // Mobile platforms need shorter timeouts due to background restrictions
            return 30000; // 30 seconds
#else
            return 60000; // 60 seconds for desktop/console
#endif
        }

        public static bool ShouldReconnectOnBackground()
        {
#if UNITY_EDITOR
            // Can test mobile backgrounding behavior in editor
            return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS ||
                   UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android;
#elif UNITY_IOS || UNITY_ANDROID
            return true; // Mobile apps need to reconnect when returning from background
#else
            return false;
#endif
        }

#if UNITY_SWITCH
        private static bool CheckSwitchNetworkCapabilities()
        {
            // Add Switch-specific network checks here
            return true;
        }
#endif
    }
}