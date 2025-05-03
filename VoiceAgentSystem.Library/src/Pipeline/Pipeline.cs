using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Exceptions;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Pipeline
{
    /// <summary>
    /// Pipeline for processing audio through a connected graph of nodes
    /// </summary>
    public class Pipeline
    {
        /// <summary>
        /// Pipeline ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Pipeline name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether the pipeline is currently running
        /// </summary>
        public bool IsRunning { get; private set; }

        // Collections for nodes and connections
        private readonly List<INode> _nodes = new List<INode>();
        private readonly List<INodeConnection> _connections = new List<INodeConnection>();
        private readonly Dictionary<string, INode> _nodeById = new Dictionary<string, INode>();
        private readonly Dictionary<string, INodeConnection> _connectionById = new Dictionary<string, INodeConnection>();

        // Entry points to the pipeline
        private readonly List<IAudioInputNode> _entryPoints = new List<IAudioInputNode>();

        // Exit points from the pipeline
        private readonly List<IAudioOutputNode> _exitPoints = new List<IAudioOutputNode>();

        // Pipeline execution monitoring
        private readonly List<DataTransferEventArgs> _executionLog = new List<DataTransferEventArgs>();
        private readonly SemaphoreSlim _executionLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Event raised when pipeline execution starts
        /// </summary>
        public event EventHandler<PipelineExecutionEventArgs> ExecutionStarted;

        /// <summary>
        /// Event raised when pipeline execution completes
        /// </summary>
        public event EventHandler<PipelineExecutionEventArgs> ExecutionCompleted;

        /// <summary>
        /// Event raised when a node processes data
        /// </summary>
        public event EventHandler<NodeProcessingEventArgs> NodeProcessing;

        /// <summary>
        /// Create a new pipeline
        /// </summary>
        public Pipeline(string id, string name)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Add a node to the pipeline
        /// </summary>
        public void AddNode(INode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            if (_nodeById.ContainsKey(node.Id))
                throw new PipelineException(Id, $"Node with ID '{node.Id}' already exists in the pipeline");

            _nodes.Add(node);
            _nodeById[node.Id] = node;

            // Update entry/exit points
            if (node is IAudioInputNode inputNode && !_entryPoints.Contains(inputNode))
            {
                _entryPoints.Add(inputNode);
            }

            if (node is IAudioOutputNode outputNode && !_exitPoints.Contains(outputNode))
            {
                _exitPoints.Add(outputNode);
            }
        }

        /// <summary>
        /// Remove a node from the pipeline
        /// </summary>
        public void RemoveNode(string nodeId)
        {
            if (!_nodeById.TryGetValue(nodeId, out var node))
                return;

            // Remove all connections involving this node
            var connectionsToRemove = _connections
                .Where(c => c.Source.Id == nodeId || c.Target.Id == nodeId)
                .ToList();

            foreach (var connection in connectionsToRemove)
            {
                RemoveConnection(connection.Id);
            }

            _nodes.Remove(node);
            _nodeById.Remove(nodeId);

            // Update entry/exit points
            if (node is IAudioInputNode inputNode)
            {
                _entryPoints.Remove(inputNode);
            }

            if (node is IAudioOutputNode outputNode)
            {
                _exitPoints.Remove(outputNode);
            }
        }

        /// <summary>
        /// Create a connection between two nodes
        /// </summary>
        public INodeConnection Connect(string sourceId, string targetId, string connectionId = null)
        {
            if (!_nodeById.TryGetValue(sourceId, out var source))
                throw new PipelineException(Id, $"Source node with ID '{sourceId}' not found");

            if (!_nodeById.TryGetValue(targetId, out var target))
                throw new PipelineException(Id, $"Target node with ID '{targetId}' not found");

            return Connect(source, target, connectionId);
        }

        /// <summary>
        /// Create a connection between two nodes
        /// </summary>
        public INodeConnection Connect(INode source, INode target, string connectionId = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            // Generate connection ID if not provided
            connectionId ??= $"conn_{source.Id}_{target.Id}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            // Check if connection already exists
            if (_connectionById.ContainsKey(connectionId))
                throw new PipelineException(Id, $"Connection with ID '{connectionId}' already exists");

            // Create the connection
            var connection = new NodeConnection(connectionId, source, target);

            // Add to source and target node's connection lists
            source.OutputConnections.Add(connection);
            target.InputConnections.Add(connection);

            // Add to collections
            _connections.Add(connection);
            _connectionById[connectionId] = connection;

            // Subscribe to events
            connection.DataTransferred += Connection_DataTransferred;

            return connection;
        }

        /// <summary>
        /// Remove a connection from the pipeline
        /// </summary>
        public void RemoveConnection(string connectionId)
        {
            if (!_connectionById.TryGetValue(connectionId, out var connection))
                return;
            NodeConnection nodeConnection = (NodeConnection)connection;
            // Unsubscribe from events
            nodeConnection.DataTransferred -= Connection_DataTransferred;

            // Remove from source and target node's connection lists
            nodeConnection.Source.OutputConnections.Remove(connection);
            nodeConnection.Target.InputConnections.Remove(connection);

            // Remove from collections
            _connections.Remove(connection);
            _connectionById.Remove(connectionId);
        }

        /// <summary>
        /// Initialize all nodes in the pipeline
        /// </summary>
        public async Task InitializeAsync()
        {
            foreach (var node in _nodes)
            {
                await node.Initialize();
            }

            // Validate all connections
            foreach (var connection in _connections)
            {
                await connection.ValidateAsync();
            }
        }

        /// <summary>
        /// Shutdown all nodes in the pipeline
        /// </summary>
        public async Task ShutdownAsync()
        {
            IsRunning = false;

            foreach (var node in _nodes)
            {
                await node.Shutdown();
            }
        }

        /// <summary>
        /// Reset the pipeline state
        /// </summary>
        public async Task ResetAsync()
        {
            await _executionLock.WaitAsync();
            try
            {
                _executionLog.Clear();

                foreach (var node in _nodes)
                {
                    await node.Reset();
                }
            }
            finally
            {
                _executionLock.Release();
            }
        }

        /// <summary>
        /// Execute the pipeline with the given input
        /// </summary>
        public async Task<IList<IAudioData>> ExecuteAsync(IAudioData input, ProcessingContext context = null)
        {
            context ??= new ProcessingContext();

            await _executionLock.WaitAsync();
            try
            {
                IsRunning = true;

                var executionId = Guid.NewGuid();
                var startTime = DateTimeOffset.Now;

                // Log execution start
                var startArgs = new PipelineExecutionEventArgs(executionId, context, startTime);
                OnExecutionStarted(startArgs);

                // Find entry points
                var entryPoints = _entryPoints
                    .Where(n => n.IsEnabled)
                    .OrderBy(n => n.InputConnections.Count)
                    .ToList();

                if (entryPoints.Count == 0)
                    throw new PipelineException(Id, "No entry points found in the pipeline");

                // Process through entry points
                foreach (var entryPoint in entryPoints)
                {
                    await entryPoint.AcceptAudioAsync(input, context);
                }

                // Collect results from all exit points
                var results = new List<IAudioData>();
                foreach (var exitPoint in _exitPoints.Where(n => n.IsEnabled))
                {
                    var output = await exitPoint.GetAudioOutputAsync(context);
                    if (output != null)
                    {
                        results.Add(output);
                    }
                }

                // Log execution completion
                var endTime = DateTimeOffset.Now;
                var completedArgs = new PipelineExecutionEventArgs(executionId, context, startTime)
                {
                    EndTime = endTime,
                    Results = results
                };

                OnExecutionCompleted(completedArgs);

                IsRunning = false;
                return results;
            }
            catch (Exception ex)
            {
                IsRunning = false;
                throw new PipelineException(Id, $"Pipeline execution failed: {ex.Message}", ex);
            }
            finally
            {
                _executionLock.Release();
            }
        }

        /// <summary>
        /// Execute the pipeline with multiple inputs
        /// </summary>
        public async Task<IList<IAudioData>> ExecuteMultipleAsync(IEnumerable<IAudioData> inputs, ProcessingContext context = null)
        {
            context ??= new ProcessingContext();
            var results = new List<IAudioData>();

            foreach (var input in inputs)
            {
                var outputList = await ExecuteAsync(input, context);
                results.AddRange(outputList);
            }

            return results;
        }

        /// <summary>
        /// Get a node by ID
        /// </summary>
        public INode GetNode(string nodeId)
        {
            if (_nodeById.TryGetValue(nodeId, out var node))
                return node;

            return null;
        }

        /// <summary>
        /// Get a connection by ID
        /// </summary>
        public INodeConnection GetConnection(string connectionId)
        {
            if (_connectionById.TryGetValue(connectionId, out var connection))
                return connection;

            return null;
        }

        /// <summary>
        /// Get all nodes in the pipeline
        /// </summary>
        public IReadOnlyList<INode> GetAllNodes() => _nodes.AsReadOnly();

        /// <summary>
        /// Get all connections in the pipeline
        /// </summary>
        public IReadOnlyList<INodeConnection> GetAllConnections() => _connections.AsReadOnly();

        /// <summary>
        /// Get the entry points of the pipeline
        /// </summary>
        public IReadOnlyList<IAudioInputNode> GetEntryPoints() => _entryPoints.AsReadOnly();

        /// <summary>
        /// Get the exit points of the pipeline
        /// </summary>
        public IReadOnlyList<IAudioOutputNode> GetExitPoints() => _exitPoints.AsReadOnly();

        /// <summary>
        /// Get the execution log
        /// </summary>
        public IReadOnlyList<DataTransferEventArgs> GetExecutionLog() => _executionLog.AsReadOnly();

        /// <summary>
        /// Event handler for data transfer through connections
        /// </summary>
        private void Connection_DataTransferred(object sender, DataTransferEventArgs e)
        {
            _executionLog.Add(e);
        }

        /// <summary>
        /// Raise execution started event
        /// </summary>
        protected virtual void OnExecutionStarted(PipelineExecutionEventArgs e)
        {
            ExecutionStarted?.Invoke(this, e);
        }

        /// <summary>
        /// Raise execution completed event
        /// </summary>
        protected virtual void OnExecutionCompleted(PipelineExecutionEventArgs e)
        {
            ExecutionCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// Raise node processing event
        /// </summary>
        protected virtual void OnNodeProcessing(NodeProcessingEventArgs e)
        {
            NodeProcessing?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for pipeline execution events
    /// </summary>
    public class PipelineExecutionEventArgs : EventArgs
    {
        /// <summary>
        /// Unique execution ID
        /// </summary>
        public Guid ExecutionId { get; }

        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }

        /// <summary>
        /// Execution start time
        /// </summary>
        public DateTimeOffset StartTime { get; }

        /// <summary>
        /// Execution end time
        /// </summary>
        public DateTimeOffset? EndTime { get; set; }

        /// <summary>
        /// Execution results
        /// </summary>
        public IList<IAudioData> Results { get; set; }

        /// <summary>
        /// Execution duration
        /// </summary>
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

        /// <summary>
        /// Create pipeline execution event args
        /// </summary>
        public PipelineExecutionEventArgs(Guid executionId, ProcessingContext context, DateTimeOffset startTime)
        {
            ExecutionId = executionId;
            Context = context;
            StartTime = startTime;
        }
    }

    /// <summary>
    /// Event arguments for node processing events
    /// </summary>
    public class NodeProcessingEventArgs : EventArgs
    {
        /// <summary>
        /// Node that processed data
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
        /// Processing time
        /// </summary>
        public TimeSpan ProcessingTime { get; }

        /// <summary>
        /// Create node processing event args
        /// </summary>
        public NodeProcessingEventArgs(INode node, object input, object output, ProcessingContext context, TimeSpan processingTime)
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
