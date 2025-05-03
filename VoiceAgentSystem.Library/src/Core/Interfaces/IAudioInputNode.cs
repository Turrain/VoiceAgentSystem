using System.Collections.Generic;
using System.Threading.Tasks;

namespace VoiceBotSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for nodes that accept audio input
    /// </summary>
    public interface IAudioInputNode : INode
    {
        /// <summary>
        /// Accept audio data for processing
        /// </summary>
        Task<bool> AcceptAudioAsync(IAudioData audioData, ProcessingContext context);

        /// <summary>
        /// Supported audio formats
        /// </summary>
        IList<AudioFormat> SupportedFormats { get; }
    }
}
