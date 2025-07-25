using System;
using UnityEngine;

namespace PlayFlow
{
    /// <summary>
    /// Tracks SSE connection quality metrics for debugging and optimization
    /// </summary>
    public class SSEConnectionMetrics
    {
        private int _eventsReceived = 0;
        private int _reconnectCount = 0;
        private int _errorCount = 0;
        private float _connectionStartTime = 0;
        private float _totalConnectedTime = 0;
        private float _lastEventTime = 0;
        
        public int EventsReceived => _eventsReceived;
        public int ReconnectCount => _reconnectCount;
        public int ErrorCount => _errorCount;
        public float AverageEventInterval => _eventsReceived > 1 ? _totalConnectedTime / _eventsReceived : 0;
        public float ConnectionUptime => Time.time - _connectionStartTime;
        
        public void OnConnected()
        {
            _connectionStartTime = Time.time;
            _lastEventTime = Time.time;
        }
        
        public void OnDisconnected()
        {
            if (_connectionStartTime > 0)
            {
                _totalConnectedTime += Time.time - _connectionStartTime;
            }
        }
        
        public void OnEventReceived()
        {
            _eventsReceived++;
            _lastEventTime = Time.time;
        }
        
        public void OnReconnect()
        {
            _reconnectCount++;
        }
        
        public void OnError()
        {
            _errorCount++;
        }
        
        public void Reset()
        {
            _eventsReceived = 0;
            _reconnectCount = 0;
            _errorCount = 0;
            _connectionStartTime = 0;
            _totalConnectedTime = 0;
            _lastEventTime = 0;
        }
        
        public string GetReport()
        {
            return $"SSE Metrics - Events: {_eventsReceived}, Reconnects: {_reconnectCount}, " +
                   $"Errors: {_errorCount}, Uptime: {ConnectionUptime:F1}s, " +
                   $"Avg Interval: {AverageEventInterval:F2}s";
        }
    }
}