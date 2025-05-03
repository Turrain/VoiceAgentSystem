using System.Threading.Tasks;

namespace VoiceBotSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for nodes that process audio data
    /// </summary>
    public interface IAudioProcessorNode : IAudioInputNode, IAudioOutputNode
    {
        /// <summary>
        /// Process audio data
        /// </summary>
        Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context);
    }
}
