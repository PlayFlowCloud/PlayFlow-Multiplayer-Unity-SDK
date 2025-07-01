using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlayFlow
{
    /// <summary>
    /// Request queue to prevent race conditions and ensure ordered operations
    /// Platform-safe implementation using coroutines for all Unity platforms
    /// </summary>
    internal class PlayFlowRequestQueue : MonoBehaviour
    {
        private Queue<QueuedRequest> requestQueue = new Queue<QueuedRequest>();
        private bool isProcessing = false;
        private Coroutine processingCoroutine;
        
        // Singleton pattern for easy access
        private static PlayFlowRequestQueue instance;
        public static PlayFlowRequestQueue Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("PlayFlowRequestQueue");
                    instance = go.AddComponent<PlayFlowRequestQueue>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        
        private class QueuedRequest
        {
            public string OperationName { get; set; }
            public Func<IEnumerator> Operation { get; set; }
            public Action<Exception> OnError { get; set; }
            public bool IsCritical { get; set; }
            public float TimeoutSeconds { get; set; }
            public DateTime QueuedAt { get; set; }
        }
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        /// <summary>
        /// Enqueue an operation to be executed
        /// </summary>
        public void EnqueueOperation(string operationName, Func<IEnumerator> operation, Action<Exception> onError = null, bool isCritical = false, float timeoutSeconds = 30f)
        {
            var request = new QueuedRequest
            {
                OperationName = operationName,
                Operation = operation,
                OnError = onError,
                IsCritical = isCritical,
                TimeoutSeconds = timeoutSeconds,
                QueuedAt = DateTime.Now
            };
            
            requestQueue.Enqueue(request);
            
            // Start processing if not already running
            if (!isProcessing && processingCoroutine == null)
            {
                processingCoroutine = StartCoroutine(ProcessQueue());
            }
        }
        
        /// <summary>
        /// Process queued requests sequentially
        /// </summary>
        private IEnumerator ProcessQueue()
        {
            isProcessing = true;
            
            while (requestQueue.Count > 0)
            {
                var request = requestQueue.Dequeue();
                
                // Check if request has expired
                var queueTime = (DateTime.Now - request.QueuedAt).TotalSeconds;
                if (queueTime > request.TimeoutSeconds)
                {
                    Debug.LogWarning($"[PlayFlowRequestQueue] Request '{request.OperationName}' expired after {queueTime:F1}s in queue");
                    request.OnError?.Invoke(new TimeoutException($"Request expired after {queueTime:F1}s in queue"));
                    continue;
                }
                
                bool operationCompleted = false;
                Exception operationError = null;
                
                // Create a coroutine to run the operation with timeout
                Coroutine operationCoroutine = StartCoroutine(RunOperationWithTimeout(
                    request.Operation(),
                    request.TimeoutSeconds,
                    () => operationCompleted = true,
                    (error) => { operationError = error; operationCompleted = true; }
                ));
                
                // Wait for operation to complete
                while (!operationCompleted)
                {
                    yield return null;
                }
                
                // Handle any errors
                if (operationError != null)
                {
                    Debug.LogError($"[PlayFlowRequestQueue] Operation '{request.OperationName}' failed: {operationError.Message}");
                    request.OnError?.Invoke(operationError);
                    
                    // If it's a critical operation, clear the queue
                    if (request.IsCritical)
                    {
                        Debug.LogWarning($"[PlayFlowRequestQueue] Critical operation failed, clearing queue");
                        requestQueue.Clear();
                        break;
                    }
                }
                
                // Small delay between operations to prevent overwhelming the server
                yield return new WaitForSeconds(0.1f);
            }
            
            isProcessing = false;
            processingCoroutine = null;
        }
        
        /// <summary>
        /// Run an operation with timeout
        /// </summary>
        private IEnumerator RunOperationWithTimeout(IEnumerator operation, float timeoutSeconds, Action onComplete, Action<Exception> onError)
        {
            bool completed = false;
            Exception error = null;
            
            // Start the operation
            Coroutine operationCoroutine = StartCoroutine(RunOperation(operation, () => completed = true, (e) => { error = e; completed = true; }));
            
            // Start timeout timer
            float timeoutTimer = 0f;
            while (!completed && timeoutTimer < timeoutSeconds)
            {
                timeoutTimer += Time.deltaTime;
                yield return null;
            }
            
            if (!completed)
            {
                // Operation timed out
                StopCoroutine(operationCoroutine);
                onError(new TimeoutException($"Operation timed out after {timeoutSeconds}s"));
            }
            else if (error != null)
            {
                onError(error);
            }
            else
            {
                onComplete();
            }
        }
        
        /// <summary>
        /// Run a single operation
        /// </summary>
        private IEnumerator RunOperation(IEnumerator operation, Action onComplete, Action<Exception> onError)
        {
            while (true)
            {
                try
                {
                    if (!operation.MoveNext())
                    {
                        break; // Coroutine finished successfully
                    }
                }
                catch (Exception e)
                {
                    onError(e);
                    yield break; // Stop processing this coroutine
                }

                yield return operation.Current;
            }
            
            onComplete();
        }
        
        /// <summary>
        /// Clear all pending requests
        /// </summary>
        public void ClearQueue()
        {
            requestQueue.Clear();
        }
        
        /// <summary>
        /// Get the number of pending requests
        /// </summary>
        public int GetPendingCount()
        {
            return requestQueue.Count;
        }
        
        /// <summary>
        /// Check if currently processing
        /// </summary>
        public bool IsProcessing
        {
            get { return isProcessing; }
        }
        
        private void OnDestroy()
        {
            if (processingCoroutine != null)
            {
                StopCoroutine(processingCoroutine);
            }
            requestQueue.Clear();
            if (instance == this)
            {
                instance = null;
            }
        }
    }
} 