using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class PlayFlowAPI
{
    // Determine if we're in development or production based on Unity Editor
    // private static string API_URL = "http://localhost:8000";
    private static string API_URL =  "https://api.computeflow.cloud";
    public class PlayFlowWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            request.Timeout = 10000000;
            return request;
        }
    }

    [System.Serializable]
    private class ProjectIdResponse
    {
        public string project_id;
    }
    
    [System.Serializable]
    private class BuildPresignedUploadResponse
    {
        public string build_id;
        public string name;
        public string upload_url;
        public string message;
    }
    
    [System.Serializable]
    private class BuildUploadResponse
    {
        public string build_id;
        public string name;
        public string status;
        public string message;
        public int version;
        public int? file_size;
        public string upload_time;
    }
    
    public static string GetProjectID(string apiKey)
    {
        string projectID = "";
        try
        {
            string actionUrl = $"{API_URL}/v2/project";

            using (PlayFlowWebClient client = new PlayFlowWebClient())
            {
                client.Headers.Add("api-key", apiKey);
                string response = client.DownloadString(actionUrl);
                var responseData = JsonUtility.FromJson<ProjectIdResponse>(response);
                projectID = responseData.project_id;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        return projectID;
    }

    private static BuildPresignedUploadResponse GetPresignedUploadUrl(string apiKey, string buildName = "default")
    {
        try
        {
            string actionUrl = $"{API_URL}/v2/builds/builds/upload-url?name={System.Uri.EscapeDataString(buildName)}";

            using (PlayFlowWebClient client = new PlayFlowWebClient())
            {
                client.Headers.Add("api-key", apiKey);
                client.Headers.Add("Content-Type", "application/json");
                
                // Make POST request to get pre-signed URL
                string response = client.UploadString(actionUrl, "POST", "");
                return JsonUtility.FromJson<BuildPresignedUploadResponse>(response);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to get pre-signed upload URL: {e.Message}");
            throw;
        }
    }

    public static void Upload(string fileLocation, string apiKey, string buildName = "default", Action onComplete = null, Action<float> onProgress = null)
    {
        // This method now starts the upload process but does not wait for it to complete.
        // It uses an editor coroutine pattern with EditorApplication.update.
        try
        {
            EditorUtility.DisplayProgressBar("Uploading to PlayFlow", "Getting upload URL...", 0.25f);
            onProgress?.Invoke(0.0f);

            var presignedResponse = GetPresignedUploadUrl(apiKey, buildName);

            if (string.IsNullOrEmpty(presignedResponse.upload_url))
            {
                throw new Exception("Failed to get pre-signed upload URL");
            }

            // Add ServicePointManager settings for SSL/TLS
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            
            var uwr = new UnityWebRequest(presignedResponse.upload_url, "PUT")
            {
                uploadHandler = new UploadHandlerFile(fileLocation),
                downloadHandler = new DownloadHandlerBuffer(),
                certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey()
            };
            uwr.SetRequestHeader("Content-Type", "application/octet-stream");
            uwr.timeout = 600; // 10 minutes timeout

            var operation = uwr.SendWebRequest();

            EditorApplication.CallbackFunction onUpdate = null;
            onUpdate = () =>
            {
                if (!operation.isDone)
                {
                    float progress = operation.progress;
                    onProgress?.Invoke(progress);
                    
                    bool cancelled = EditorUtility.DisplayCancelableProgressBar("Uploading to PlayFlow",
                        $"Uploading build... {Mathf.FloorToInt(progress * 100)}% (Press Cancel to abort)",
                        progress);

                    if (cancelled)
                    {
                        uwr.Abort();
                        EditorUtility.ClearProgressBar();
                        Debug.LogWarning("Upload cancelled by user.");
                        EditorApplication.update -= onUpdate;
                        uwr.Dispose();
                        onComplete?.Invoke();
                    }
                    return;
                }

                EditorApplication.update -= onUpdate;
                EditorUtility.ClearProgressBar();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to upload build: {uwr.error} - {uwr.downloadHandler?.text}");
                }
                else
                {
                    Debug.Log("Build uploaded successfully.");
                    onProgress?.Invoke(1.0f); // Only report 100% on success
                }
                
                uwr.Dispose();
                onComplete?.Invoke();
            };

            EditorApplication.update += onUpdate;
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"Upload failed: {e.Message}");
            onComplete?.Invoke();
        }
    }
    
    public class AcceptAllCertificatesSignedWithASpecificKeyPublicKey : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            // Accept all certificates for now. In production, you should validate properly.
            return true;
        }
    }
}
