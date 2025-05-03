using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Nodes.Base
{
    /// <summary>
    /// Base implementation for audio output nodes
    /// </summary>
    public abstract class AudioOutputNodeBase : NodeBase, IAudioOutputNode
    {
        /// <summary>
        /// Output audio format
        /// </summary>
        public AudioFormat OutputFormat { get; protected set; }

        /// <summary>
        /// Last processed audio output
        /// </summary>
        protected IAudioData LastAudioOutput;

        /// <summary>
        /// Create audio output node
        /// </summary>
        protected AudioOutputNodeBase(string id, string name) : base(id, name)
        {
            OutputFormat = AudioFormat.Default;
        }

        /// <summary>
        /// Get audio output from this node
        /// </summary>
        public abstract Task<IAudioData> GetAudioOutputAsync(ProcessingContext context);
    }
}
