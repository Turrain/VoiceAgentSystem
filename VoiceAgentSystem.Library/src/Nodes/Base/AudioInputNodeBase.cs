using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Nodes.Base
{
    /// <summary>
    /// Base implementation for audio input nodes
    /// </summary>
    public abstract class AudioInputNodeBase : NodeBase, IAudioInputNode
    {
        /// <summary>
        /// Supported audio formats
        /// </summary>
        public IList<AudioFormat> SupportedFormats { get; } = new List<AudioFormat>();

        /// <summary>
        /// Create audio input node
        /// </summary>
        protected AudioInputNodeBase(string id, string name) : base(id, name)
        {
            // Add default format
            SupportedFormats.Add(AudioFormat.Default);
        }

        /// <summary>
        /// Accept audio data for processing
        /// </summary>
        public abstract Task<bool> AcceptAudioAsync(IAudioData audioData, ProcessingContext context);

        /// <summary>
        /// Propagate audio to output connections
        /// </summary>
        protected async Task<bool> PropagateToOutputs(IAudioData audioData, ProcessingContext context)
        {
            if (!IsEnabled || audioData == null)
                return false;

            bool anySuccess = false;

            // Check for cancellation
            if (context.CancellationToken.IsCancellationRequested)
                return false;

            // Propagate to all output connections in order of priority
            foreach (var connection in OutputConnections
                .Where(c => c.IsEnabled)
                .OrderBy(c => c.Priority))
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
        /// Check if the format is supported
        /// </summary>
        protected bool IsFormatSupported(AudioFormat format)
        {
            // If no formats are specified, accept any format
            if (SupportedFormats.Count == 0)
                return true;

            return SupportedFormats.Any(f =>
                f.SampleRate == format.SampleRate &&
                f.Channels == format.Channels &&
                f.BitsPerSample == format.BitsPerSample);
        }
    }
}
