using System;

namespace VoiceBotSystem.Core
{
    /// <summary>
    /// Audio format descriptor
    /// </summary>
    public class AudioFormat : IEquatable<AudioFormat>
    {
        /// <summary>
        /// Sample rate in Hz
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Number of channels
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// Bits per sample
        /// </summary>
        public int BitsPerSample { get; set; }

        /// <summary>
        /// Whether the format is floating point
        /// </summary>
        public bool IsFloat { get; set; }

        /// <summary>
        /// Size of each frame in bytes
        /// </summary>
        public int FrameSize => Channels * (BitsPerSample / 8);

        /// <summary>
        /// Bytes per second of audio
        /// </summary>
        public int BytesPerSecond => SampleRate * FrameSize;

        /// <summary>
        /// Default audio format (16kHz, mono, 16-bit PCM)
        /// </summary>
        public static AudioFormat Default => new AudioFormat
        {
            SampleRate = 16000,
            Channels = 1,
            BitsPerSample = 16,
            IsFloat = false
        };

        /// <summary>
        /// Standard CD quality audio format (44.1kHz, stereo, 16-bit PCM)
        /// </summary>
        public static AudioFormat CD => new AudioFormat
        {
            SampleRate = 44100,
            Channels = 2,
            BitsPerSample = 16,
            IsFloat = false
        };

        /// <summary>
        /// High quality audio format (48kHz, stereo, 24-bit PCM)
        /// </summary>
        public static AudioFormat HighQuality => new AudioFormat
        {
            SampleRate = 48000,
            Channels = 2,
            BitsPerSample = 24,
            IsFloat = false
        };

        /// <summary>
        /// Check if two formats are equal
        /// </summary>
        public bool Equals(AudioFormat other)
        {
            if (other == null) return false;

            return SampleRate == other.SampleRate &&
                   Channels == other.Channels &&
                   BitsPerSample == other.BitsPerSample &&
                   IsFloat == other.IsFloat;
        }

        /// <summary>
        /// Check if two formats are equal
        /// </summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as AudioFormat);
        }

        /// <summary>
        /// Get hash code for the format
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(SampleRate, Channels, BitsPerSample, IsFloat);
        }

        /// <summary>
        /// Convert to string
        /// </summary>
        public override string ToString()
        {
            return $"{SampleRate}Hz, {Channels} channel(s), {BitsPerSample}-bit {(IsFloat ? "float" : "PCM")}";
        }
    }
}
