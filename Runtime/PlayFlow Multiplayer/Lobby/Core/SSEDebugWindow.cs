#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace PlayFlow
{
    /// <summary>
    /// Editor window for debugging SSE connections in Unity Editor
    /// </summary>
    public class SSEDebugWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private GUIStyle _connectedStyle;
        private GUIStyle _disconnectedStyle;
        private SSEConnectionMetrics _metrics;
        
        [MenuItem("PlayFlow/SSE Debug Window")]
        public static void ShowWindow()
        {
            GetWindow<SSEDebugWindow>("SSE Debug");
        }
        
        void OnEnable()
        {
            _connectedStyle = new GUIStyle();
            _connectedStyle.normal.textColor = Color.green;
            _connectedStyle.fontStyle = FontStyle.Bold;
            
            _disconnectedStyle = new GUIStyle();
            _disconnectedStyle.normal.textColor = Color.red;
            _disconnectedStyle.fontStyle = FontStyle.Bold;
        }
        
        void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("SSE Debug is only available in Play Mode", MessageType.Info);
                return;
            }
            
            var sseManager = LobbySseManager.Instance;
            if (sseManager == null)
            {
                EditorGUILayout.HelpBox("SSE Manager not initialized", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.LabelField("SSE Connection Status", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Connection Status
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", GUILayout.Width(100));
            if (sseManager.IsConnected)
            {
                EditorGUILayout.LabelField("CONNECTED", _connectedStyle);
            }
            else
            {
                EditorGUILayout.LabelField("DISCONNECTED", _disconnectedStyle);
            }
            EditorGUILayout.EndHorizontal();
            
            // Platform Support
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("SSE Supported:", GUILayout.Width(100));
            EditorGUILayout.LabelField(PlatformSSEHandler.IsSSESupported() ? "Yes" : "No");
            EditorGUILayout.EndHorizontal();
            
            // Timeout
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Timeout:", GUILayout.Width(100));
            EditorGUILayout.LabelField($"{PlatformSSEHandler.GetRecommendedTimeout() / 1000}s");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            // Manual controls
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Disconnect"))
            {
                sseManager.Disconnect();
            }
            if (GUILayout.Button("Simulate Error"))
            {
                sseManager.GetType().GetMethod("HandleSSEError", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(sseManager, new object[] { "Simulated error from debug window" });
            }
            EditorGUILayout.EndHorizontal();
            
            // Metrics
            if (_metrics != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Metrics", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_metrics.GetReport(), GUILayout.Height(60));
            }
            
            // Force repaint for real-time updates
            Repaint();
        }
    }
}
#endif