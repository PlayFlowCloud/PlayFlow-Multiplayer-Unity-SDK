using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PlayFlow
{
    public class UnityNetworkManager : MonoBehaviour, INetworkQueue, INetworkManager
    {
        private Queue<NetworkRequest> _requestQueue = new Queue<NetworkRequest>();
        private bool _isProcessing;
        private PlayFlowSettings _settings;
        
        public void Initialize(PlayFlowSettings settings)
        {
            _settings = settings;
        }
        
        public void EnqueueRequest(NetworkRequest request)
        {
            _requestQueue.Enqueue(request);
            
            if (!_isProcessing)
            {
                StartCoroutine(ProcessQueue());
            }
        }
        
        private IEnumerator ProcessQueue()
        {
            _isProcessing = true;
            
            while (_requestQueue.Count > 0)
            {
                var request = _requestQueue.Dequeue();
                yield return ProcessRequest(request);
            }
            
            _isProcessing = false;
        }
        
        private IEnumerator ProcessRequest(NetworkRequest request)
        {
            using (var webRequest = CreateUnityWebRequest(request))
            {
                webRequest.timeout = (int)_settings.requestTimeout;
                
                var operation = webRequest.SendWebRequest();
                
                while (!operation.isDone)
                {
                    request.OnProgress?.Invoke(operation.progress);
                    yield return null;
                }
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    var response = new NetworkResponse
                    {
                        StatusCode = webRequest.responseCode,
                        Data = webRequest.downloadHandler?.text ?? "",
                        Headers = webRequest.GetResponseHeaders()
                    };
                    
                    request.OnSuccess?.Invoke(response);
                    request.IsComplete = true;
                }
                else
                {
                    HandleError(webRequest, request);
                }
            }
        }
        
        private UnityWebRequest CreateUnityWebRequest(NetworkRequest request)
        {
            UnityWebRequest webRequest;
            
            switch (request.Method.ToUpper())
            {
                case "POST":
                    webRequest = UnityWebRequest.PostWwwForm(BuildUrl(request.Endpoint), "POST");
                    if (!string.IsNullOrEmpty(request.Data))
                    {
                        byte[] bodyRaw = Encoding.UTF8.GetBytes(request.Data);
                        webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        webRequest.downloadHandler = new DownloadHandlerBuffer();
                        webRequest.SetRequestHeader("Content-Type", "application/json");
                    }
                    break;
                    
                case "PUT":
                    webRequest = UnityWebRequest.Put(BuildUrl(request.Endpoint), request.Data ?? "");
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    break;
                    
                case "DELETE":
                    webRequest = UnityWebRequest.Delete(BuildUrl(request.Endpoint));
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    break;
                    
                default: // GET
                    webRequest = UnityWebRequest.Get(BuildUrl(request.Endpoint));
                    break;
            }
            
            // Add API key header
            if (!string.IsNullOrEmpty(_settings.apiKey))
            {
                webRequest.SetRequestHeader("api-key", _settings.apiKey);
            }
            
            // Add custom headers
            if (request.Headers != null)
            {
                foreach (var header in request.Headers)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }
            }
            
            return webRequest;
        }
        
        private string BuildUrl(string endpoint)
        {
            if (endpoint.StartsWith("http://") || endpoint.StartsWith("https://"))
            {
                return endpoint;
            }
            
            var baseUrl = _settings.baseUrl.TrimEnd('/');
            var cleanEndpoint = endpoint.TrimStart('/');
            return $"{baseUrl}/{cleanEndpoint}";
        }
        
        private void HandleError(UnityWebRequest webRequest, NetworkRequest request)
        {
            var errorMessage = GetErrorMessage(webRequest);
            
            if (request.RetryCount < _settings.maxRetryAttempts && ShouldRetry(webRequest))
            {
                request.RetryCount++;
                
                if (_settings.debugLogging)
                {
                    Debug.Log($"[UnityNetworkManager] Retrying request (attempt {request.RetryCount}/{_settings.maxRetryAttempts}): {request.Endpoint}");
                }
                
                StartCoroutine(RetryRequest(request));
            }
            else
            {
                request.OnError?.Invoke(errorMessage);
                request.IsComplete = true;
            }
        }
        
        private string GetErrorMessage(UnityWebRequest webRequest)
        {
            if (!string.IsNullOrEmpty(webRequest.downloadHandler?.text))
            {
                return $"{webRequest.error} - {webRequest.downloadHandler.text}";
            }
            
            return webRequest.error ?? "Unknown error";
        }
        
        private bool ShouldRetry(UnityWebRequest webRequest)
        {
            // Don't retry client errors (4xx)
            if (webRequest.responseCode >= 400 && webRequest.responseCode < 500)
            {
                return false;
            }
            
            // Retry on network errors and server errors (5xx)
            return webRequest.result == UnityWebRequest.Result.ConnectionError ||
                   webRequest.result == UnityWebRequest.Result.ProtocolError ||
                   webRequest.responseCode >= 500;
        }
        
        private IEnumerator RetryRequest(NetworkRequest request)
        {
            var delay = _settings.retryDelay * request.RetryCount;
            yield return new WaitForSeconds(delay);
            EnqueueRequest(request);
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