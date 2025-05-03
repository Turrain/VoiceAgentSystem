using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Processors;

namespace VoiceBotSystem.Pipeline
{
    /// <summary>
    /// Interface for a pipeline template
    /// </summary>
    public interface IPipelineTemplate
    {
        /// <summary>
        /// Template ID
        /// </summary>
        string TemplateId { get; }

        /// <summary>
        /// Template name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Template description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Apply the template to a pipeline builder
        /// </summary>
        Task ApplyToBuilder(PipelineBuilder builder, string idPrefix = null);
    }

    /// <summary>
    /// Base implementation of a pipeline template
    /// </summary>
    public abstract class PipelineTemplate : IPipelineTemplate
    {
        /// <summary>
        /// Template ID
        /// </summary>
        public string TemplateId { get; }

        /// <summary>
        /// Template name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Template description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Create a pipeline template
        /// </summary>
        protected PipelineTemplate(string templateId, string name, string description)
        {
            TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description;
        }

        /// <summary>
        /// Apply the template to a pipeline builder
        /// </summary>
        public abstract Task ApplyToBuilder(PipelineBuilder builder, string idPrefix = null);

        /// <summary>
        /// Generate a node ID with optional prefix
        /// </summary>
        protected string GenerateNodeId(string baseId, string idPrefix)
        {
            if (string.IsNullOrEmpty(idPrefix))
                return baseId;

            return $"{idPrefix}_{baseId}";
        }
    }

    /// <summary>
    /// Registry for pipeline templates
    /// </summary>
    public class TemplateRegistry
    {
        private readonly Dictionary<string, IPipelineTemplate> _templates = new Dictionary<string, IPipelineTemplate>();

        /// <summary>
        /// Create a template registry with default templates
        /// </summary>
        public TemplateRegistry()
        {
            // Register built-in templates
            RegisterTemplate(new BasicVoiceProcessingTemplate());
            RegisterTemplate(new AudioSplittingTemplate());
        }

        /// <summary>
        /// Register a template
        /// </summary>
        public void RegisterTemplate(IPipelineTemplate template)
        {
            _templates[template.TemplateId] = template;
        }

        /// <summary>
        /// Get a template by ID
        /// </summary>
        public IPipelineTemplate GetTemplate(string templateId)
        {
            if (_templates.TryGetValue(templateId, out var template))
                return template;

            return null;
        }

        /// <summary>
        /// Get all registered templates
        /// </summary>
        public IReadOnlyCollection<IPipelineTemplate> GetAllTemplates() => _templates.Values;
    }

    /// <summary>
    /// Basic voice processing pipeline template
    /// </summary>
    public class BasicVoiceProcessingTemplate : PipelineTemplate
    {
        /// <summary>
        /// Create basic voice processing template
        /// </summary>
        public BasicVoiceProcessingTemplate()
            : base("basic_voice_processing", "Basic Voice Processing",
                  "A simple voice processing pipeline with volume adjustment and passthrough")
        {
        }

        /// <summary>
        /// Apply the template to a pipeline builder
        /// </summary>
        public override async Task ApplyToBuilder(PipelineBuilder builder, string idPrefix = null)
        {
            // Create nodes
            builder
                .AddProcessorNode<AudioPassthroughNode>(
                    GenerateNodeId("passthrough", idPrefix),
                    "Audio Passthrough")
                .AddVolumeControlNode(
                    GenerateNodeId("volume", idPrefix),
                    "Volume Control",
                    1.2);

            // Create connections
            await builder.CreateLinearPipelineAsync(
                GenerateNodeId("passthrough", idPrefix),
                GenerateNodeId("volume", idPrefix));
        }
    }

    /// <summary>
    /// Audio splitting template
    /// </summary>
    public class AudioSplittingTemplate : PipelineTemplate
    {
        /// <summary>
        /// Create audio splitting template
        /// </summary>
        public AudioSplittingTemplate()
            : base("audio_splitting", "Audio Splitting",
                  "A pipeline that splits audio into multiple processing paths")
        {
        }

        /// <summary>
        /// Apply the template to a pipeline builder
        /// </summary>
        public override async Task ApplyToBuilder(PipelineBuilder builder, string idPrefix = null)
        {
            // Create main splitter
            builder.AddSplitterNode(
                GenerateNodeId("splitter", idPrefix),
                "Audio Splitter",
                new[]
                {
                    ("voice", "Voice Channel"),
                    ("music", "Music Channel")
                });

            // Create voice path
            builder
                .AddVolumeControlNode(
                    GenerateNodeId("voice_volume", idPrefix),
                    "Voice Volume",
                    1.5);

            // Create music path
            builder
                .AddVolumeControlNode(
                    GenerateNodeId("music_volume", idPrefix),
                    "Music Volume",
                    0.8);

            // Create mixer for recombining
            builder
                .AddMixerNode(
                    GenerateNodeId("mixer", idPrefix),
                    "Audio Mixer");

            // Connect everything
            await builder
                .ConnectAsync(
                    GenerateNodeId("voice_volume", idPrefix),
                    GenerateNodeId("mixer", idPrefix));
            await builder
                .ConnectAsync(
                    GenerateNodeId("music_volume", idPrefix),
                    GenerateNodeId("mixer", idPrefix));
        }
    }
}
