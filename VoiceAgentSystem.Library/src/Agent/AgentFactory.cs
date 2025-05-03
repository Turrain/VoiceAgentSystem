using System;
using System.Threading.Tasks;
using VoiceBotSystem.Modules;
using VoiceBotSystem.Nodes.IO;
using VoiceBotSystem.Nodes.Processors;
using VoiceBotSystem.Pipeline;

namespace VoiceBotSystem.Agent
{
    /// <summary>
    /// Factory for creating agent components
    /// </summary>
    public class AgentFactory
    {
        private readonly ModuleRegistry _moduleRegistry;
        private readonly TemplateRegistry _templateRegistry;

        /// <summary>
        /// Create a new agent factory
        /// </summary>
        public AgentFactory(ModuleRegistry moduleRegistry, TemplateRegistry templateRegistry = null)
        {
            _moduleRegistry = moduleRegistry ?? throw new ArgumentNullException(nameof(moduleRegistry));
            _templateRegistry = templateRegistry ?? new TemplateRegistry();
        }

        /// <summary>
        /// Create a new agent
        /// </summary>
        public VoiceBotAgent CreateAgent(string id, string name, string description = null)
        {
            var agent = new VoiceBotAgent(id, name, _moduleRegistry, _templateRegistry);
            agent.Description = description;
            return agent;
        }

        /// <summary>
        /// Create a basic agent with a simple pipeline
        /// </summary>
        public async Task<VoiceBotAgent> CreateBasicAgentAsync(string id, string name, string description = null)
        {
            var agent = CreateAgent(id, name, description);

            // Add a basic pipeline
            await agent.AddPipelineAsync("default", "Default Pipeline", async builder =>
            {
                // Add basic input/output nodes
                builder
                    .AddProcessorNode<RawPcmInputNode>("input", "Input Node")
                    .AddProcessorNode<RawPcmOutputNode>("output", "Output Node");

                // Add a simple passthrough node
                builder.AddProcessorNode<AudioPassthroughNode>("passthrough", "Passthrough Node");

                // Connect nodes
                await builder.ConnectAsync("input", "passthrough");
                await builder.ConnectAsync("passthrough", "output");

                return builder;
            });

            // Initialize the agent
            await agent.InitializeAsync();

            return agent;
        }

        /// <summary>
        /// Create an agent with a more advanced pipeline
        /// </summary>
        public async Task<VoiceBotAgent> CreateAdvancedAgentAsync(string id, string name, string description = null)
        {
            var agent = CreateAgent(id, name, description);

            // Add an advanced pipeline
            await agent.AddPipelineAsync("advanced", "Advanced Pipeline", async builder =>
            {
                // Add basic input/output nodes
                builder
                    .AddProcessorNode<RawPcmInputNode>("input", "Input Node")
                    .AddProcessorNode<RawPcmOutputNode>("output", "Output Node");

                // Add processing nodes
                builder
                    .AddSplitterNode("splitter", "Audio Splitter", new[]
                    {
                        ("voice", "Voice Channel"),
                        ("music", "Music Channel")
                    })
                    .AddVolumeControlNode("voice_volume", "Voice Volume", 1.5)
                    .AddVolumeControlNode("music_volume", "Music Volume", 0.8)
                    .AddMixerNode("mixer", "Audio Mixer");

                // Connect nodes
                await builder.ConnectAsync("input", "splitter");
                await builder.ConnectSplitterChannelAsync("splitter", "voice", "voice_volume");
                await builder.ConnectSplitterChannelAsync("splitter", "music", "music_volume");
                await builder.ConnectAsync("voice_volume", "mixer");
                await builder.ConnectAsync("music_volume", "mixer");
                await builder.ConnectAsync("mixer", "output");

                return builder;
            });

            // Initialize the agent
            await agent.InitializeAsync();

            return agent;
        }
    }
}
