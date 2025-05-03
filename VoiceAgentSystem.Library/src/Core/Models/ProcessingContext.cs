using System;
using System.Collections.Generic;
using System.Threading;

namespace VoiceBotSystem.Core
{
    /// <summary>
    /// Context for pipeline processing
    /// </summary>
    public class ProcessingContext
    {
        /// <summary>
        /// Unique session identifier
        /// </summary>
        public Guid SessionId { get; }

        /// <summary>
        /// Session-level data (persists across processing calls)
        /// </summary>
        public Dictionary<string, object> SessionData { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Transient data (only for current processing call)
        /// </summary>
        public Dictionary<string, object> TransientData { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Processing start time
        /// </summary>
        public DateTimeOffset StartTime { get; }

        /// <summary>
        /// Time of last update
        /// </summary>
        public DateTimeOffset LastUpdated { get; private set; }

        /// <summary>
        /// Cancellation token for processing
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Log messages
        /// </summary>
        public List<LogMessage> LogMessages { get; } = new List<LogMessage>();

        /// <summary>
        /// Create processing context
        /// </summary>
        public ProcessingContext(Guid? sessionId = null, CancellationToken cancellationToken = default)
        {
            SessionId = sessionId ?? Guid.NewGuid();
            StartTime = DateTimeOffset.Now;
            LastUpdated = StartTime;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Update the last updated time
        /// </summary>
        public void Update()
        {
            LastUpdated = DateTimeOffset.Now;
        }

        /// <summary>
        /// Get session data
        /// </summary>
        public T GetSessionData<T>(string key, T defaultValue = default)
        {
            if (SessionData.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;

            return defaultValue;
        }

        /// <summary>
        /// Set session data
        /// </summary>
        public void SetSessionData<T>(string key, T value)
        {
            SessionData[key] = value;
            Update();
        }

        /// <summary>
        /// Get transient data
        /// </summary>
        public T GetTransientData<T>(string key, T defaultValue = default)
        {
            if (TransientData.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;

            return defaultValue;
        }

        /// <summary>
        /// Set transient data
        /// </summary>
        public void SetTransientData<T>(string key, T value)
        {
            TransientData[key] = value;
        }

        /// <summary>
        /// Remove transient data
        /// </summary>
        public void RemoveTransientData(string key)
        {
            if (TransientData.ContainsKey(key))
                TransientData.Remove(key);
        }

        /// <summary>
        /// Log an information message
        /// </summary>
        public void LogInformation(string message)
        {
            LogMessages.Add(new LogMessage(LogLevel.Information, message));
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        public void LogWarning(string message)
        {
            LogMessages.Add(new LogMessage(LogLevel.Warning, message));
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        public void LogError(string message)
        {
            LogMessages.Add(new LogMessage(LogLevel.Error, message));
        }
    }

    /// <summary>
    /// Log level enum
    /// </summary>
    public enum LogLevel
    {
        Information,
        Warning,
        Error
    }

    /// <summary>
    /// Log message
    /// </summary>
    public class LogMessage
    {
        /// <summary>
        /// Log level
        /// </summary>
        public LogLevel Level { get; }

        /// <summary>
        /// Message text
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Create log message
        /// </summary>
        public LogMessage(LogLevel level, string message)
        {
            Level = level;
            Message = message;
            Timestamp = DateTimeOffset.Now;
        }
    }
}
