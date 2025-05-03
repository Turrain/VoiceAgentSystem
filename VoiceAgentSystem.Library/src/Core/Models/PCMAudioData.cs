using System;
using System.Collections.Generic;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Core
{
    /// <summary>
    /// Implementation of PCM audio data
    /// </summary>
    public class PCMAudioData : IAudioData
    {
        /// <summary>
        /// Raw PCM audio data
        /// </summary>
        public byte[] RawData { get; }

        /// <summary>
        /// Audio format
        /// </summary>
        public AudioFormat Format { get; }

        /// <summary>
        /// Duration of audio in seconds
        /// </summary>
        public double DurationInSeconds => RawData.Length / (double)Format.BytesPerSecond;

        /// <summary>
        /// Audio metadata
        /// </summary>
        public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Create PCM audio data
        /// </summary>
        public PCMAudioData(byte[] rawData, AudioFormat format)
        {
            RawData = rawData ?? throw new ArgumentNullException(nameof(rawData));
            Format = format ?? throw new ArgumentNullException(nameof(format));
        }

        /// <summary>
        /// Create a segment of this audio
        /// </summary>
        public IAudioData CreateSegment(int startByteOffset, int length)
        {
            if (startByteOffset < 0 || startByteOffset >= RawData.Length)
                throw new ArgumentOutOfRangeException(nameof(startByteOffset));

            if (length <= 0 || startByteOffset + length > RawData.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            byte[] segmentData = new byte[length];
            Array.Copy(RawData, startByteOffset, segmentData, 0, length);

            var segment = new PCMAudioData(segmentData, Format);

            // Copy relevant metadata
            foreach (var kvp in Metadata)
            {
                segment.Metadata[kvp.Key] = kvp.Value;
            }

            // Add segment information
            segment.Metadata["OriginalOffset"] = startByteOffset;
            segment.Metadata["OriginalLength"] = RawData.Length;

            return segment;
        }

        /// <summary>
        /// Create a copy of this audio
        /// </summary>
        public IAudioData Clone()
        {
            byte[] clonedData = new byte[RawData.Length];
            Array.Copy(RawData, clonedData, RawData.Length);

            var clone = new PCMAudioData(clonedData, Format);

            // Copy metadata
            foreach (var kvp in Metadata)
            {
                clone.Metadata[kvp.Key] = kvp.Value;
            }

            return clone;
        }
    }
}
