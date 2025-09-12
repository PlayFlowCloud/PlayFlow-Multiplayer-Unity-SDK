using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR

namespace PlayFlow.SDK.Editor
{
    public class PlayFlowCloudDeploy : EditorWindow
    {
    [SerializeField] private VisualTreeAsset _tree;
    private Button QuickStart;
    private Button documentationButton;
    private Button discordButton;
    private Button pricingButton;
    private Button getTokenButton;
    private Button uploadButton;
    private Button directUploadButton;
    private Button uploadStatusButton;
    private Button ButtonLaunchSimplified;

    private TextField tokenField;
    private TextField servertag;

    private Toggle devBuild;
    
    private DropdownField sceneDropDown;

    private Toggle buildSettingsToggle;

    private ProgressBar progress;
    
    private List<string> sceneList;

    [MenuItem("PlayFlow/PlayFlow Cloud")]
    public static void ShowEditor()
    {
        var window = GetWindow<PlayFlowCloudDeploy>();
        window.titleContent = new GUIContent("PlayFlow Cloud");
    }

    public Dictionary<string, string> productionRegionOptions = new Dictionary<string, string>
    {
        {"North America East (North Virginia)", "us-east"},
        {"North America West (California)", "us-west"},
        {"North America West (Oregon)", "us-west-2"},
        {"Europe (Stockholm)", "eu-north"},
        {"Europe (France)", "eu-west"},
        {"South Asia (Mumbai)", "ap-south"},
        {"South East Asia (Singapore)", "sea"},
        {"East Asia (Korea)", "ea"},
        {"East Asia (Japan)", "ap-north"},
        {"Australia (Sydney)", "ap-southeast"},
        {"South Africa (Cape Town)", "south-africa"},
        {"South America (Brazil)", "south-america-brazil"},
        {"South America (Chile)", "south-america-chile"}
    };

    private void CreateGUI()
    {
        if (_tree == null)
        {
            string[] guids = AssetDatabase.FindAssets("playflow t:VisualTreeAsset");
            string path = "";
            if (guids.Length > 0)
            {
                // To avoid ambiguity, we're assuming the first result is the correct one.
                // It's best to remove duplicate/old SDK folders from your project.
                path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            }

            if (_tree == null)
            {
                Debug.LogError("PlayFlowCloud Error: Could not load UI asset 'playflow.uxml'. Make sure it is in your project.");
                return;
            }
        }
        
        _tree.CloneTree(rootVisualElement);
        documentationButton = rootVisualElement.Q<Button>("ButtonDocumentation");
        discordButton = rootVisualElement.Q<Button>("ButtonDiscord");
        pricingButton = rootVisualElement.Q<Button>("ButtonPricing");
        getTokenButton = rootVisualElement.Q<Button>("ButtonGetToken");
        uploadButton = rootVisualElement.Q<Button>("ButtonUpload");
        directUploadButton = rootVisualElement.Q<Button>("ButtonDirectUpload");
        uploadStatusButton = rootVisualElement.Q<Button>("ButtonUploadStatus");
        QuickStart =  rootVisualElement.Q<Button>("QuickStart");
        ButtonLaunchSimplified = rootVisualElement.Q<Button>("ButtonLaunchSimplified");
        
        progress = rootVisualElement.Q<ProgressBar>("progress");

        sceneList = new List<string>();

        tokenField = rootVisualElement.Q<TextField>("TextToken");

        servertag = rootVisualElement.Q<TextField>("servertag");
        
        devBuild = rootVisualElement.Q<Toggle>("DevelopmentBuild");
        
        buildSettingsToggle = rootVisualElement.Q<Toggle>("UseBuildSettings");
        
        sceneDropDown = rootVisualElement.Q<DropdownField>("sceneDropDown");

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled) sceneList.Add(scene.path);
        }
        sceneDropDown.choices = sceneList;
        if (sceneList.Count > 0) sceneDropDown.index = 0;

        sceneDropDown.RegisterCallback<MouseDownEvent>(OnSceneDropDown);
        
        documentationButton.clicked += OnDocumentationPressed;
        discordButton.clicked += OnDiscordPressed;
        pricingButton.clicked += OnPricingPressed;
        getTokenButton.clicked += OnGetTokenPressed;
        QuickStart.clicked += OnQuickStartPressed;
        ButtonLaunchSimplified.clicked += OnLaunchSimplifiedPressed;

        uploadButton.clicked += OnUploadPressed;
        directUploadButton.clicked += OnDirectUploadPressed;
        uploadStatusButton.clicked += OnUploadStatusPressed;
        buildSettingsToggle.RegisterValueChangedCallback(HandleBuildSettings);
    }

    private void OnSceneDropDown(MouseDownEvent clickEvent)
    {
        sceneList.Clear();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled) sceneList.Add(scene.path);
        }
        sceneDropDown.choices = sceneList;
        if (sceneDropDown.index < 0 && sceneList.Count > 0) sceneDropDown.index = 0;
    }
    
    private void HandleBuildSettings(ChangeEvent<bool> value)
    {
        if (value.newValue)
        {
            sceneDropDown.style.display = DisplayStyle.None;
        }
        else
        {
            sceneDropDown.style.display = DisplayStyle.Flex;
            sceneList.Clear();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                 if (scene.enabled) sceneList.Add(scene.path);
            }
            sceneDropDown.choices = sceneList;
            if (sceneDropDown.index < 0 && sceneList.Count > 0) sceneDropDown.index = 0;
        }
    }

    private void OnDocumentationPressed()
    {
        Application.OpenURL("https://documentation.playflowcloud.com");
    }
    
    private void OnQuickStartPressed()
    {
        Application.OpenURL("https://documentation.playflowcloud.com/quickstart");
    }

    private void OnDiscordPressed()
    {
        Application.OpenURL("https://discord.gg/P5w45Vx5Q8");
    }

    private void OnPricingPressed()
    {
        Application.OpenURL("https://www.playflowcloud.com/#pricing");
    }

    private void OnGetTokenPressed()
    {
        Application.OpenURL("https://app.playflowcloud.com");
    }

    private void outputLogs(string s, bool isError = false)
    {
        string formattedMessage = DateTime.Now.ToString() + " PlayFlow: " + PrettyPrintJson(s);
        if (isError)
        {
            Debug.LogError(formattedMessage);
        }
        else
        {
            Debug.Log(formattedMessage);
        }
    }
    
    
    
    public static string PrettyPrintJson(string json)
    {
        try
        {
            var indent = 0;
            var quoted = false;
            var result = new System.Text.StringBuilder();
            for (var i = 0; i < json.Length; i++)
            {
                var ch = json[i];
                switch (ch)
                {
                    case '{':
                    case '[':
                        result.Append(ch);
                        if (!quoted)
                        {
                            result.AppendLine();
                            result.Append(new string(' ', ++indent * 2));
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            result.AppendLine();
                            result.Append(new string(' ', --indent * 2));
                        }
                        result.Append(ch);
                        break;
                    case '"':
                        result.Append(ch);
                        var escaped = false;
                        var index = i;
                        while (index > 0 && json[--index] == '\\')
                        {
                            escaped = !escaped;
                        }
                        if (!escaped)
                        {
                            quoted = !quoted;
                        }
                        break;
                    case ',':
                        result.Append(ch);
                        if (!quoted)
                        {
                            result.AppendLine();
                            result.Append(new string(' ', indent * 2));
                        }
                        break;
                    case ':':
                        result.Append(ch);
                        if (!quoted)
                        {
                            result.Append(" ");
                        }
                        break;
                    default:
                        result.Append(ch);
                        break;
                }
            }
            return result.ToString();
        }
        catch
        {
            return json;
        }
    }

    private void OnUploadPressed()
    {
        if (servertag.value == null || servertag.value.Equals(""))
        {
            outputLogs("Server tag cannot be empty. Please enter a server tag or use `default` as the server tag", true);
            return;
        }
        
        BuildTarget originalTarget = EditorUserBuildSettings.selectedStandaloneTarget;
        BuildTargetGroup originalBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(originalTarget);
#if UNITY_2021_2_OR_NEWER
        StandaloneBuildSubtarget originalSubTarget = EditorUserBuildSettings.standaloneBuildSubtarget;
#endif
        
#if UNITY_2021_2_OR_NEWER
        if (!CheckLinuxServerModule())
        {
            // outputLogs("Linux server module not correctly set up. Please check console.", true);
            // return; // Decide if this should be a hard stop or a warning.
        }
#endif
        
        showProgress(25);
        
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
        {
            outputLogs("Linux build target is not installed in the editor.", true);
            hideProgress();
            return;
        }
        try
        {
            uploadButton.SetEnabled(false);
            List<string> scenesToUpload = new List<string>();
            if (buildSettingsToggle.value)
            {
                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                {
                    if (scene.enabled)
                    {
                        scenesToUpload.Add(scene.path);
                    }
                }
            }
            else
            {
                if (sceneDropDown.value == null || sceneDropDown.value.Equals(""))
                {
                    outputLogs("Select a scene first before uploading", true);
                    throw new Exception("Select a scene first before uploading");
                }
                scenesToUpload.Add(sceneDropDown.value);
            }
            
            if (scenesToUpload.Count == 0)
            {
                 outputLogs("No scenes selected for build.", true);
                 throw new Exception("No scenes selected for build.");
            }

            bool success = PlayFlowBuilder.BuildServer(devBuild.value, scenesToUpload);
            if (!success)
            {
                outputLogs("Build failed. Check console for details.", true);
                throw new Exception("Build failed");
            }
            string zipFile = PlayFlowBuilder.ZipServerBuild();
            
            // Show progress for upload phase
            showProgress(50);
            outputLogs("Starting upload... You can cancel the upload using the Unity progress bar.");
            
            Action onUploadComplete = () =>
            {
                // This gets called whether upload succeeds, fails, or is cancelled
                uploadButton.SetEnabled(true);
                hideProgress();
                // Check Unity console for success/failure messages
            };
            
            Action<float> onUploadProgress = (uploadProgress) =>
            {
                // Map upload progress (0-1) to our progress bar range (50-100)
                float mappedProgress = 50 + (uploadProgress * 50);
                showProgress(mappedProgress);
            };

            PlayFlowAPI.Upload(zipFile, tokenField.value, servertag.value, onUploadComplete, onUploadProgress);
        }
        catch (Exception e)
        {
            outputLogs($"Upload process failed: {e.Message}", true);
            // Ensure UI is re-enabled even if the pre-upload steps fail
            uploadButton.SetEnabled(true);
            hideProgress();
            EditorUtility.ClearProgressBar();
        }
    }

    private void OnDirectUploadPressed()
    {
        if (servertag.value == null || servertag.value.Equals(""))
        {
            outputLogs("Server tag cannot be empty. Please enter a server tag or use `default` as the server tag", true);
            return;
        }

        string zipPath = EditorUtility.OpenFilePanel("Select Server Build Zip", "", "zip");
        if (string.IsNullOrEmpty(zipPath))
        {
            return; // User cancelled
        }

        if (!File.Exists(zipPath))
        {
            outputLogs("Selected file does not exist.", true);
            return;
        }

        try
        {
            directUploadButton.SetEnabled(false);
            showProgress(25);
            outputLogs($"Starting direct upload of: {Path.GetFileName(zipPath)}");

            Action onUploadComplete = () =>
            {
                directUploadButton.SetEnabled(true);
                hideProgress();
            };

            Action<float> onUploadProgress = (uploadProgress) =>
            {
                float mappedProgress = 25 + (uploadProgress * 75);
                showProgress(mappedProgress);
            };

            PlayFlowAPI.Upload(zipPath, tokenField.value, servertag.value, onUploadComplete, onUploadProgress);
        }
        catch (Exception e)
        {
            outputLogs($"Direct upload failed: {e.Message}", true);
            directUploadButton.SetEnabled(true);
            hideProgress();
            EditorUtility.ClearProgressBar();
        }
    }

    private void OnUploadStatusPressed() 
    {
        string apiKey = tokenField.value;
        if (string.IsNullOrEmpty(apiKey))
        {
            outputLogs("API Token is required to view builds.", true);
            return;
        }
        string projectID = PlayFlowAPI.GetProjectID(apiKey);
        if (string.IsNullOrEmpty(projectID))
        {
            outputLogs("Could not retrieve Project ID. Please check your API token and network connection.", true);
            return;
        }
        Application.OpenURL($"https://app.playflowcloud.com/projects/{projectID}/builds");
    }

    private void OnLaunchSimplifiedPressed()
    {
        string apiKey = tokenField.value;
        if (string.IsNullOrEmpty(apiKey))
        {
            outputLogs("API Token is required to launch servers.", true);
            return;
        }
        string projectID = PlayFlowAPI.GetProjectID(apiKey);
        if (string.IsNullOrEmpty(projectID))
        {
            outputLogs("Could not retrieve Project ID. Please check your API token and network connection.", true);
            return;
        }
        Application.OpenURL($"https://app.playflowcloud.com/projects/{projectID}/servers");
    }

#pragma warning disable 0168
    private void showProgress(float value)
    {
        try
        {
            progress.value = value;
            progress.title = $"Progress: {Mathf.FloorToInt(value)}%";
            progress.style.display = DisplayStyle.Flex;
        }
        catch (Exception)
        {
        }
    }
    
    private void hideProgress()
    {
        try
        {
            progress.value = 0;
            progress.style.display = DisplayStyle.None;
        }
        catch (Exception)
        {
        }
    }
#pragma warning restore 0168

    public static bool CheckLinuxServerModule()
    {
        bool isLinuxTargetSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        if (!isLinuxTargetSupported)
        {
            Debug.LogError("Linux Standalone target is not installed. Please install it via Unity Hub.");
            return false;
        }

        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Server)
            {
                Debug.LogWarning("For PlayFlow server builds, ensure your active build target is Linux and subtarget is Server.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to verify/set Linux Server build subtarget: " + e.Message);
            return false;
        }
        return true;
    }
}
}

#endif