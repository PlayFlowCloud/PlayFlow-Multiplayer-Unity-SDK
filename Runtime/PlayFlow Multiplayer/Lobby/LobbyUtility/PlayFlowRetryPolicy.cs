using System;
using System.Collections;
using UnityEngine;

namespace PlayFlow
{
    /// <summary>
    /// Retry policy with exponential backoff for API operations
    /// Platform-safe implementation using coroutines
    /// </summary>
    internal class PlayFlowRetryPolicy
    {
        private readonly int maxRetries;
        private readonly float baseDelaySeconds;
        private readonly float maxDelaySeconds;
        private readonly float backoffMultiplier;
        
        public PlayFlowRetryPolicy(int maxRetries = 3, float baseDelaySeconds = 0.1f, float maxDelaySeconds = 5f, float backoffMultiplier = 2f)
        {
            this.maxRetries = maxRetries;
            this.baseDelaySeconds = baseDelaySeconds;
            this.maxDelaySeconds = maxDelaySeconds;
            this.backoffMultiplier = backoffMultiplier;
        }
        
        /// <summary>
        /// Execute an operation with retry logic using coroutines
        /// </summary>
        public IEnumerator ExecuteWithRetry(
            Func<IEnumerator> operation,
            Action<object> onSuccess,
            Action<Exception> onFailure,
            Func<Exception, bool> shouldRetry = null)
        {
            Exception lastException = null;
            float currentDelay = baseDelaySeconds;
            
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    Debug.Log($"[PlayFlowRetryPolicy] Retry attempt {attempt}/{maxRetries} after {currentDelay:F1}s delay");
                    yield return new WaitForSeconds(currentDelay);
                }
                
                bool operationSucceeded = false;
                object result = null;
                Exception currentException = null;
                
                // Execute the operation
                yield return ExecuteOperation(
                    operation,
                    (res) => { result = res; operationSucceeded = true; },
                    (ex) => { currentException = ex; operationSucceeded = false; }
                );
                
                if (operationSucceeded)
                {
                    onSuccess?.Invoke(result);
                    yield break;
                }
                
                lastException = currentException;
                
                // Check if we should retry
                if (shouldRetry != null && !shouldRetry(currentException))
                {
                    Debug.LogWarning($"[PlayFlowRetryPolicy] Operation failed and retry condition not met: {currentException?.Message}");
                    break;
                }
                
                // Calculate next delay with exponential backoff
                currentDelay = Mathf.Min(currentDelay * backoffMultiplier, maxDelaySeconds);
                
                if (attempt < maxRetries)
                {
                    Debug.LogWarning($"[PlayFlowRetryPolicy] Operation failed (attempt {attempt + 1}), will retry: {currentException?.Message}");
                }
            }
            
            // All retries exhausted
            Debug.LogError($"[PlayFlowRetryPolicy] All {maxRetries + 1} attempts failed");
            onFailure?.Invoke(lastException ?? new Exception("Operation failed after all retries"));
        }
        
        /// <summary>
        /// Helper to execute a single operation
        /// </summary>
        private IEnumerator ExecuteOperation(Func<IEnumerator> operation, Action<object> onSuccess, Action<Exception> onFailure)
        {
            bool hasError = false;
            Exception caughtException = null;
            
            // Execute the operation outside of try-catch to avoid yield restrictions
            IEnumerator operationEnumerator = null;
            
            try
            {
                operationEnumerator = operation();
            }
            catch (Exception e)
            {
                hasError = true;
                caughtException = e;
            }
            
            if (hasError)
            {
                onFailure(caughtException);
                yield break;
            }
            
            // Execute the enumerator if we got one
            if (operationEnumerator != null)
            {
                while (true)
                {
                    object current = null;
                    bool moveNextSucceeded = false;
                    
                    try
                    {
                        moveNextSucceeded = operationEnumerator.MoveNext();
                        if (moveNextSucceeded)
                        {
                            current = operationEnumerator.Current;
                        }
                    }
                    catch (Exception e)
                    {
                        onFailure(e);
                        yield break;
                    }
                    
                    if (!moveNextSucceeded)
                    {
                        // Operation completed successfully
                        onSuccess(null);
                        yield break;
                    }
                    
                    yield return current;
                }
            }
            else
            {
                // Operation returned null enumerator
                onSuccess(null);
            }
        }
        
        /// <summary>
        /// Default retry condition - retry on network errors and timeouts
        /// </summary>
        public static bool DefaultShouldRetry(Exception exception)
        {
            if (exception == null) return false;
            
            var message = exception.Message.ToLower();
            
            // Retry on network errors
            if (message.Contains("network") || 
                message.Contains("timeout") || 
                message.Contains("connection") ||
                message.Contains("unreachable"))
            {
                return true;
            }
            
            // Don't retry on client errors (4xx)
            if (message.Contains("400") || 
                message.Contains("401") || 
                message.Contains("403") || 
                message.Contains("404"))
            {
                return false;
            }
            
            // Retry on server errors (5xx)
            if (message.Contains("500") || 
                message.Contains("502") || 
                message.Contains("503") || 
                message.Contains("504"))
            {
                return true;
            }
            
            // Default to retry for unknown errors
            return true;
        }
    }
    
    /// <summary>
    /// Extension methods for easier retry usage
    /// </summary>
    internal static class RetryPolicyExtensions
    {
        private static readonly PlayFlowRetryPolicy defaultPolicy = new PlayFlowRetryPolicy();
        
        /// <summary>
        /// Execute with default retry policy
        /// </summary>
        public static IEnumerator WithRetry(
            this MonoBehaviour behaviour,
            Func<IEnumerator> operation,
            Action<object> onSuccess,
            Action<Exception> onFailure)
        {
            return defaultPolicy.ExecuteWithRetry(
                operation,
                onSuccess,
                onFailure,
                PlayFlowRetryPolicy.DefaultShouldRetry
            );
        }
        
        /// <summary>
        /// Execute with custom retry policy
        /// </summary>
        public static IEnumerator WithRetry(
            this MonoBehaviour behaviour,
            Func<IEnumerator> operation,
            Action<object> onSuccess,
            Action<Exception> onFailure,
            PlayFlowRetryPolicy policy)
        {
            return policy.ExecuteWithRetry(
                operation,
                onSuccess,
                onFailure,
                PlayFlowRetryPolicy.DefaultShouldRetry
            );
        }
    }
} 