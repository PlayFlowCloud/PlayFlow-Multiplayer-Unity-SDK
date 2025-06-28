// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Threading.Tasks;
// using Castle.Core.Internal;
// using Codice.Client.BaseCommands;
// using UnityEditor;
// using UnityEngine;
// using UnityEngine.UIElements;
//
// #if UNITY_EDITOR
//
//
// public class PlayFlowEditorWindow : EditorWindow
// {
//     [SerializeField] private VisualTreeAsset _tree;
//     private Button documentationButton;
//     private Button discordButton;
//     private Button pricingButton;
//     private Button getTokenButton;
//     private Button uploadButton;
//     private Button uploadStatusButton;
//     private Button startButton;
//     private Button refreshButton;
//     private Button getStatusButton;
//     private Button getLogsButton;
//     private Button restartButton;
//     private Button stopButton;
//
//     private TextField tokenField;
//     private IntegerField sslValue;
//     private TextField argumentsField;
//     private TextField logs;
//
//
//     private Toggle enableSSL;
//     private Toggle devBuild;
//     
//     private DropdownField location;
//     private DropdownField instanceType;
//     private DropdownField activeServersField;
//
//     private ProgressBar progress;
//
//
//     [MenuItem("PlayFlow/PlayFlow Cloud")]
//     public static void ShowEditor()
//     {
//         var window = GetWindow<PlayFlowEditorWindow>();
//         window.titleContent = new GUIContent("PlayFlow Cloud");
//     }
//
//     public Dictionary<string, string> productionRegionOptions = new Dictionary<string, string>
//     {
//         {"North America East (North Virginia)", "us-east"},
//         {"North America West (California)", "us-west"},
//         {"North America West (Oregon)", "us-west-2"},
//         {"Europe (Stockholm)", "eu-north"},
//         {"Europe (France)", "eu-west"},
//         {"South Asia (Mumbai)", "ap-south"},
//         {"South East Asia (Singapore)", "sea"},
//         {"East Asia (Korea)", "ea"},
//         {"East Asia (Japan)", "ap-north"},
//         {"Australia (Sydney)", "ap-southeast"}
//     };
//
//     Dictionary<string, string> instance_types = new Dictionary<string, string>
//     {
//         {"Small - 2 VCPU 1GB RAM", "small"},
//         {"Medium - 2 VCPU 2GB RAM", "medium"},
//         {"Large - 2 VCPU 4GB RAM", "large"},
//     };
//
//     private void CreateGUI()
//     {
//         _tree.CloneTree(rootVisualElement);
//         documentationButton = rootVisualElement.Q<Button>("ButtonDocumentation");
//         discordButton = rootVisualElement.Q<Button>("ButtonDiscord");
//         pricingButton = rootVisualElement.Q<Button>("ButtonPricing");
//         getTokenButton = rootVisualElement.Q<Button>("ButtonGetToken");
//         uploadButton = rootVisualElement.Q<Button>("ButtonUpload");
//         uploadStatusButton = rootVisualElement.Q<Button>("ButtonUploadStatus");
//         startButton = rootVisualElement.Q<Button>("ButtonStart");
//         refreshButton = rootVisualElement.Q<Button>("ButtonRefresh");
//         getStatusButton = rootVisualElement.Q<Button>("ButtonGetStatus");
//         getLogsButton = rootVisualElement.Q<Button>("ButtonGetLogs");
//         restartButton = rootVisualElement.Q<Button>("ButtonRestartServer");
//         stopButton = rootVisualElement.Q<Button>("ButtonStopServer");
//         logs = rootVisualElement.Q<TextField>("logs");
//         progress = rootVisualElement.Q<ProgressBar>("progress");
//
//         tokenField = rootVisualElement.Q<TextField>("TextToken");
//         tokenField.RegisterValueChangedCallback(HandleToken);
//
//         argumentsField = rootVisualElement.Q<TextField>("TextArgs");
//         sslValue = rootVisualElement.Q<IntegerField>("sslValue");
//
//         devBuild = rootVisualElement.Q<Toggle>("DevelopmentBuild");
//
//         enableSSL = rootVisualElement.Q<Toggle>("enableSSL");
//         location = rootVisualElement.Q<DropdownField>("locationDropdown");
//         location.choices = productionRegionOptions.Keys.ToList();
//
//         if (location.value.IsNullOrEmpty())
//         {
//             location.index = 0;
//         }
//
//         instanceType = rootVisualElement.Q<DropdownField>("instanceTypeDropdown");
//         instanceType.choices = instance_types.Keys.ToList();
//
//         if (instanceType.value.IsNullOrEmpty())
//         {
//             instanceType.index = 0;
//         }
//
//         activeServersField = rootVisualElement.Q<DropdownField>("ActiveServersDropdown");
//         activeServersField.choices = new List<string>();
//
//         documentationButton.clicked += OnDocumentationPressed;
//         discordButton.clicked += OnDiscordPressed;
//         pricingButton.clicked += OnPricingPressed;
//         getTokenButton.clicked += OnGetTokenPressed;
//
//         uploadButton.clicked += OnUploadPressed;
//         uploadStatusButton.clicked += OnUploadStatusPressed;
//         startButton.clicked += OnStartPressed;
//         enableSSL.RegisterValueChangedCallback(HandleSSL);
//
//         refreshButton.clicked += OnRefreshPressed;
//         getStatusButton.clicked += OnGetStatusPressed;
//         getLogsButton.clicked += OnGetLogsPressed;
//         restartButton.clicked += OnRestartPressed;
//         stopButton.clicked += OnStopPressed;
//     }
//
//     private void HandleToken(ChangeEvent<string> value)
//     {
//         instanceType.style.display = isProductionToken(value.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
//     }
//
//     private bool isProductionToken(string value)
//     {
//         return value.Length > 60;
//     }
//
//     private void HandleSSL(ChangeEvent<bool> value)
//     {
//         if (value.newValue && isProductionToken(tokenField.value))
//         {
//             sslValue.style.display = DisplayStyle.Flex;
//         }
//         else
//         {
//             sslValue.style.display = DisplayStyle.None;
//         }
//     }
//
//     private void OnDocumentationPressed()
//     {
//         System.Diagnostics.Process.Start("https://docs.playflowcloud.com");
//     }
//
//     private void OnDiscordPressed()
//     {
//         System.Diagnostics.Process.Start("https://discord.gg/P5w45Vx5Q8");
//     }
//
//     private void OnPricingPressed()
//     {
//         System.Diagnostics.Process.Start("https://www.playflowcloud.com/pricing");
//     }
//
//     private void OnGetTokenPressed()
//     {
//         System.Diagnostics.Process.Start("https://app.playflowcloud.com");
//         // Debug.Log(tokenField.value);
//     }
//
//     private void validateToken()
//     {
//         if (tokenField.value.IsNullOrEmpty())
//         {
//             throw new Exception("PlayFlow Token is empty. Please provide a PlayFlow token.");
//         }
//     }
//
//     private async void setCurrentServer(MatchInfo matchInfo)
//     {
//         await get_server_list(false);
//
//         if (matchInfo != null)
//         {
//             string match = matchInfo.match_id;
//
//             if (matchInfo.ssl_port != null)
//             {
//                 match = matchInfo.match_id + " -> (SSL) " + matchInfo.ssl_port;
//             }
//             activeServersField.index = (activeServersField.choices.IndexOf(match));
//         }
//
//     }
//
//     private async Task get_server_list(bool printOutput)
//     {
//         validateToken();
//         string response = await PlayFlowAPI.GetActiveServers(tokenField.value, productionRegionOptions[location.value], true);
//         Server[] servers = JsonHelper.FromJson<Server>(response);
//         List<string> active_servers = new List<string>();
//         foreach (Server server in servers)
//         {
//             string serverInfo = server.port;
//
//             if (server.ssl_enabled)
//             {
//                 serverInfo = server.port + " -> (SSL) " + server.ssl_port;
//             }
//             active_servers.Add(serverInfo);
//         }
//         active_servers.Sort();
//         activeServersField.choices = active_servers;
//
//         if (active_servers.IsNullOrEmpty())
//         {
//             activeServersField.value = "";
//             activeServersField.index = 0;
//         }
//         
//         if (activeServersField.value.IsNullOrEmpty())
//         {
//             activeServersField.index = 0;
//         }
//
//         if (printOutput)
//         {
//             outputLogs(response);
//         }
//     }
//
//     private async Task get_status()
//     {
//         validateToken();
//         if (activeServersField.value.IsNullOrEmpty())
//         {
//             outputLogs("No server selected");
//             return;
//         }
//         string response = await PlayFlowAPI.GetServerStatus(tokenField.value, activeServersField.value);
//         outputLogs(response);
//     }
//
//     private async Task get_logs()
//     {
//         if (activeServersField.value.IsNullOrEmpty())
//         {
//             outputLogs("No server selected");
//             return;
//         }
//         string playflow_logs = await PlayFlowAPI.GetServerLogs(tokenField.value, productionRegionOptions[location.value], activeServersField.value);
//         string[] split = playflow_logs.Split(new[] {"\\n"}, StringSplitOptions.None);
//         playflow_logs = "";
//         foreach (string s in split)
//             playflow_logs += s + "\n";
//         
//         outputLogs(playflow_logs);
//     }
//     
//     private async Task restart_server()
//     {
//         if (activeServersField.value.IsNullOrEmpty())
//         {
//             outputLogs("No server selected");
//             return;
//         }
//         string response =
//             await PlayFlowAPI.RestartServer(tokenField.value, productionRegionOptions[location.value],  argumentsField.value, enableSSL.value.ToString(), activeServersField.value);
//         outputLogs(response);
//         
//     }
//     
//     private async Task stop_server()
//     {
//         if (activeServersField.value.IsNullOrEmpty())
//         {
//             outputLogs("No server selected");
//             return;
//         }
//         string response =
//             await PlayFlowAPI.StopServer(tokenField.value, productionRegionOptions[location.value],  activeServersField.value);
//         outputLogs(response);
//         await get_server_list(false);
//         activeServersField.index = 0;
//     }
//
//
//     private void outputLogs(string s)
//     {
//         Debug.Log( DateTime.Now.ToString() + " PlayFlow Logs: " +  s);
//         logs.value = s;
//     }
//
//     private async void OnRefreshPressed()
//     {
//         //
//         showProgress();
//         await get_server_list(true);
//         hideProgress();
//     }
//
//     private async void OnGetStatusPressed()
//     {
//         //
//         showProgress();
//         await get_status();
//         hideProgress();
//         
//     }
//
//     private async void OnGetLogsPressed()
//     {
//         //
//         showProgress();
//         await get_logs();
//         hideProgress();
//         
//     }
//
//     private async void OnRestartPressed()
//     {
//         //
//         showProgress();
//         await restart_server();
//         hideProgress();
//     }
//
//     private async void OnStopPressed()
//     {
//         //
//         showProgress();
//         await stop_server();
//         hideProgress();
//     }
//     
//     private void OnUploadPressed()
//     {
//         showProgress(25);
//         validateToken();
//         
//         BuildTarget standaloneTarget = EditorUserBuildSettings.selectedStandaloneTarget;
//         BuildTargetGroup currentBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(standaloneTarget);
// #if UNITY_2021_2_OR_NEWER
//         StandaloneBuildSubtarget currentSubTarget = EditorUserBuildSettings.standaloneBuildSubtarget;
// #endif
//         try
//         {
//             PlayFlowBuilder.BuildServer(devBuild.value);
//             string zipFile = PlayFlowBuilder.ZipServerBuild();
//             string directoryToZip = Path.GetDirectoryName(PlayFlowBuilder.defaultPath);
//             showProgress(50);
//             string targetfile = Path.Combine(directoryToZip, @".." + Path.DirectorySeparatorChar + "Server.zip");
//             showProgress(75);
//             string playflow_logs = PlayFlowAPI.Upload(targetfile, tokenField.value, productionRegionOptions[location.value]);
//             outputLogs(playflow_logs);
//         }
//         finally
//         {
//             EditorUserBuildSettings.SwitchActiveBuildTarget(currentBuildTargetGroup, standaloneTarget);
// #if UNITY_2021_2_OR_NEWER
//             EditorUserBuildSettings.standaloneBuildSubtarget = currentSubTarget;
// #endif
//             hideProgress();
//             EditorUtility.ClearProgressBar();
//
//         }
//         //
//     }
//
//     private async void OnUploadStatusPressed()
//     {
//         showProgress();
//         validateToken();
//         string response = await PlayFlowAPI.Get_Upload_Version(tokenField.value);
//         Debug.Log(response);
//         outputLogs(response);
//         hideProgress();
//         //
//     }
//
//     private async void OnStartPressed()
//     {
//         showProgress();
//         validateToken();
//         string response = await PlayFlowAPI.StartServer(tokenField.value, productionRegionOptions[location.value], argumentsField.value, enableSSL.value.ToString(), sslValue.value.ToString(), instance_types[instanceType.value], isProductionToken(tokenField.value));
//         MatchInfo matchInfo = JsonUtility.FromJson<MatchInfo>(response);
//         setCurrentServer(matchInfo);
//         outputLogs(response);
//         hideProgress();
//     }
//
//     private void showProgress()
//     {
//         progress.value = 50;
//         progress.title = "Loading...";
//         progress.style.display = DisplayStyle.Flex;
//     }
//     
//     private void showProgress(float value)
//     {
//         progress.value = value;
//         progress.title = "Loading...";
//         progress.style.display = DisplayStyle.Flex;
//     }
//     
//     private void hideProgress()
//     {
//         progress.value = 0;
//         progress.style.display = DisplayStyle.None;
//     }
// }
//
//
// [Serializable]
// public class Server
// {
//     public string ssl_port;
//     public bool ssl_enabled;
//     public string server_arguments;
//     public string status;
//     public string port;
// }
//
//
// [Serializable]
// public class MatchInfo
// {
//     public string match_id;
//     public string server_url;
//     public string ssl_port;
// }
//
// public static class JsonHelper
// {
//     public static T[] FromJson<T>(string json)
//     {
//         Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
//         return wrapper.servers;
//     }
//
//     public static string ToJson<T>(T[] array)
//     {
//         Wrapper<T> wrapper = new Wrapper<T>();
//         wrapper.servers = array;
//         return JsonUtility.ToJson(wrapper);
//     }
//
//     public static string ToJson<T>(T[] array, bool prettyPrint)
//     {
//         Wrapper<T> wrapper = new Wrapper<T>();
//         wrapper.servers = array;
//         return JsonUtility.ToJson(wrapper, prettyPrint);
//     }
//
//     [Serializable]
//     private class Wrapper<T>
//     {
//         public T[] servers;
//     }
// }
//
// #endif