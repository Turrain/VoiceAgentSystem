using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.IO
{
    /// <summary>
    /// Input node that feeds raw PCM audio into the pipeline
    /// </summary>
    public class RawPcmInputNode : AudioInputNodeBase
    {
        /// <summary>
        /// Input queue for audio data
        /// </summary>
        private Queue<IAudioData> _inputQueue = new Queue<IAudioData>();

        /// <summary>
        /// Create raw PCM input node
        /// </summary>
        public RawPcmInputNode(string id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// Accept audio data for processing
        /// </summary>
        public override async Task<bool> AcceptAudioAsync(IAudioData audioData, ProcessingContext context)
        {
            if (!IsEnabled || audioData == null)
                return false;

            // Check format compatibility
            if (!IsFormatSupported(audioData.Format))
            {
                context.LogWarning($"Node {Name} does not support format: {audioData.Format}");
                return false;
            }

            // Store in queue for later processing
            _inputQueue.Enqueue(audioData);
            context.LogInformation($"Input node {Name} received audio: {audioData.DurationInSeconds:F2}s");

            // Propagate to connected nodes
            return await PropagateToOutputs(audioData, context);
        }

        /// <summary>
        /// Push raw PCM data into the pipeline
        /// </summary>
        public async Task<bool> PushAudioAsync(byte[] rawPcmData, AudioFormat format, ProcessingContext context = null)
        {
            if (!IsEnabled || rawPcmData == null)
                return false;

            context ??= new ProcessingContext();
            var audioData = new PCMAudioData(rawPcmData, format);

            return await AcceptAudioAsync(audioData, context);
        }

        /// <summary>
        /// Get the next queued audio data
        /// </summary>
        public IAudioData GetNextQueuedAudio()
        {
            if (_inputQueue.Count > 0)
                return _inputQueue.Dequeue();

            return null;
        }

        /// <summary>
        /// Check if there is queued audio data
        /// </summary>
        public bool HasQueuedAudio()
        {
            return _inputQueue.Count > 0;
        }

        /// <summary>
        /// Clear the input queue
        /// </summary>
        public void ClearQueue()
        {
            _inputQueue.Clear();
        }

        /// <summary>
        /// Reset the node
        /// </summary>
        public override async Task Reset()
        {
            await base.Reset();

            _inputQueue.Clear();
        }
    }
}
