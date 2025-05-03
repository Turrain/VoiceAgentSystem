using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Nodes.Base
{
    /// <summary>
    /// Base implementation for all nodes
    /// </summary>
    public abstract class NodeBase : INode
    {
        /// <summary>
        /// Unique identifier for the node
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Display name for the node
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether the node is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Node-specific configuration parameters
        /// </summary>
        public IDictionary<string, object> Configuration { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Input connections to this node
        /// </summary>
        public IList<INodeConnection> InputConnections { get; } = new List<INodeConnection>();

        /// <summary>
        /// Output connections from this node
        /// </summary>
        public IList<INodeConnection> OutputConnections { get; } = new List<INodeConnection>();

        /// <summary>
        /// Additional metadata about the node
        /// </summary>
        public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

        // Node statistics
        protected int ProcessingCount = 0;
        protected TimeSpan TotalProcessingTime = TimeSpan.Zero;

        // Node state
        protected bool IsInitialized = false;

        /// <summary>
        /// Event raised when node processing starts
        /// </summary>
        public event EventHandler<NodeEventArgs> ProcessingStarted;

        /// <summary>
        /// Event raised when node processing completes
        /// </summary>
        public event EventHandler<NodeEventArgs> ProcessingCompleted;

        /// <summary>
        /// Create a new node
        /// </summary>
        protected NodeBase(string id, string name)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));

            // Set default metadata
            Metadata["Type"] = GetType().Name;
            Metadata["CreatedAt"] = DateTimeOffset.Now;
        }

        /// <summary>
        /// Initialize the node
        /// </summary>
        public virtual async Task Initialize()
        {
            if (IsInitialized)
                return;

            await OnInitializeAsync();
            IsInitialized = true;

            Metadata["InitializedAt"] = DateTimeOffset.Now;
        }

        /// <summary>
        /// Shutdown the node and release resources
        /// </summary>
        public virtual async Task Shutdown()
        {
            if (!IsInitialized)
                return;

            await OnShutdownAsync();
            IsInitialized = false;

            Metadata["ShutdownAt"] = DateTimeOffset.Now;
        }

        /// <summary>
        /// Validate the node's configuration and connections
        /// </summary>
        public virtual Task<bool> ValidateAsync()
        {
            // Basic validation - can be extended by derived classes
            return Task.FromResult(true);
        }

        /// <summary>
        /// Reset the node to its initial state
        /// </summary>
        public virtual async Task Reset()
        {
            ProcessingCount = 0;
            TotalProcessingTime = TimeSpan.Zero;
            await OnResetAsync();

            Metadata["LastReset"] = DateTimeOffset.Now;
        }

        /// <summary>
        /// Get configuration value
        /// </summary>
        public T GetConfigurationValue<T>(string key, T defaultValue = default)
        {
            if (Configuration.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;

            return defaultValue;
        }

        /// <summary>
        /// Set configuration value
        /// </summary>
        public void SetConfigurationValue<T>(string key, T value)
        {
            Configuration[key] = value;
        }

        /// <summary>
        /// Called during initialization
        /// </summary>
        protected virtual Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called during shutdown
        /// </summary>
        protected virtual Task OnShutdownAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called during reset
        /// </summary>
        protected virtual Task OnResetAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Track processing metrics
        /// </summary>
        protected void TrackProcessing(TimeSpan processingTime)
        {
            ProcessingCount++;
            TotalProcessingTime += processingTime;

            // Update metadata
            Metadata["ProcessingCount"] = ProcessingCount;
            Metadata["AverageProcessingTime"] = TotalProcessingTime.TotalMilliseconds / ProcessingCount;
            Metadata["LastProcessedAt"] = DateTimeOffset.Now;
        }

        /// <summary>
        /// Raise processing started event
        /// </summary>
        protected virtual void OnProcessingStarted(object input, ProcessingContext context)
        {
            ProcessingStarted?.Invoke(this, new NodeEventArgs(this, input, null, context));
        }

        /// <summary>
        /// Raise processing completed event
        /// </summary>
        protected virtual void OnProcessingCompleted(object input, object output, ProcessingContext context, TimeSpan processingTime)
        {
            ProcessingCompleted?.Invoke(this, new NodeEventArgs(this, input, output, context, processingTime));
        }
    }

    /// <summary>
    /// Event arguments for node events
    /// </summary>
    public class NodeEventArgs : EventArgs
    {
        /// <summary>
        /// Node that raised the event
        /// </summary>
        public INode Node { get; }

        /// <summary>
        /// Input data
        /// </summary>
        public object Input { get; }

        /// <summary>
        /// Output data
        /// </summary>
        public object Output { get; }

        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }

        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Processing time if applicable
        /// </summary>
        public TimeSpan? ProcessingTime { get; }

        /// <summary>
        /// Create node event args
        /// </summary>
        public NodeEventArgs(INode node, object input, object output, ProcessingContext context, TimeSpan? processingTime = null)
        {
            Node = node;
            Input = input;
            Output = output;
            Context = context;
            Timestamp = DateTimeOffset.Now;
            ProcessingTime = processingTime;
        }
    }
}
