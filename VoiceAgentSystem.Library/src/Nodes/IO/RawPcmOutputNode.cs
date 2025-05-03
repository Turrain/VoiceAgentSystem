using System;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.IO
{
    /// <summary>
    /// Output node that produces raw PCM audio from the pipeline
    /// </summary>
    public class RawPcmOutputNode : AudioOutputNodeBase
    {
        /// <summary>
        /// Event raised when audio is received
        /// </summary>
        public event EventHandler<AudioReceivedEventArgs> AudioReceived;

        /// <summary>
        /// Create raw PCM output node
        /// </summary>
        public RawPcmOutputNode(string id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// Get audio output from this node
        /// </summary>
        public override Task<IAudioData> GetAudioOutputAsync(ProcessingContext context)
        {
            context?.LogInformation($"Output node {Name} providing audio: {LastAudioOutput?.DurationInSeconds:F2}s");
            return Task.FromResult(LastAudioOutput);
        }

        /// <summary>
        /// Accept audio directly to this output node
        /// </summary>
        public async Task<bool> AcceptAudioAsync(IAudioData audioData, ProcessingContext context)
        {
            if (!IsEnabled || audioData == null)
                return false;

            // Set as last audio output
            LastAudioOutput = audioData;

            // Update output format if needed
            if (OutputFormat == null || !OutputFormat.Equals(audioData.Format))
            {
                OutputFormat = audioData.Format;
            }

            // Raise event
            OnAudioReceived(new AudioReceivedEventArgs(audioData, context));

            context?.LogInformation($"Output node {Name} received audio: {audioData.DurationInSeconds:F2}s");

            return true;
        }

        /// <summary>
        /// Raise audio received event
        /// </summary>
        protected virtual void OnAudioReceived(AudioReceivedEventArgs e)
        {
            AudioReceived?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for audio received events
    /// </summary>
    public class AudioReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Audio data
        /// </summary>
        public IAudioData AudioData { get; }

        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }

        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Create audio received event args
        /// </summary>
        public AudioReceivedEventArgs(IAudioData audioData, ProcessingContext context)
        {
            AudioData = audioData;
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
}
