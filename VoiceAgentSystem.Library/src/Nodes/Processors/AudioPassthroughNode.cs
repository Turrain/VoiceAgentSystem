using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.Processors
{
    /// <summary>
    /// Simple passthrough node that doesn't modify audio
    /// </summary>
    public class AudioPassthroughNode : AudioProcessorNodeBase
    {
        /// <summary>
        /// Create audio passthrough node
        /// </summary>
        public AudioPassthroughNode(string id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// Process audio data (just pass through)
        /// </summary>
        public override Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context)
        {
            // Simply pass through the audio without modification
            context?.LogInformation($"Passthrough node {Name} passing audio: {input.DurationInSeconds:F2}s");

            // Update output format if needed
            if (!input.Format.Equals(OutputFormat))
            {
                OutputFormat = input.Format;
            }

            return Task.FromResult(input);
        }
    }
}
