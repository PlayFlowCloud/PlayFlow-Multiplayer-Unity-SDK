using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PlayFlow
{
    public class UnityNetworkManager : MonoBehaviour, INetworkManager
    {
        private PlayFlowSettings _settings;
        
        public void Initialize(PlayFlowSettings settings)
        {
            _settings = settings;
        }
        
        private string GetErrorMessage(UnityWebRequest webRequest)
        {
            if (!string.IsNullOrEmpty(webRequest.downloadHandler?.text))
            {
                return $"{webRequest.error} - {webRequest.downloadHandler.text}";
            }
            
            return webRequest.error ?? "Unknown error";
        }
        
        // INetworkManager implementation
        public IEnumerator Get(string url, string apiKey, System.Action<string> onSuccess, System.Action<string> onError)
        {
            using (var webRequest = UnityWebRequest.Get(url))
            {
                webRequest.timeout = (int)_settings.requestTimeout;
                webRequest.SetRequestHeader("api-key", apiKey);
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(webRequest.downloadHandler.text);
                }
                else
                {
                    onError?.Invoke(GetErrorMessage(webRequest));
                }
            }
        }
        
        public IEnumerator Post(string url, string json, string apiKey, System.Action<string> onSuccess, System.Action<string> onError)
        {
            using (var webRequest = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("api-key", apiKey);
                webRequest.timeout = (int)_settings.requestTimeout;
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(webRequest.downloadHandler.text);
                }
                else
                {
                    onError?.Invoke(GetErrorMessage(webRequest));
                }
            }
        }
        
        public IEnumerator Put(string url, string json, string apiKey, System.Action<string> onSuccess, System.Action<string> onError)
        {
            using (var webRequest = UnityWebRequest.Put(url, json))
            {
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("api-key", apiKey);
                webRequest.timeout = (int)_settings.requestTimeout;
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(webRequest.downloadHandler.text);
                }
                else
                {
                    onError?.Invoke(GetErrorMessage(webRequest));
                }
            }
        }
        
        public IEnumerator Delete(string url, string apiKey, System.Action<string> onSuccess, System.Action<string> onError)
        {
            using (var webRequest = UnityWebRequest.Delete(url))
            {
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("api-key", apiKey);
                webRequest.timeout = (int)_settings.requestTimeout;
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(webRequest.downloadHandler.text);
                }
                else
                {
                    onError?.Invoke(GetErrorMessage(webRequest));
                }
            }
        }
    }
}
 