using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VoiceBotSystem.Core.Interfaces
{
    /// <summary>
    /// Base interface for all nodes in a pipeline
    /// </summary>
    public interface INode
    {
        /// <summary>
        /// Unique identifier for the node
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name for the node
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Whether the node is enabled and actively processing
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Node-specific configuration parameters
        /// </summary>
        IDictionary<string, object> Configuration { get; }

        /// <summary>
        /// Input connections to this node
        /// </summary>
        IList<INodeConnection> InputConnections { get; }

        /// <summary>
        /// Output connections from this node
        /// </summary>
        IList<INodeConnection> OutputConnections { get; }

        /// <summary>
        /// Additional metadata about the node
        /// </summary>
        IDictionary<string, object> Metadata { get; }

        /// <summary>
        /// Initialize the node
        /// </summary>
        Task Initialize();

        /// <summary>
        /// Shutdown the node and release resources
        /// </summary>
        Task Shutdown();

        /// <summary>
        /// Validate the node's configuration and connections
        /// </summary>
        Task<bool> ValidateAsync();

        /// <summary>
        /// Reset the node to its initial state
        /// </summary>
        Task Reset();
    }
}
