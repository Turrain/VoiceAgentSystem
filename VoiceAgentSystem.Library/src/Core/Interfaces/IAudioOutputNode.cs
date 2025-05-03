using System.Threading.Tasks;

namespace VoiceBotSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for nodes that produce audio output
    /// </summary>
    public interface IAudioOutputNode : INode
    {
        /// <summary>
        /// Get audio output from this node
        /// </summary>
        Task<IAudioData> GetAudioOutputAsync(ProcessingContext context);

        /// <summary>
        /// Output audio format
        /// </summary>
        AudioFormat OutputFormat { get; }
    }
}
