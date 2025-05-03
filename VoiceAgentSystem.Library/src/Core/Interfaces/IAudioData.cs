using System.Collections.Generic;

namespace VoiceBotSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for audio data
    /// </summary>
    public interface IAudioData
    {
        /// <summary>
        /// Raw PCM audio data
        /// </summary>
        byte[] RawData { get; }

        /// <summary>
        /// Audio format information
        /// </summary>
        AudioFormat Format { get; }

        /// <summary>
        /// Duration of audio in seconds
        /// </summary>
        double DurationInSeconds { get; }

        /// <summary>
        /// Create a segment of this audio
        /// </summary>
        IAudioData CreateSegment(int startByteOffset, int length);

        /// <summary>
        /// Create a copy of this audio
        /// </summary>
        IAudioData Clone();

        /// <summary>
        /// Audio metadata
        /// </summary>
        IDictionary<string, object> Metadata { get; }
    }
}
