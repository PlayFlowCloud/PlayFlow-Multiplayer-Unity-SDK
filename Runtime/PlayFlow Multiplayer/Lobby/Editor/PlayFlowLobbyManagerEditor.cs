using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using PlayFlow; // Assuming Lobby and PlayFlowLobbyManager are in this namespace

[CustomEditor(typeof(PlayFlowLobbyManager))]
public class PlayFlowLobbyManagerEditor : Editor
{
    private bool _showAvailableLobbies = true;
    private bool _showCurrentLobbyDetails = true;
    private bool _showCurrentLobbyPlayers = true;
    private bool _showCurrentLobbySettings = false;
    private bool _showCurrentLobbyGameServer = false;

    // Dictionary to store foldout states for nested dictionaries/lists
    private Dictionary<string, bool> _nestedFoldoutStates = new Dictionary<string, bool>();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayFlowLobbyManager manager = (PlayFlowLobbyManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Live Lobby View", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Live lobby view is available only when the application is playing.", MessageType.Info);
            return;
        }

        // Display Current Lobby Details
        _showCurrentLobbyDetails = EditorGUILayout.Foldout(_showCurrentLobbyDetails, "Current Lobby Details", true, EditorStyles.foldoutHeader);
        if (_showCurrentLobbyDetails)
        {
            EditorGUI.indentLevel++;
            Lobby currentLobby = manager.GetCurrentLobby();
            if (currentLobby != null)
            {
                EditorGUILayout.LabelField("Name:", currentLobby.name);
                EditorGUILayout.LabelField("ID:", currentLobby.id);
                EditorGUILayout.LabelField("Host:", currentLobby.host + (manager.IsHost() ? " (You)" : ""));
                EditorGUILayout.LabelField("Status:", currentLobby.status);
                EditorGUILayout.LabelField("Players:", $"{currentLobby.currentPlayers} / {currentLobby.maxPlayers}");
                EditorGUILayout.LabelField("Is Private:", currentLobby.isPrivate.ToString());
                EditorGUILayout.LabelField("Invite Code:", manager.GetInviteCode() ?? "N/A");
                EditorGUILayout.Space(5);

                // Current Lobby Players
                _showCurrentLobbyPlayers = EditorGUILayout.Foldout(_showCurrentLobbyPlayers, $"Players ({currentLobby.players?.Length ?? 0})", true, EditorStyles.foldout);
                if (_showCurrentLobbyPlayers && currentLobby.players != null)
                {
                    EditorGUI.indentLevel++;
                    foreach (string playerId in currentLobby.players)
                    {
                        EditorGUILayout.LabelField("- " + playerId + (playerId == manager.GetPlayerId() ? " (You)" : "") + (playerId == currentLobby.host ? " (Host)" : ""));
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(5);

                // Current Lobby Settings
                _showCurrentLobbySettings = EditorGUILayout.Foldout(_showCurrentLobbySettings, "Lobby Settings", true, EditorStyles.foldout);
                if (_showCurrentLobbySettings)
                {
                    if (currentLobby.settings != null && currentLobby.settings.Count > 0) 
                    {
                        DisplayDictionary(currentLobby.settings, "lobbySettings");
                    }
                    else
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("No custom settings.");
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUILayout.Space(5);
                
                // Current Lobby GameServer Info
                _showCurrentLobbyGameServer = EditorGUILayout.Foldout(_showCurrentLobbyGameServer, "Game Server Info", true, EditorStyles.foldout);
                if (_showCurrentLobbyGameServer)
                {
                     if (currentLobby.gameServer != null && currentLobby.gameServer.Count > 0)
                    {
                        DisplayDictionary(currentLobby.gameServer, "gameServer");
                    }
                    else
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("No game server info.");
                        EditorGUI.indentLevel--;
                    }
                }

            }
            else
            {
                EditorGUILayout.LabelField("Not currently in a lobby.");
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();

        // Display Available Lobbies
        _showAvailableLobbies = EditorGUILayout.Foldout(_showAvailableLobbies, "Available Lobbies", true, EditorStyles.foldoutHeader);
        if (_showAvailableLobbies)
        {
            EditorGUI.indentLevel++;
            List<Lobby> availableLobbies = manager.GetAvailableLobbies();
            if (availableLobbies != null && availableLobbies.Count > 0)
            {
                EditorGUILayout.LabelField("Count:", availableLobbies.Count.ToString());
                for (int i = 0; i < availableLobbies.Count; i++)
                {
                    Lobby lobby = availableLobbies[i];
                    EditorGUILayout.LabelField($"Lobby {i + 1}: {lobby.name} ({lobby.id})", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Status:", lobby.status);
                    EditorGUILayout.LabelField("Players:", $"{lobby.currentPlayers} / {lobby.maxPlayers}");
                    EditorGUILayout.LabelField("Private:", lobby.isPrivate.ToString());
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(2);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No lobbies found or list not refreshed yet.");
            }
            EditorGUI.indentLevel--;
        }
        
        if(Application.isPlaying) Repaint();
    }

    private void DisplayDictionary(Dictionary<string, object> dict, string path)
    {
        EditorGUI.indentLevel++;
        if (dict == null || dict.Count == 0)
        {
            EditorGUILayout.LabelField("Empty or null dictionary.");
            EditorGUI.indentLevel--;
            return;
        }

        foreach (var kvp in dict)
        {
            string currentPath = path + "." + kvp.Key;
            
            if (kvp.Value is Dictionary<string, object> nestedDict)
            {
                if (!_nestedFoldoutStates.ContainsKey(currentPath)) _nestedFoldoutStates[currentPath] = false;
                _nestedFoldoutStates[currentPath] = EditorGUILayout.Foldout(_nestedFoldoutStates[currentPath], kvp.Key + " (Dictionary)", true, EditorStyles.foldout);
                if (_nestedFoldoutStates[currentPath])
                {
                    DisplayDictionary(nestedDict, currentPath);
                }
            }
            else if (kvp.Value is List<object> list)
            {
                if (!_nestedFoldoutStates.ContainsKey(currentPath)) _nestedFoldoutStates[currentPath] = false;
                _nestedFoldoutStates[currentPath] = EditorGUILayout.Foldout(_nestedFoldoutStates[currentPath], kvp.Key + $" (List[{list.Count}])", true, EditorStyles.foldout);
                 
                if (_nestedFoldoutStates[currentPath])
                {
                     EditorGUI.indentLevel++;
                     for(int i=0; i < list.Count; i++)
                     {
                        string itemPath = currentPath + "[" + i + "]";
                        if (list[i] is Dictionary<string, object> listItemDict)
                        {
                            if (!_nestedFoldoutStates.ContainsKey(itemPath)) _nestedFoldoutStates[itemPath] = false;
                             _nestedFoldoutStates[itemPath] = EditorGUILayout.Foldout(_nestedFoldoutStates[itemPath], $"Item {i} (Dictionary)", true, EditorStyles.foldout);
                            if(_nestedFoldoutStates[itemPath])
                            {
                                DisplayDictionary(listItemDict, itemPath);
                            }
                        }
                        else 
                        {
                            // For simple list items, just display them directly.
                            // Could also make primitive list items foldouts if they can be very long.
                            EditorGUILayout.LabelField($"Item {i}:", list[i]?.ToString() ?? "null", EditorStyles.wordWrappedLabel);
                        }
                     }
                     EditorGUI.indentLevel--;
                }
            }
            else
            {
                // Use WordWrappedLabel for potentially long string values
                EditorGUILayout.LabelField(kvp.Key + ":", kvp.Value?.ToString() ?? "null", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.Space(2); // Add a small space after each entry
        }
        EditorGUI.indentLevel--;
    }
} 