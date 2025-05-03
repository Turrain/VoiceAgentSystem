using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.Processors
{
    /// <summary>
    /// Audio splitter node that can split audio to multiple outputs with different processing
    /// </summary>
    public class AudioSplitterNode : AudioProcessorNodeBase
    {
        /// <summary>
        /// Audio channels
        /// </summary>
        private readonly List<AudioChannel> _channels = new List<AudioChannel>();

        /// <summary>
        /// Create audio splitter node
        /// </summary>
        public AudioSplitterNode(string id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// Process audio data
        /// </summary>
        public override async Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context)
        {
            // Store original input
            context.SetTransientData($"{Id}_OriginalInput", input);

            // Update output format
            OutputFormat = input.Format;

            context.LogInformation($"Splitter node {Name} processing audio: {input.DurationInSeconds:F2}s");

            // Check if there are any enabled channels
            if (_channels.Count(c => c.IsEnabled) == 0)
            {
                context.LogInformation($"Splitter node {Name} has no enabled channels, passing through");
                return input;
            }

            // Process each channel
            int processedChannels = 0;
            foreach (var channel in _channels.Where(c => c.IsEnabled))
            {
                // Clone the input for each channel
                var channelInput = input.Clone();

                // Set channel context
                context.SetTransientData("SplitterChannelId", channel.Id);

                // Apply channel processor if available
                if (channel.Processor != null)
                {
                    var processedAudio = await channel.Processor.Invoke(channelInput, context);

                    if (processedAudio != null)
                    {
                        // Store in context
                        context.SetTransientData($"{Id}_Channel_{channel.Id}_Output", processedAudio);

                        // Propagate to specific connections
                        await PropagateToChannel(processedAudio, channel.Id, context);
                        processedChannels++;
                    }
                }
                else
                {
                    // Just use the cloned input
                    context.SetTransientData($"{Id}_Channel_{channel.Id}_Output", channelInput);

                    // Propagate to specific connections
                    await PropagateToChannel(channelInput, channel.Id, context);
                    processedChannels++;
                }
            }

            context.LogInformation($"Splitter node {Name} processed {processedChannels} channels");

            // Return original input for default connections
            return input;
        }

        /// <summary>
        /// Propagate audio to a specific channel
        /// </summary>
        private async Task<bool> PropagateToChannel(IAudioData audioData, string channelId, ProcessingContext context)
        {
            bool anySuccess = false;

            // Propagate to specific connections for this channel
            foreach (var connection in OutputConnections.Where(c =>
                c.IsEnabled &&
                c.GetConfigurationValue<string>("ChannelId", null) == channelId))
            {
                bool success = await connection.TransferDataAsync(audioData, context);
                if (success)
                {
                    anySuccess = true;
                }
            }

            return anySuccess;
        }

        /// <summary>
        /// Add a channel to the splitter
        /// </summary>
        public void AddChannel(string channelId, string name, Func<IAudioData, ProcessingContext, Task<IAudioData>> processor = null)
        {
            if (string.IsNullOrEmpty(channelId))
                throw new ArgumentNullException(nameof(channelId));

            if (_channels.Any(c => c.Id == channelId))
                throw new ArgumentException($"Channel with ID '{channelId}' already exists", nameof(channelId));

            var channel = new AudioChannel
            {
                Id = channelId,
                Name = name ?? channelId,
                Processor = processor,
                IsEnabled = true
            };

            _channels.Add(channel);
        }

        /// <summary>
        /// Remove a channel from the splitter
        /// </summary>
        public void RemoveChannel(string channelId)
        {
            var channel = _channels.FirstOrDefault(c => c.Id == channelId);
            if (channel != null)
            {
                _channels.Remove(channel);
            }
        }

        /// <summary>
        /// Enable or disable a channel
        /// </summary>
        public void SetChannelEnabled(string channelId, bool enabled)
        {
            var channel = _channels.FirstOrDefault(c => c.Id == channelId);
            if (channel != null)
            {
                channel.IsEnabled = enabled;
            }
        }

        /// <summary>
        /// Get all channels
        /// </summary>
        public IReadOnlyList<AudioChannel> GetChannels() => _channels.AsReadOnly();

        /// <summary>
        /// Reset the node
        /// </summary>
        public override async Task Reset()
        {
            await base.Reset();

            // Just reset channel states, don't remove the channels
            foreach (var channel in _channels)
            {
                channel.IsEnabled = true;
            }
        }
    }

    /// <summary>
    /// Represents a channel in the splitter
    /// </summary>
    public class AudioChannel
    {
        /// <summary>
        /// Channel ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Channel name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether the channel is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Channel processor
        /// </summary>
        public Func<IAudioData, ProcessingContext, Task<IAudioData>> Processor { get; set; }
    }
}
