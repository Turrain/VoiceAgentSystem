using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Modules;
using VoiceBotSystem.Nodes.Base;
using VoiceBotSystem.Nodes.IO;
using VoiceBotSystem.Nodes.Processors;

namespace VoiceBotSystem.Pipeline
{
    /// <summary>
    /// Builder for constructing pipelines
    /// </summary>
    public class PipelineBuilder
    {
        private readonly Pipeline _pipeline;
        private readonly ModuleRegistry _moduleRegistry;
        private readonly Dictionary<string, INode> _nodeCache = new Dictionary<string, INode>();

        /// <summary>
        /// Create a new pipeline builder
        /// </summary>
        public PipelineBuilder(Pipeline pipeline, ModuleRegistry moduleRegistry)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _moduleRegistry = moduleRegistry ?? throw new ArgumentNullException(nameof(moduleRegistry));

            // Cache existing nodes
            foreach (var node in pipeline.GetAllNodes())
            {
                _nodeCache[node.Id] = node;
            }
        }

        /// <summary>
        /// Add a processor node of the specified type
        /// </summary>
        public PipelineBuilder AddProcessorNode<T>(string id, string name, Action<T> configure = null) where T : INode
        {
            if (_nodeCache.ContainsKey(id))
                throw new ArgumentException($"Node with ID '{id}' already exists");

            var nodeType = typeof(T);
            var node = (T)Activator.CreateInstance(nodeType, id, name);
            configure?.Invoke(node);

            _pipeline.AddNode(node);
            _nodeCache[id] = node;

            return this;
        }

        /// <summary>
        /// Add a node from a registered module
        /// </summary>
        public PipelineBuilder AddModuleNode(string typeName, string id, string name, Action<INode> configure = null)
        {
            if (_nodeCache.ContainsKey(id))
                throw new ArgumentException($"Node with ID '{id}' already exists");

            var node = _moduleRegistry.CreateNodeInstance(typeName, id, name);
            configure?.Invoke(node);

            _pipeline.AddNode(node);
            _nodeCache[id] = node;

            return this;
        }

        /// <summary>
        /// Add an input node to the pipeline
        /// </summary>
        public PipelineBuilder AddInputNode(string id, string name, Action<RawPcmInputNode> configure = null)
        {
            return AddProcessorNode<RawPcmInputNode>(id, name, inputNode =>
            {
                configure?.Invoke(inputNode);
            });
        }

        /// <summary>
        /// Add an output node to the pipeline
        /// </summary>
        public PipelineBuilder AddOutputNode(string id, string name, Action<RawPcmOutputNode> configure = null)
        {
            return AddProcessorNode<RawPcmOutputNode>(id, name, outputNode =>
            {
                configure?.Invoke(outputNode);
            });
        }

        /// <summary>
        /// Add an audio mixer node
        /// </summary>
        public PipelineBuilder AddMixerNode(string id, string name, Action<AudioMixerNode> configure = null)
        {
            return AddProcessorNode<AudioMixerNode>(id, name, mixer =>
            {
                mixer.NormalizeOutput = true;
                configure?.Invoke(mixer);
            });
        }

        /// <summary>
        /// Add an audio splitter node
        /// </summary>
        public PipelineBuilder AddSplitterNode(
            string id,
            string name,
            IEnumerable<(string channelId, string channelName)> channels = null,
            Action<AudioSplitterNode> configure = null)
        {
            return AddProcessorNode<AudioSplitterNode>(id, name, splitter =>
            {
                if (channels != null)
                {
                    foreach (var (channelId, channelName) in channels)
                    {
                        splitter.AddChannel(channelId, channelName);
                    }
                }

                configure?.Invoke(splitter);
            });
        }

        /// <summary>
        /// Add a volume control node
        /// </summary>
        public PipelineBuilder AddVolumeControlNode(string id, string name, double gain = 1.0)
        {
            return AddProcessorNode<VolumeControlNode>(id, name, volume =>
            {
                volume.Gain = gain;
            });
        }

        /// <summary>
        /// Connect two nodes with additional configuration
        /// </summary>
        public async Task<PipelineBuilder> ConnectAsync(
            string sourceId,
            string targetId,
            string connectionId = null,
            Action<INodeConnection> configure = null)
        {
            if (!_nodeCache.TryGetValue(sourceId, out var source))
                throw new ArgumentException($"Source node with ID '{sourceId}' not found");

            if (!_nodeCache.TryGetValue(targetId, out var target))
                throw new ArgumentException($"Target node with ID '{targetId}' not found");

            var connection = _pipeline.Connect(source, target, connectionId);
            configure?.Invoke(connection);

            await connection.ValidateAsync();

            return this;
        }

        /// <summary>
        /// Connect a splitter channel to a target node
        /// </summary>
        public async Task<PipelineBuilder> ConnectSplitterChannelAsync(
            string splitterId,
            string channelId,
            string targetId,
            string connectionId = null)
        {
            if (!_nodeCache.TryGetValue(splitterId, out var splitterNode) ||
                !(splitterNode is AudioSplitterNode))
                throw new ArgumentException($"Splitter node with ID '{splitterId}' not found or not a splitter");

            if (!_nodeCache.TryGetValue(targetId, out var target))
                throw new ArgumentException($"Target node with ID '{targetId}' not found");

            var connection = _pipeline.Connect(splitterNode, target, connectionId);
            connection.Configuration["ChannelId"] = channelId;

            await connection.ValidateAsync();

            return this;
        }

        /// <summary>
        /// Create a linear pipeline with multiple nodes
        /// </summary>
        public async Task<PipelineBuilder> CreateLinearPipelineAsync(params string[] nodeIds)
        {
            if (nodeIds.Length < 2)
                throw new ArgumentException("At least two nodes required for a linear pipeline");

            for (int i = 0; i < nodeIds.Length - 1; i++)
            {
                await ConnectAsync(nodeIds[i], nodeIds[i + 1]);
            }

            return this;
        }

        /// <summary>
        /// Create a branch in the pipeline
        /// </summary>
        public async Task<PipelineBuilder> CreateBranchAsync(
            string sourceId,
            IEnumerable<string> targetIds,
            Action<INodeConnection> configureConnection = null)
        {
            foreach (var targetId in targetIds)
            {
                await ConnectAsync(sourceId, targetId, configure: configureConnection);
            }

            return this;
        }

        /// <summary>
        /// Create a merge point in the pipeline
        /// </summary>
        public async Task<PipelineBuilder> CreateMergeAsync(
            IEnumerable<string> sourceIds,
            string targetId,
            Action<INodeConnection> configureConnection = null)
        {
            foreach (var sourceId in sourceIds)
            {
                await ConnectAsync(sourceId, targetId, configure: configureConnection);
            }

            return this;
        }

        /// <summary>
        /// Load a pipeline from a JSON file
        /// </summary>
        public async Task<PipelineBuilder> LoadFromJsonAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Pipeline JSON file not found: {filePath}");

            var json = await File.ReadAllTextAsync(filePath);
            var pipelineData = JsonSerializer.Deserialize<PipelineData>(json);

            // Create nodes
            foreach (var nodeData in pipelineData.Nodes)
            {
                if (nodeData.ModuleType != null)
                {
                    AddModuleNode(nodeData.ModuleType, nodeData.Id, nodeData.Name, node =>
                    {
                        foreach (var config in nodeData.Configuration)
                        {
                            node.Configuration[config.Key] = config.Value;
                        }
                    });
                }
                else
                {
                    // Try to create with reflection
                    Type nodeType = Type.GetType(nodeData.Type);
                    if (nodeType == null)
                        throw new InvalidOperationException($"Node type not found: {nodeData.Type}");

                    var node = (INode)Activator.CreateInstance(nodeType, nodeData.Id, nodeData.Name);
                    foreach (var config in nodeData.Configuration)
                    {
                        node.Configuration[config.Key] = config.Value;
                    }
                    _pipeline.AddNode(node);
                    _nodeCache[nodeData.Id] = node;
                }
            }

            // Create connections
            foreach (var connectionData in pipelineData.Connections)
            {
                await ConnectAsync(
                    connectionData.SourceId,
                    connectionData.TargetId,
                    connectionData.Id,
                    connection =>
                    {
                        connection.Label = connectionData.Label;
                        connection.IsEnabled = connectionData.IsEnabled;
                        connection.Priority = connectionData.Priority;

                        foreach (var config in connectionData.Configuration)
                        {
                            connection.Configuration[config.Key] = config.Value;
                        }
                    });
            }

            return this;
        }

        /// <summary>
        /// Save the pipeline to a JSON file
        /// </summary>
        public async Task SaveToJsonAsync(string filePath)
        {
            var pipelineData = new PipelineData
            {
                Id = _pipeline.Id,
                Name = _pipeline.Name,
                Nodes = _pipeline.GetAllNodes().Select(n => new NodeData
                {
                    Id = n.Id,
                    Name = n.Name,
                    Type = n.GetType().AssemblyQualifiedName,
                    IsEnabled = n.IsEnabled,
                    Configuration = n.Configuration.ToDictionary(k => k.Key, k => k.Value)
                }).ToList(),
                Connections = _pipeline.GetAllConnections().Select(c => new ConnectionData
                {
                    Id = c.Id,
                    SourceId = c.Source.Id,
                    TargetId = c.Target.Id,
                    Label = c.Label,
                    IsEnabled = c.IsEnabled,
                    Priority = c.Priority,
                    Configuration = c.Configuration.ToDictionary(k => k.Key, k => k.Value)
                }).ToList()
            };

            var json = JsonSerializer.Serialize(pipelineData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// Build and initialize the pipeline
        /// </summary>
        public async Task<Pipeline> BuildAsync()
        {
            await _pipeline.InitializeAsync();
            return _pipeline;
        }
    }

        /// <summary>
        /// Pipeline data for serialization
        /// </summary>
        public class PipelineData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public List<NodeData> Nodes { get; set; } = new List<NodeData>();
            public List<ConnectionData> Connections { get; set; } = new List<ConnectionData>();
        }

        /// <summary>
        /// Node data for serialization
        /// </summary>
        public class NodeData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string ModuleType { get; set; }
            public bool IsEnabled { get; set; } = true;
            public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
        }

        /// <summary>
        /// Connection data for serialization
        /// </summary>
        public class ConnectionData
        {
            public string Id { get; set; }
            public string SourceId { get; set; }
            public string TargetId { get; set; }
            public string Label { get; set; }
            public bool IsEnabled { get; set; } = true;
            public int Priority { get; set; } = 0;
            public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
        }
}