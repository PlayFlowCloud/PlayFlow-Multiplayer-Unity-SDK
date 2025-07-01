using UnityEngine;

namespace PlayFlow
{
    [CreateAssetMenu(fileName = "PlayFlowSettings", menuName = "PlayFlow/Settings")]
    public class PlayFlowSettings : ScriptableObject
    {
        [Header("API Configuration")]
        [Tooltip("Your PlayFlow API key")]
        public string apiKey;
        
        [Tooltip("The base URL for the PlayFlow backend")]
        public string baseUrl = "https://backend.computeflow.cloud";
        
        [Tooltip("The default lobby configuration name")]
        public string defaultLobbyConfig = "Default";
        
        [Header("Network Settings")]
        [Tooltip("How often to refresh lobby data (in seconds)")]
        [Range(3f, 30f)]
        public float refreshInterval = 5f;
        
        [Tooltip("Enable or disable automatic refreshing of lobby data.")]
        public bool autoRefresh = true;
        
        [Tooltip("Maximum number of retry attempts for failed requests")]
        [Range(1, 10)]
        public int maxRetryAttempts = 3;
        
        [Tooltip("Delay between retry attempts (in seconds)")]
        [Range(0.5f, 5f)]
        public float retryDelay = 1f;
        
        [Header("Timeouts")]
        [Tooltip("Request timeout in seconds")]
        public float requestTimeout = 30f;
        
        [Tooltip("Connection timeout in seconds")]
        public float connectionTimeout = 10f;
        
        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        public bool debugLogging = false;
        
        private void OnValidate()
        {
            refreshInterval = Mathf.Max(3f, refreshInterval);
            requestTimeout = Mathf.Max(5f, requestTimeout);
            connectionTimeout = Mathf.Max(5f, connectionTimeout);
        }
    }
} 