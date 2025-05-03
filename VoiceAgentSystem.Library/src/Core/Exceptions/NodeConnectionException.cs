
using System;

namespace VoiceBotSystem.Core.Exceptions
{
    /// <summary>
    /// Exception thrown for node connection errors
    /// </summary>
    public class NodeConnectionException : Exception
    {
        /// <summary>
        /// Source node ID
        /// </summary>
        public string SourceNodeId { get; }

        /// <summary>
        /// Target node ID
        /// </summary>
        public string TargetNodeId { get; }

        /// <summary>
        /// Create node connection exception
        /// </summary>
        public NodeConnectionException(string message) : base(message)
        {
        }

        /// <summary>
        /// Create node connection exception with source and target
        /// </summary>
        public NodeConnectionException(string sourceNodeId, string targetNodeId, string message)
            : base(message)
        {
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
        }

        /// <summary>
        /// Create node connection exception with inner exception
        /// </summary>
        public NodeConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Create node connection exception with source, target, and inner exception
        /// </summary>
        public NodeConnectionException(string sourceNodeId, string targetNodeId, string message, Exception innerException)
            : base(message, innerException)
        {
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
        }
    }
}
