using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Nodes.Base
{
    /// <summary>
    /// Base implementation for audio processor nodes
    /// </summary>
    public abstract class AudioProcessorNodeBase : NodeBase, IAudioProcessorNode
    {
        /// <summary>
        /// Supported audio formats
        /// </summary>
        public IList<AudioFormat> SupportedFormats { get; } = new List<AudioFormat>();

        /// <summary>
        /// Output audio format
        /// </summary>
        public AudioFormat OutputFormat { get; protected set; }

        /// <summary>
        /// Last input audio data
        /// </summary>
        protected IAudioData LastInputAudio;

        /// <summary>
        /// Last output audio data
        /// </summary>
        protected IAudioData LastOutputAudio;

        /// <summary>
        /// Create audio processor node
        /// </summary>
        protected AudioProcessorNodeBase(string id, string name) : base(id, name)
        {
            // Set default format
            OutputFormat = AudioFormat.Default;
            SupportedFormats.Add(AudioFormat.Default);
        }

        /// <summary>
        /// Accept audio data for processing
        /// </summary>
        public virtual async Task<bool> AcceptAudioAsync(IAudioData audioData, ProcessingContext context)
        {
            if (!IsEnabled || audioData == null)
                return false;

            // Check for cancellation
            if (context.CancellationToken.IsCancellationRequested)
                return false;

            // Check if format is supported
            if (!IsFormatSupported(audioData.Format))
            {
                context.LogWarning($"Node {Name} does not support format: {audioData.Format}");
                return false;
            }

            // Store input
            LastInputAudio = audioData;

            // Process audio
            OnProcessingStarted(audioData, context);

            var stopwatch = Stopwatch.StartNew();
            var processedAudio = await ProcessAudioAsync(audioData, context);
            stopwatch.Stop();

            // Update metrics
            TrackProcessing(stopwatch.Elapsed);

            if (processedAudio != null)
            {
                // Store output
                LastOutputAudio = processedAudio;

                // Raise event
                OnProcessingCompleted(audioData, processedAudio, context, stopwatch.Elapsed);

                // Propagate to outputs
                await PropagateToOutputs(processedAudio, context);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get audio output from this node
        /// </summary>
        public virtual Task<IAudioData> GetAudioOutputAsync(ProcessingContext context)
        {
            return Task.FromResult(LastOutputAudio);
        }

        /// <summary>
        /// Process audio data
        /// </summary>
        public abstract Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context);

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
