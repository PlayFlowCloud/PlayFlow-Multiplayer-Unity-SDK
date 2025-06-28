// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Threading.Tasks;
// using PlayFlow;
// using UnityEditor;
// using UnityEngine;
// using UnityEngine.UIElements;

// #if UNITY_EDITOR

// public class PlayFlowCloudDeploy : EditorWindow
// {
//     [SerializeField] private VisualTreeAsset _tree;
//     private Button QuickStart;
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
//     private Button resetButton;
//     private Button resetStatusButton;
//     private Button getTagsButton;
//     private Button ButtonDeleteTag;
//     private Button ButtonUpdateServer;
    
//     private Coroutine currentCoroutine;


//     private Button ButtonCopyIP;
//     private Button ButtonCopyMatchId;
//     private Button ButtonCopyServerUrl;
//     private Button ButtonViewDashboard;

//     private TextField tokenField;
//     private TextField sslValue;
//     private TextField argumentsField;
//     private TextField logs;
//     private TextField servertag;


//     private Foldout ConfigFoldout;
//     private Foldout UploadFoldout;
//     private Foldout LaunchServersFoldout;
//     private Foldout ManageFoldout;
//     private Foldout LogsFoldout;
//     private Foldout TagsFoldout;

    


//     private Toggle enableSSL;
//     private Toggle devBuild;
    
//     private DropdownField location;
//     private DropdownField instanceType;
//     private DropdownField activeServersField;
//     private DropdownField sceneDropDown;
//     private DropdownField LaunchTagDropdown;
    
//     private DropdownField tagsDropDown;


//     private Toggle buildSettingsToggle;

//     private ProgressBar progress;
    
//     private List<string> sceneList;

//     private Label uploadedInfo;
    
//     private Label ServerStatusLabel;
    
//     private TextField uploadInfoHidden;

//     private TextField lastRefreshedKey;

//     private VisualElement LaunchingIcon;
//     private VisualElement OnlineIcon;

//     private double startTime;



//     [MenuItem("PlayFlow/PlayFlow Cloud")]
//     public static void ShowEditor()
//     {
//         var window = GetWindow<PlayFlowCloudDeploy>();
//         window.titleContent = new GUIContent("PlayFlow Cloud");
//     }

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
//         {"Australia (Sydney)", "ap-southeast"},
//         {"South Africa (Cape Town)", "south-africa"},
//         {"South America (Brazil)", "south-america-brazil"},
//         {"South America (Chile)", "south-america-chile"}
//     };

//     Dictionary<string, string> instance_types = new Dictionary<string, string>
//     {
//         {"Small - 2 VCPU 1GB RAM", "small"},
//         {"Medium - 2 VCPU 2GB RAM", "medium"},
//         {"Large - 2 VCPU 4GB RAM", "large"},
//     };

//     private Dictionary<string, string> scenes = new Dictionary<string, string>();
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
//         resetButton =  rootVisualElement.Q<Button>("ResetInstance");
//         resetStatusButton =  rootVisualElement.Q<Button>("InstanceStatus");
//         QuickStart =  rootVisualElement.Q<Button>("QuickStart");
//         getTagsButton = rootVisualElement.Q<Button>("ButtonGetTags");
//         tagsDropDown = rootVisualElement.Q<DropdownField>("BuildTagsDropdown");
//         ButtonDeleteTag = rootVisualElement.Q<Button>("ButtonDeleteTag");
//         LaunchTagDropdown = rootVisualElement.Q<DropdownField>("LaunchTagDropdown");
//         ButtonCopyIP = rootVisualElement.Q<Button>("ButtonCopyIP");
//         ButtonCopyMatchId = rootVisualElement.Q<Button>("ButtonCopyMatchId");
//         ButtonUpdateServer = rootVisualElement.Q<Button>("ButtonUpdateServer");
//         ButtonCopyServerUrl = rootVisualElement.Q<Button>("ButtonCopyServerUrl");
//         ButtonViewDashboard = rootVisualElement.Q<Button>("ButtonViewDashboard");
        
//         ServerStatusLabel = rootVisualElement.Q<Label>("ServerStatusLabel");
//         OnlineIcon = rootVisualElement.Q<VisualElement>("OnlineIcon");
//         LaunchingIcon = rootVisualElement.Q<VisualElement>("LaunchingIcon");
        
//         lastRefreshedKey = rootVisualElement.Q<TextField>("lastRefreshedKey");
        
//         ConfigFoldout = rootVisualElement.Q<Foldout>("ConfigFoldout");
//         UploadFoldout = rootVisualElement.Q<Foldout>("UploadFoldout");
//         LaunchServersFoldout = rootVisualElement.Q<Foldout>("LaunchServersFoldout");
//         ManageFoldout = rootVisualElement.Q<Foldout>("ManageFoldout");
//         LogsFoldout = rootVisualElement.Q<Foldout>("LogsFoldout");
//         TagsFoldout = rootVisualElement.Q<Foldout>("TagsFoldout");

        
//         logs = rootVisualElement.Q<TextField>("logs");
//         progress = rootVisualElement.Q<ProgressBar>("progress");

//         sceneList = new List<string>();

//         tokenField = rootVisualElement.Q<TextField>("TextToken");
//         tokenField.RegisterValueChangedCallback(HandleToken);

//         argumentsField = rootVisualElement.Q<TextField>("TextArgs");
//         sslValue = rootVisualElement.Q<TextField>("sslValue");
//         servertag = rootVisualElement.Q<TextField>("servertag");
        
//         uploadInfoHidden = rootVisualElement.Q<TextField>("uploadedInfoValue");

//         devBuild = rootVisualElement.Q<Toggle>("DevelopmentBuild");
        
//         buildSettingsToggle = rootVisualElement.Q<Toggle>("UseBuildSettings");
        
        
        
//         sceneDropDown = rootVisualElement.Q<DropdownField>("sceneDropDown");
//         enableSSL = rootVisualElement.Q<Toggle>("enableSSL");
//         sslValue.style.display = enableSSL.value ? DisplayStyle.Flex : DisplayStyle.None;

        
        
//         location = rootVisualElement.Q<DropdownField>("locationDropdown");
//         location.choices = productionRegionOptions.Keys.ToList();

//         if (location.value == null || location.value.Equals(""))
//         {
//             location.index = 0;
//         }
        
//         foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
//         {
//             sceneList.Add(scene.path);
//         }
//         sceneDropDown.choices = sceneList;

//         instanceType = rootVisualElement.Q<DropdownField>("instanceTypeDropdown");
//         instanceType.choices = instance_types.Keys.ToList();

//         if (instanceType.value == null  || instanceType.value.Equals(""))
//         {
//             instanceType.index = 0;
//         }

//         activeServersField = rootVisualElement.Q<DropdownField>("ActiveServersDropdown");
//         activeServersField.choices = new List<string>();

//         sceneDropDown.RegisterCallback<MouseDownEvent>(OnSceneDropDown);
        
//         //When the user clicks the dropdown first time, we populate the list
//         LaunchTagDropdown.RegisterCallback<MouseDownEvent>(OnLaunchTagDropDown);
//         tagsDropDown.RegisterCallback<MouseDownEvent>(OnLaunchTagDropDown);

        
//         documentationButton.clicked += OnDocumentationPressed;
//         discordButton.clicked += OnDiscordPressed;
//         pricingButton.clicked += OnPricingPressed;
//         getTokenButton.clicked += OnGetTokenPressed;
//         QuickStart.clicked += OnQuickStartPressed;

//         uploadButton.clicked += OnUploadPressed;
//         uploadStatusButton.clicked += OnUploadStatusPressed;
//         startButton.clicked += OnStartPressed;
//         enableSSL.RegisterValueChangedCallback(HandleSSL);
//         buildSettingsToggle.RegisterValueChangedCallback(HandleBuildSettings);

//         refreshButton.clicked += OnRefreshPressed;
//         getStatusButton.clicked += OnGetStatusPressed;
//         getLogsButton.clicked += OnGetLogsPressed;
//         restartButton.clicked += OnRestartPressed;
//         ButtonUpdateServer.clicked += OnUpdatePressed;
//         stopButton.clicked += OnStopPressed;

//         resetButton.clicked += OnResetPressed;
//         resetStatusButton.clicked += OnResetStatusPressed;
//         getTagsButton.clicked += OnGetTagsPressed;
//         ButtonDeleteTag.clicked += OnDeleteTagPressed;
        
        
        
//         ButtonCopyIP.clicked += OnCopyIPPressed;
//         ButtonCopyMatchId.clicked += OnCopyMatchIdPressed;
//         ButtonCopyServerUrl.clicked += OnCopyServerUrlPressed;
        
//         ButtonViewDashboard.clicked += OnViewDashboardPressed;

//         enableManagementButtons(false);
//         // Start the timer
//         startTime = EditorApplication.timeSinceStartup;
        
//         // Add the update function to EditorApplication.update
//         EditorApplication.update += CheckTokenAfterDelay;
        
//         activeServersField.RegisterValueChangedCallback(evt =>
//         {
//             // Debug.Log("Active Server Changed");
//             // StopCheckingServerStatus(); // Stop any existing checking process

//             if (!string.IsNullOrEmpty(evt.newValue))
//             {
//                 string[] split = evt.newValue.Split(' ');
//                 string matchId = split[0];
//                 OnGetStatusPressed();
//             }
//         });

//     }

//     private void CheckTokenAfterDelay()
//     {
//         // Check if 5 seconds have passed
//         if (EditorApplication.timeSinceStartup - startTime >= 1)
//         {
//             // Remove the update function from EditorApplication.update
//             EditorApplication.update -= CheckTokenAfterDelay;
//             // Debug.Log("Initializing PlayFlow API");
//             // Check the token value and validate it if not empty
//             if (!string.IsNullOrEmpty(tokenField.value))
//             {
//                 validateToken();
//                 OnUploadStatusPressed();
                
//                 OnRefreshPressed();
//             }
//         }
//     }
    
// private bool isCheckingServerStatus = false;
// private double lastCheckTime;
// private double checkInterval = 10; // 10
// private double maxCheckDuration = 360; // 6 minutes

// private void StartCheckingServerStatus(string matchId)
// {
//     isCheckingServerStatus = true;
//     currentMatchId = matchId;
//     startTime = EditorApplication.timeSinceStartup; // Reset the start time
//     lastCheckTime = EditorApplication.timeSinceStartup;
//     EditorApplication.update += CheckServerStatusPeriodically;
// }

// private void StopCheckingServerStatus()
// {
//     isCheckingServerStatus = false;
//     EditorApplication.update -= CheckServerStatusPeriodically;
// }

// private async void CheckServerStatusPeriodically()
// {
//     if (!isCheckingServerStatus)
//     {
//         return;
//     }

//     double currentTime = EditorApplication.timeSinceStartup;

//     if (currentTime - startTime >= maxCheckDuration)
//     {
//         SetServerStatus("Timed out", false);
//         StopCheckingServerStatus();
//         return;
//     }

//     if (currentTime - lastCheckTime < checkInterval)
//     {
//         return;
//     }

//     lastCheckTime = currentTime;

//     MatchInfo matchInfo = await GetServerStatusAsync(tokenField.value, currentMatchId);


//     UpdateServerStatus(matchInfo);
// }



// private void UpdateServerStatus(MatchInfo matchInfo)
// {
//     if (matchInfo == null || matchInfo.match_id == null)
//     {
//         SetServerStatus("Offline", false);
//         StopCheckingServerStatus();
//         return;
//     }

//     switch (matchInfo.status)
//     {
//         case "running":
//             SetServerStatus("Online", true, matchInfo.ip, matchInfo.match_id, matchInfo.server_url, matchInfo.playflow_api_version);
//             StopCheckingServerStatus();
//             break;
//         case "launching":
//             SetServerStatus("Launching...", false);
//             break;
//         default:
//             SetServerStatus("Offline", false);
//             StopCheckingServerStatus();
//             break;
//     }
    

// }

// private void SetServerStatus(string status, bool isOnline, string ip = null, string matchId = null, string serverUrl = null, int playflowApiVersion = 9)
// {
//     enableManagementButtons(isOnline, playflowApiVersion);
//     ServerStatusLabel.text = $"Current Server Status: {status}";
//     OnlineIcon.style.display = isOnline ? DisplayStyle.Flex : DisplayStyle.None;
//     LaunchingIcon.style.display = status == "Launching..." ? DisplayStyle.Flex : DisplayStyle.None;

//     if (isOnline)
//     {
//         currentIp = ip;
//         currentMatchId = matchId;
//         currentServerUrl = serverUrl;
//     }

//     // Debug.Log($"Server status updated: {status}");
// }

// private async void checkIfCurrentSelectedServerIsRunning()
// {
//     if (string.IsNullOrEmpty(activeServersField.value))
//     {
//         SetServerStatus("Offline", false);
//         return;
//     }

//     string currentServer = activeServersField.value;
//     string match_id = currentServer.Split(' ')[0];

//     MatchInfo matchInfo = await GetServerStatusAsync(tokenField.value, match_id);
//     UpdateServerStatus(matchInfo);
// }

//     private async void OnDeleteTagPressed()
//     {
//         if (tagsDropDown.value == null || tagsDropDown.value.Equals("") || tagsDropDown.value.Equals("default"))
//         {
//             outputLogs("Please select a tag to delete. Default tag cannot be deleted");
//             return;
//         }
//         validateToken();
//         showProgress();
//         string response = await PlayFlowAPI.Delete_Tag(tokenField.value, tagsDropDown.value);
//         outputLogs(response);
//         hideProgress();
        
//         //Change dropdown to index 0
//         tagsDropDown.index = 0;
        
//         OnGetTagsPressed();
//     }
    
//     private void OnLaunchTagDropDown(MouseDownEvent evt)
//     {
//         OnGetTagsPressed();
//     }
//     private async void OnGetTagsPressed()
//     {
        
//         validateToken();
//         showProgress();
//         string response = await PlayFlowAPI.Get_Tags(tokenField.value);
//         string currentTag = tagsDropDown.value;
//         string currentLaunchTag = LaunchTagDropdown.value;
//         outputLogs(response);
//         //response to json object
//         TagsResponse tagsResponse = JsonUtility.FromJson<TagsResponse>(response);
//         tagsDropDown.choices = tagsResponse.tags.ToList();
//         LaunchTagDropdown.choices = tagsResponse.tags.ToList();
        
//         // Check if there is a value currently in the dropdown, if not set the value to the first element
//         if (string.IsNullOrEmpty(tagsDropDown.value))
//         {
//             tagsDropDown.index = 0;
//         }
//         if (string.IsNullOrEmpty(LaunchTagDropdown.value))
//         {
//             LaunchTagDropdown.index = 0;
//         }
        
//         tagsDropDown.value = currentTag;
//         LaunchTagDropdown.value = currentLaunchTag;
//         hideProgress();
//     }
    

//     private void OnSceneDropDown(MouseDownEvent clickEvent)
//     {
//         foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
//         {
//             sceneList.Add(scene.path);
//         }
//         sceneDropDown.choices = sceneList;
//     }
    
//     private void HandleBuildSettings(ChangeEvent<bool> value)
//     {
//         if (value.newValue)
//         {
//             sceneDropDown.style.display = DisplayStyle.None;
//         }
//         else
//         {
//             sceneDropDown.style.display = DisplayStyle.Flex;
//         }
//     }

//     private async void OnResetPressed()
//     {
//         validateToken();
//         showProgress();
//         string response = await PlayFlowAPI.ResetInstance(tokenField.value);
//         outputLogs(response);
//         hideProgress();
//     }

//     private async void OnResetStatusPressed()
//     {
//         validateToken();
//         showProgress();
//         string response = await PlayFlowAPI.ResetStatus(tokenField.value);
//         outputLogs(response);
//         hideProgress();
//     }

//     private async void HandleToken(ChangeEvent<string> value)
//     {
//         //Check if token is valid and has > or = 32 characters before we validate
//         await validate(value.newValue);
        
//     }

//     // private int api_version = 8;

//     private async Task validate(string value)
//     {
//         string response = await PlayFlowAPI.Validate_Token(value);
//         //response json = {"success":true,"api_version":"9"}
//         //Check API version from the response json
//         //Validation_Response
//         Validation_Response validationResponse = JsonUtility.FromJson<Validation_Response>(response);

//         if (validationResponse.success == "true")
//         {
//             //Clear all the dropdowns
//             tagsDropDown.choices = new List<string>();
//             LaunchTagDropdown.choices = new List<string>();
//             activeServersField.choices = new List<string>();
            
//             //And clear the values
//             tagsDropDown.value = "default";
//             LaunchTagDropdown.value = "default";
//             activeServersField.value = "";

//             if (servertag != null && servertag.value.Equals(""))
//             {
//                 servertag.value = "default";
//             }

//             OnRefreshPressed();
//             OnGetTagsPressed();
//             OnUploadStatusPressed();
            
//             if (validationResponse.api_version == "9")
//             {
//                 enableSSL.style.display = DisplayStyle.None;
//                 sslValue.style.display = DisplayStyle.None;
                
//                 //Build Tag Enable
//                 LaunchTagDropdown.style.display = DisplayStyle.Flex;
//                 tagsDropDown.style.display = DisplayStyle.Flex;
//                 ButtonDeleteTag.style.display = DisplayStyle.Flex;
//                 getTagsButton.style.display = DisplayStyle.Flex;
//                 servertag.style.display = DisplayStyle.Flex;
//                 TagsFoldout.style.display = DisplayStyle.Flex;
                
//                 // api_version = 9;

//             }
//             else
//             {
//                 enableSSL.style.display = DisplayStyle.Flex;
//                 sslValue.style.display = DisplayStyle.Flex;
                
//                 LaunchTagDropdown.style.display = DisplayStyle.Flex;
//                 tagsDropDown.style.display = DisplayStyle.Flex;
//                 ButtonDeleteTag.style.display = DisplayStyle.Flex;
//                 getTagsButton.style.display = DisplayStyle.Flex;
//                 servertag.style.display = DisplayStyle.Flex;
//                 TagsFoldout.style.display = DisplayStyle.Flex;
                
//                 // api_version = 8;
                
                
//             }
//         }
//     }

//     private bool isProductionToken(string value)
//     {
//         return true;
//     }

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

//     private void OnDocumentationPressed()
//     {
//         System.Diagnostics.Process.Start("https://docs.playflowcloud.com");
//     }
    
//     private void OnQuickStartPressed()
//     {
//         System.Diagnostics.Process.Start("https://docs.playflowcloud.com/guides/creating-your-first-server-deployment");
//     }

//     private void OnDiscordPressed()
//     {
//         System.Diagnostics.Process.Start("https://discord.gg/P5w45Vx5Q8");
//     }

//     private void OnPricingPressed()
//     {
//         System.Diagnostics.Process.Start("https://www.playflowcloud.com/pricing");
//     }

//     private void OnGetTokenPressed()
//     {
//         System.Diagnostics.Process.Start("https://app.playflowcloud.com");
//     }

//     private void validateToken()
//     {
//         if (tokenField.value == null || tokenField.value.Equals(""))
//         {
//             outputLogs("PlayFlow Token is empty. Please provide a PlayFlow token.");
//             throw new Exception("PlayFlow Token is empty. Please provide a PlayFlow token.");
//         }
//     }

//     private async void setCurrentServer(MatchInfo matchInfo)
//     {
//         await get_server_list(true);

//         if (matchInfo != null)
//         {
//             string matchId = matchInfo.match_id;
//             string match = matchId;

//             if (matchInfo.ssl_port != null)
//             {
//                 match = matchId + " -> (SSL) " + matchInfo.ssl_port;
//             }

//             for (int i = 0; i < activeServersField.choices.Count; i++)
//             {
//                 string choice = activeServersField.choices[i];
//                 string[] split = choice.Split(' ');

//                 if (split.Length > 0 && split[0] == matchId)
//                 {
//                     activeServersField.index = i;
//                     break;
//                 }
//             }
//         }

//     }

//     private async Task get_server_list(bool printOutput)
//     {
//         validateToken();
//         string response = await PlayFlowAPI.GetActiveServers(tokenField.value, productionRegionOptions[location.value], true);
//         Server[] servers = JsonHelper.FromJson<Server>(response);
//         List<string> active_servers = new List<string>();
//         foreach (Server server in servers)
//         {
//             string serverInfo = server.match_id;
//             string portsJson = PlayFlowBackend.ExtractPortsJson(response, server.match_id);
//             server.ports = PlayFlowBackend.ParsePorts(portsJson);
            
//             outputLogs(serverInfo);
//             if (server.ssl_enabled)
//             {
//                 serverInfo = server.match_id + " -> (SSL) " + server.ssl_port;
//             }
//             active_servers.Add(serverInfo);
//         }
//         active_servers.Sort();
//         activeServersField.choices = active_servers;

//         if (active_servers == null || active_servers.Count.Equals(0))
//         {
//             activeServersField.value = "";
//             activeServersField.index = 0;
//         }
        
//         if (activeServersField.value == null || activeServersField.value.Equals(""))
//         {
//             activeServersField.index = 0;
//         }

//         if (printOutput)
//         {
//             outputLogs(response);
//         }
//     }

//     private async Task get_status()
//     {
//         validateToken();
//         if (activeServersField.value == null || activeServersField.value.Equals(""))
//         {
//             outputLogs("No server selected");
//             return;
//         }
//         string response = await PlayFlowAPI.GetServerStatus(tokenField.value, activeServersField.value);
        
//         MatchInfo matchInfo = JsonUtility.FromJson<MatchInfo>(response);
//         StartCheckingServerStatus(matchInfo.match_id);
//         outputLogs(response);
//     }

//     private async Task get_logs()
//     {
//         if (activeServersField.value == null || activeServersField.value.Equals(""))
//         {
//             outputLogs("No server selected");
//             return;
//         }
//         string playflow_logs = await PlayFlowAPI.GetServerLogs(tokenField.value, productionRegionOptions[location.value], activeServersField.value);
//         string[] split = playflow_logs.Split(new[] {"\\n"}, StringSplitOptions.None);
//         playflow_logs = "";
//         foreach (string s in split)
//             playflow_logs += s + "\n";
        
//         outputLogs(playflow_logs);
//     }
    
//     private async Task restart_server(bool update)
//     {
//         if (activeServersField.value == null || activeServersField.value.Equals(""))
//         {
//             outputLogs("No server selected");
//             return;
//         }
        
//         if (LaunchTagDropdown.value == null || LaunchTagDropdown.value.Equals(""))
//         {
//             LaunchTagDropdown.value = "default";
//         }
        
//         string response =
//             await PlayFlowAPI.RestartServer(tokenField.value, productionRegionOptions[location.value],  argumentsField.value, enableSSL.value.ToString(), activeServersField.value, update, LaunchTagDropdown.value);
//         outputLogs(response);
        
//     }
    
//     private async Task stop_server()
//     {
//         if (activeServersField.value == null || activeServersField.value.Equals(""))
//         {
//             outputLogs("No server selected");
//             return;
//         }
//         string response =
//             await PlayFlowAPI.StopServer(tokenField.value, productionRegionOptions[location.value],  activeServersField.value);
//         outputLogs(response);
//         await get_server_list(true);
//         activeServersField.index = 0;
//         checkIfCurrentSelectedServerIsRunning();
//         StopCheckingServerStatus();
//     }


//     private void outputLogs(string s, bool shouldDebug = false)
//     {
//         string formatted = PrettyPrintJson(s);
        
//         if (shouldDebug)
//         {
//             Debug.Log(DateTime.Now.ToString() + " PlayFlow Logs: " + s);
//         }


//         logs.value = formatted;
//     }
    
    
//     public static string PrettyPrintJson(string json)
// {
//     try
//     {
//         var indent = 0;
//         var quoted = false;
//         var result = new System.Text.StringBuilder();
//         for (var i = 0; i < json.Length; i++)
//         {
//             var ch = json[i];
//             switch (ch)
//             {
//                 case '{':
//                 case '[':
//                     result.Append(ch);
//                     if (!quoted)
//                     {
//                         result.AppendLine();
//                         result.Append(new string(' ', ++indent * 2));
//                     }
//                     break;
//                 case '}':
//                 case ']':
//                     if (!quoted)
//                     {
//                         result.AppendLine();
//                         result.Append(new string(' ', --indent * 2));
//                     }
//                     result.Append(ch);
//                     break;
//                 case '"':
//                     result.Append(ch);
//                     var escaped = false;
//                     var index = i;
//                     while (index > 0 && json[--index] == '\\')
//                     {
//                         escaped = !escaped;
//                     }
//                     if (!escaped)
//                     {
//                         quoted = !quoted;
//                     }
//                     break;
//                 case ',':
//                     result.Append(ch);
//                     if (!quoted)
//                     {
//                         result.AppendLine();
//                         result.Append(new string(' ', indent * 2));
//                     }
//                     break;
//                 case ':':
//                     result.Append(ch);
//                     if (!quoted)
//                     {
//                         result.Append(" ");
//                     }
//                     break;
//                 default:
//                     result.Append(ch);
//                     break;
//             }
//         }
//         return result.ToString();
//     }
//     catch
//     {
//         // If any error occurs, return the original JSON string
//         return json;
//     }
// }

//     private async void OnRefreshPressed()
//     {
//         //
//         try
//         {
//             validateToken();
//             showProgress();
//             refreshButton.SetEnabled(false);
//             await get_server_list(true);
//             checkIfCurrentSelectedServerIsRunning();
//             lastRefreshedKey.value = "Last Refreshed: " + DateTime.Now.ToString();
//         }
//         finally
//         {
//             hideProgress();
//             refreshButton.SetEnabled(true);
//         }
        
//     }
    
//     private string currentIp = "";
//     private string currentMatchId = "";
//     private string currentServerUrl = "";
    
//     private void OnCopyIPPressed()
//     {
//         if (currentIp != null && !currentIp.Equals(""))
//         {
//             GUIUtility.systemCopyBuffer = currentIp;
//             outputLogs("Copied IP to clipboard: " + currentIp);
//         }
//     }
    
    
//     private void OnCopyServerUrlPressed()
//     {
//         if (currentServerUrl != null && !currentServerUrl.Equals(""))
//         {
//             GUIUtility.systemCopyBuffer = currentServerUrl;
//             outputLogs("Copied Server URL to clipboard: " + currentServerUrl);
//         }
//     }
    
//     private void OnViewDashboardPressed()
//     {
//         if (!string.IsNullOrEmpty(currentIp))
//         {
//             validateToken();
//             string username = "admin";
//             string password = tokenField.value;
//             string url = $"http://{username}:{password}@{currentIp}";
//             OpenBrowserWithAuth(url);
//         }
//     }

//     private void OpenBrowserWithAuth(string url)
//     {
//         Application.OpenURL(url);
//     }
    
//     private void OnCopyMatchIdPressed()
//     {
//         if (currentMatchId != null && !currentMatchId.Equals(""))
//         {
//             GUIUtility.systemCopyBuffer = currentMatchId;
//             outputLogs("Copied Match ID to clipboard: " + currentMatchId);
//         }
//     }




//     private void enableManagementButtons(bool enable, int playflowAPIVersion = 9)
//     {
//         getLogsButton.SetEnabled(enable);
//         restartButton.SetEnabled(enable);
//         ButtonCopyIP.SetEnabled(enable);
//         ButtonCopyMatchId.SetEnabled(enable);
//         ButtonCopyServerUrl.SetEnabled(enable);
//         ButtonUpdateServer.SetEnabled(enable);

//         if (playflowAPIVersion == 8 && enable)
//         {
//             ButtonViewDashboard.SetEnabled(true);
//         }
//         else
//             ButtonViewDashboard.SetEnabled(false);
//     }


//     private async void OnGetStatusPressed()
//     {
//         //

//         try
//         {
//             validateToken();
//             showProgress();
//             getStatusButton.SetEnabled(false);
//             await get_status();
//             checkIfCurrentSelectedServerIsRunning();

//         }
//         finally
//         {
//             hideProgress();
//             getStatusButton.SetEnabled(true);
//         }

//     }

//     private async void OnGetLogsPressed()
//     {
//         //

//         try
//         {
//             validateToken();
//             showProgress();
//             getLogsButton.SetEnabled(false);
//             await get_logs();
//         }
//         finally
//         {
//             hideProgress();
//             getLogsButton.SetEnabled(true);
//         }

//     }

//     private async void OnRestartPressed()
//     {
//         //
//         try
//         {
//             validateToken();
//             showProgress();
//             restartButton.SetEnabled(false);
//             await restart_server(false);
//             OnGetStatusPressed();
//         }
//         finally
//         {
//             hideProgress();
//             restartButton.SetEnabled(true);
//         }
//     }
    
//     private async void OnUpdatePressed()
//     {
//         //
//         try
//         {
//             validateToken();
//             showProgress();
//             ButtonUpdateServer.SetEnabled(false);
//             await restart_server(true);
            
            
            

//             checkInterval = 2; // 5 seconds
//             maxCheckDuration = 24; // 60 seconds
            
            
//             OnGetStatusPressed();
//         }
//         finally
//         {
//             hideProgress();
//             ButtonUpdateServer.SetEnabled(true);
//         }
//     }

//     private async void OnStopPressed()
//     {
//         //
//         try
//         {
//             validateToken();
//             showProgress();
//             stopButton.SetEnabled(false);
//             await stop_server();
//         }
//         finally
//         {
//             hideProgress();
//             stopButton.SetEnabled(true);
//         }

//     }
    


//     public static bool CheckLinuxServerModule()
//     {
//         // Check if the Linux Standalone target is supported
//         bool isLinuxTargetSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
//         if (!isLinuxTargetSupported)
//         {
//             Debug.LogError("Linux Standalone target is not installed.");
//             return false;
//         }

//         try
//         {
//             // Attempt to switch to the Linux Standalone target and set the server subtarget
//             EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
//             EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
//         }
//         catch (System.Exception e)
//         {
//             Debug.LogError("Failed to set Linux Server build subtarget: " + e.Message);
//             return false;
//         }

//         return true;
//     }


//     private void OnUploadPressed()
//     {
                
//         if (servertag.value == null || servertag.value.Equals(""))
//         {
//             Debug.LogError("Server tag cannot be empty. Please enter a server tag or use `default` as the server tag");
//             return;
//         }
        
//         BuildTarget standaloneTarget = EditorUserBuildSettings.selectedStandaloneTarget;
//         BuildTargetGroup currentBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(standaloneTarget);
// #if UNITY_2021_2_OR_NEWER
//         StandaloneBuildSubtarget currentSubTarget = EditorUserBuildSettings.standaloneBuildSubtarget;
// #endif
        
// #if UNITY_2021_2_OR_NEWER
//         if (CheckLinuxServerModule() == false)
//         {
//             return;
//         }
// #endif
        
//         validateToken();
//         showProgress(25);

        

//         //Check if build target is installed in the editor
//         if (!BuildPipeline.IsBuildTargetSupported(currentBuildTargetGroup, standaloneTarget))
//         {
//             Debug.LogError("Build target " + standaloneTarget + " is not installed in the editor");
//             return;
//         }
//         try
//         {
//             uploadButton.SetEnabled(false);
//             List<string> scenesToUpload = new List<string>();
//             if (buildSettingsToggle.value)
//             {
//                 foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
//                 {
//                     if (scene.enabled)
//                     {
//                         scenesToUpload.Add(scene.path);
//                     }
//                 }
//             }
//             else
//             {
//                 if (sceneDropDown.value == null || sceneDropDown.value.Equals(""))
//                 {
//                     outputLogs("Select a scene first before uploading");
//                     throw new Exception("Select a scene first before uploading");
//                 }
//                 scenesToUpload.Add(sceneDropDown.value);
//             }
            

//             bool success = PlayFlowBuilder.BuildServer(devBuild.value, scenesToUpload);
//             if (!success)
//             {
//                 outputLogs("Build failed");
//                 throw new Exception("Build failed");
//             }
//             string zipFile = PlayFlowBuilder.ZipServerBuild();
//             string directoryToZip = Path.GetDirectoryName(PlayFlowBuilder.defaultPath);
//             showProgress(50);
//             string targetfile = Path.Combine(directoryToZip, @".." + Path.DirectorySeparatorChar + "Server.zip");
//             showProgress(75);
//             string playflow_logs = PlayFlowAPI.Upload(targetfile, tokenField.value, productionRegionOptions[location.value], servertag.value);
//             outputLogs(playflow_logs);
            
//         }
//         finally
//         {
//             uploadButton.SetEnabled(true);

//             EditorUserBuildSettings.SwitchActiveBuildTarget(currentBuildTargetGroup, standaloneTarget);
// #if UNITY_2021_2_OR_NEWER
//             EditorUserBuildSettings.standaloneBuildSubtarget = currentSubTarget;
// #endif
//             hideProgress();

//             EditorUtility.ClearProgressBar();

//         }
//         //
//     }

//     private async void OnUploadStatusPressed()
//     {
//         validateToken();
//         showProgress();
//         string response = await PlayFlowAPI.Get_Upload_Version(tokenField.value);
//         uploadInfoHidden.value = response;
//         outputLogs(response);
//         hideProgress();
//         //
//     }

    
//     private async Task<MatchInfo> GetServerStatusAsync(string token, string matchId)
//     {
//         string response = await PlayFlowAPI.GetServerStatus(token, matchId);
//         outputLogs(response);
//         return JsonUtility.FromJson<MatchInfo>(response);
//     }

//     private async void OnStartPressed()
//     {
//         string response = "";
//         if (LaunchTagDropdown.value == null || LaunchTagDropdown.value.Equals(""))
//         {
//             LaunchTagDropdown.value = "default";
//         }

//         try
//         {
//             validateToken();
//             showProgress();
//             if (enableSSL.value && !(sslValue.value == null || sslValue.value.Equals("")))
//             {
//                 try
//                 {
//                     int.Parse(sslValue.value);
//                 }
//                 catch
//                 {
//                     outputLogs("SSL Port must be a valid integer.");
//                     throw new Exception("SSL  Port must be a valid integer.");
//                 }
//             }

//             startButton.SetEnabled(false);
//             response = await PlayFlowAPI.StartServer(tokenField.value, productionRegionOptions[location.value],
//                 argumentsField.value, enableSSL.value.ToString(), sslValue.value.ToString(),
//                 instance_types[instanceType.value], isProductionToken(tokenField.value), LaunchTagDropdown.value);
//             MatchInfo matchInfo = JsonUtility.FromJson<MatchInfo>(response);
            
//             if (matchInfo != null && matchInfo.playflow_api_version != null && matchInfo.playflow_api_version == 8)
//             {

//                 checkInterval = 30; // 30 seconds if MMO type server
//                 maxCheckDuration = 360; // 6 minutes
//             }
//             else
//             {
//                 checkInterval = 5; // 10 seconds if non-MMO type server
//                 maxCheckDuration = 300; // 5 minutes
//             }



//             setCurrentServer(matchInfo);
//             ManageFoldout.value = true;
//             checkIfCurrentSelectedServerIsRunning();
//         }
//         finally{
//             outputLogs(response);
//             hideProgress();
//             startButton.SetEnabled(true);

//         }

      
//     }
// #pragma warning disable 0168

//     private void showProgress()
//     {
//         try {
//         progress.value = 50;
//         progress.title = "Loading...";
//         progress.style.display = DisplayStyle.Flex;
//         }
//         catch (Exception e)
//         {
//             Debug.Log("Progress Bar UI Not found. ProgressBar will be hidden. Loading in progress");
//         }
//     }
    
//     private void showProgress(float value)
//     {
//         try
//         {
//             progress.value = value;
//             progress.title = "Loading...";
//             progress.style.display = DisplayStyle.Flex;
//         }
//         catch (Exception e)
//         {
//             Debug.Log("Progress Bar UI Not found. ProgressBar will be hidden. Loading in progress");
//         }

//     }
    
    
//     private void hideProgress()
//     {
//         try
//         {
//             progress.value = 0;
//             progress.style.display = DisplayStyle.None;
//         }
//         catch (Exception e)
//         {
//             Debug.Log("Progress Bar UI Not found. ProgressBar will be hidden");
//         }
//     }
// #pragma warning restore 0168

// }


// //{"tags":["default","serverversion1","randombugtest","testtttttt"]}
// [Serializable]
// public class TagsResponse
// {
//     public string[] tags;
// }


// [Serializable]
// public class Server
// {
//     public string ssl_port;
//     public bool ssl_enabled;
//     public string server_arguments;
//     public string status;
//     public string port;
//     public string match_id;
//     public string server_url;
//     public int playflow_api_version;
//     public Dictionary<string, int> ports = new Dictionary<string, int>();

// }

// [Serializable]
// public class Validation_Response
// {
//     public string success;
//     public string api_version;
// }



// [Serializable]
// public class MatchInfo
// {
//     public string match_id;
//     public string server_url;
//     public string ssl_port;
//     public string status;
//     public string ip;
//     public bool ssl_enabled;
//     public int playflow_api_version;

// }

// public static class JsonHelper
// {
//     public static T[] FromJson<T>(string json)
//     {
//         Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
//         return wrapper.servers;
//     }

//     public static string ToJson<T>(T[] array)
//     {
//         Wrapper<T> wrapper = new Wrapper<T>();
//         wrapper.servers = array;
//         return JsonUtility.ToJson(wrapper);
//     }

//     public static string ToJson<T>(T[] array, bool prettyPrint)
//     {
//         Wrapper<T> wrapper = new Wrapper<T>();
//         wrapper.servers = array;
//         return JsonUtility.ToJson(wrapper, prettyPrint);
//     }

//     [Serializable]
//     private class Wrapper<T>
//     {
//         public T[] servers;
//     }
// }

// #endif