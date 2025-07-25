using UnityEngine;

namespace PlayFlow
{
    public class PlayFlowCore : MonoBehaviour
    {
        private static PlayFlowCore _instance;
        
        private PlayFlowSettings _settings;
        
        // Core services
        public ILobbyAPI LobbyAPI { get; private set; }
        public IEventDispatcher EventDispatcher { get; private set; }
        
        public PlayFlowSettings Settings => _settings;
        
        public static PlayFlowCore Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PlayFlowCore>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[PlayFlowCore]");
                        _instance = go.AddComponent<PlayFlowCore>();
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        /// <summary>
        /// Initialize the core with specific settings. This must be called before using any services.
        /// </summary>
        public void InitializeWithSettings(PlayFlowSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("[PlayFlowCore] Cannot initialize with null settings");
                return;
            }
            
            if (_settings != null && _settings != settings)
            {
                Debug.LogWarning("[PlayFlowCore] Re-initializing with new settings.");
            }
            
            _settings = settings;
            
            InitializeServices();
        }
        
        private void InitializeServices()
        {
            // Clean up existing child services if any
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            var networkManagerGO = new GameObject("[NetworkManager]");
            networkManagerGO.transform.SetParent(transform);
            var networkManager = networkManagerGO.AddComponent<UnityNetworkManager>();
            networkManager.Initialize(_settings);
            
            var eventDispatcherGO = new GameObject("[EventDispatcher]");
            eventDispatcherGO.transform.SetParent(transform);
            var unityEventDispatcher = eventDispatcherGO.AddComponent<UnityEventDispatcher>();
            
            LobbyAPI = new LobbyAPIImpl(_settings.baseUrl, _settings.apiKey, _settings.defaultLobbyConfig, networkManager);
            EventDispatcher = unityEventDispatcher;
            
            if (_settings.debugLogging)
            {
                Debug.Log("[PlayFlowCore] Services initialized successfully");
            }
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
 