using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VoiceBotSystem.Core.Interfaces
{
    /// <summary>
    /// Interface defining a connection between nodes in a pipeline
    /// </summary>
    public interface INodeConnection
    {
        /// <summary>
        /// Unique identifier for the connection
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Source node of the connection
        /// </summary>
        INode Source { get; }

        /// <summary>
        /// Target node of the connection
        /// </summary>
        INode Target { get; }

        /// <summary>
        /// Optional label for the connection
        /// </summary>
        string Label { get; set; }

        /// <summary>
        /// Whether the connection is enabled
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Connection priority (lower numbers have higher priority)
        /// </summary>
        int Priority { get; set; }

        /// <summary>
        /// Connection-specific configuration
        /// </summary>
        IDictionary<string, object> Configuration { get; }

        /// <summary>
        /// Validates the connection
        /// </summary>
        Task<bool> ValidateAsync();

        /// <summary>
        /// Transfer data through the connection
        /// </summary>
        Task<bool> TransferDataAsync(object data, ProcessingContext context);

        /// <summary>
        /// Get configuration value
        /// </summary>
        T GetConfigurationValue<T>(string key, T defaultValue = default);

        /// <summary>
        /// Set configuration value
        /// </summary>
        void SetConfigurationValue<T>(string key, T value);
    }
}
