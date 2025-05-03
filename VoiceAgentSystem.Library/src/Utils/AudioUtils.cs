using System;
using System.IO;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Utils
{
    /// <summary>
    /// Utilities for working with audio
    /// </summary>
    public static class AudioUtils
    {
        /// <summary>
        /// Load raw PCM audio from a file
        /// </summary>
        public static async Task<IAudioData> LoadRawPcmFromFileAsync(string filePath, AudioFormat format)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Audio file not found: {filePath}");

            byte[] data = await File.ReadAllBytesAsync(filePath);
            return new PCMAudioData(data, format);
        }

        /// <summary>
        /// Save raw PCM audio to a file
        /// </summary>
        public static async Task SaveRawPcmToFileAsync(IAudioData audio, string filePath)
        {
            if (audio == null)
                throw new ArgumentNullException(nameof(audio));

            await File.WriteAllBytesAsync(filePath, audio.RawData);
        }

        /// <summary>
        /// Convert between audio formats
        /// </summary>
        public static IAudioData ConvertFormat(IAudioData source, AudioFormat targetFormat)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (targetFormat == null)
                throw new ArgumentNullException(nameof(targetFormat));

            // If formats are the same, just return a clone
            if (source.Format.Equals(targetFormat))
                return source.Clone();

            // For now, only support very simple conversions
            // In a real implementation, this would involve resampling, etc.

            // Handle bit depth conversion (16-bit to 32-bit float and vice versa)
            if (source.Format.SampleRate == targetFormat.SampleRate &&
                source.Format.Channels == targetFormat.Channels)
            {
                if (source.Format.BitsPerSample == 16 && !source.Format.IsFloat &&
                    targetFormat.BitsPerSample == 32 && targetFormat.IsFloat)
                {
                    return Convert16BitTo32BitFloat(source, targetFormat);
                }
                else if (source.Format.BitsPerSample == 32 && source.Format.IsFloat &&
                        targetFormat.BitsPerSample == 16 && !targetFormat.IsFloat)
                {
                    return Convert32BitFloatTo16Bit(source, targetFormat);
                }
            }

            throw new NotImplementedException($"Conversion from {source.Format} to {targetFormat} is not implemented");
        }

        /// <summary>
        /// Convert 16-bit PCM to 32-bit float
        /// </summary>
        private static IAudioData Convert16BitTo32BitFloat(IAudioData source, AudioFormat targetFormat)
        {
            int inputSamples = source.RawData.Length / 2; // 16-bit = 2 bytes per sample
            int outputLength = inputSamples * 4; // 32-bit = 4 bytes per sample

            byte[] outputData = new byte[outputLength];

            for (int i = 0; i < inputSamples; i++)
            {
                int inputOffset = i * 2;
                int outputOffset = i * 4;

                // Convert 16-bit PCM to float
                short pcmValue = (short)((source.RawData[inputOffset + 1] << 8) | source.RawData[inputOffset]);
                float floatValue = pcmValue / 32768.0f; // Normalize to [-1, 1]

                // Convert float to bytes
                byte[] floatBytes = BitConverter.GetBytes(floatValue);
                Buffer.BlockCopy(floatBytes, 0, outputData, outputOffset, 4);
            }

            return new PCMAudioData(outputData, targetFormat);
        }

        /// <summary>
        /// Convert 32-bit float to 16-bit PCM
        /// </summary>
        private static IAudioData Convert32BitFloatTo16Bit(IAudioData source, AudioFormat targetFormat)
        {
            int inputSamples = source.RawData.Length / 4; // 32-bit = 4 bytes per sample
            int outputLength = inputSamples * 2; // 16-bit = 2 bytes per sample

            byte[] outputData = new byte[outputLength];

            for (int i = 0; i < inputSamples; i++)
            {
                int inputOffset = i * 4;
                int outputOffset = i * 2;

                // Convert bytes to float
                int intValue = (source.RawData[inputOffset + 3] << 24) |
                              (source.RawData[inputOffset + 2] << 16) |
                              (source.RawData[inputOffset + 1] << 8) |
                              source.RawData[inputOffset];

                float floatValue = BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);

                // Convert float to 16-bit PCM
                short pcmValue = (short)(floatValue * 32767);

                // Write PCM value to output
                outputData[outputOffset] = (byte)(pcmValue & 0xFF);
                outputData[outputOffset + 1] = (byte)((pcmValue >> 8) & 0xFF);
            }

            return new PCMAudioData(outputData, targetFormat);
        }

        /// <summary>
        /// Mix two audio streams together
        /// </summary>
        public static IAudioData MixAudio(IAudioData audio1, IAudioData audio2, float gain1 = 1.0f, float gain2 = 1.0f)
        {
            if (audio1 == null)
                throw new ArgumentNullException(nameof(audio1));

            if (audio2 == null)
                throw new ArgumentNullException(nameof(audio2));

            // Check format compatibility
            if (!IsFormatCompatible(audio1.Format, audio2.Format))
                throw new ArgumentException("Audio formats are not compatible for mixing");

            // Use the format of the first audio
            var format = audio1.Format;

            // Find the length of the longest buffer
            int maxLength = Math.Max(audio1.RawData.Length, audio2.RawData.Length);
            byte[] mixedData = new byte[maxLength];

            // Mix based on format
            if (format.BitsPerSample == 16 && !format.IsFloat)
            {
                Mix16BitPcm(audio1.RawData, audio2.RawData, mixedData, gain1, gain2);
            }
            else if (format.BitsPerSample == 32 && format.IsFloat)
            {
                Mix32BitFloat(audio1.RawData, audio2.RawData, mixedData, gain1, gain2);
            }
            else
            {
                throw new NotImplementedException($"Mixing for format {format} is not implemented");
            }

            return new PCMAudioData(mixedData, format);
        }

        /// <summary>
        /// Mix 16-bit PCM audio
        /// </summary>
        private static void Mix16BitPcm(byte[] data1, byte[] data2, byte[] output, float gain1, float gain2)
        {
            int length1 = data1.Length;
            int length2 = data2.Length;
            int maxLength = Math.Max(length1, length2);

            for (int i = 0; i < maxLength; i += 2)
            {
                // Default to silence
                int mixed = 0;

                // Add first audio if in range
                if (i < length1)
                {
                    short sample1 = (short)((data1[i + 1] << 8) | data1[i]);
                    mixed += (int)(sample1 * gain1);
                }

                // Add second audio if in range
                if (i < length2)
                {
                    short sample2 = (short)((data2[i + 1] << 8) | data2[i]);
                    mixed += (int)(sample2 * gain2);
                }

                // Clamp to short range
                if (mixed > short.MaxValue) mixed = short.MaxValue;
                if (mixed < short.MinValue) mixed = short.MinValue;

                // Convert to bytes
                short result = (short)mixed;
                output[i] = (byte)(result & 0xFF);
                output[i + 1] = (byte)((result >> 8) & 0xFF);
            }
        }

        /// <summary>
        /// Mix 32-bit float audio
        /// </summary>
        private static void Mix32BitFloat(byte[] data1, byte[] data2, byte[] output, float gain1, float gain2)
        {
            int length1 = data1.Length;
            int length2 = data2.Length;
            int maxLength = Math.Max(length1, length2);

            for (int i = 0; i < maxLength; i += 4)
            {
                // Default to silence
                float mixed = 0;

                // Add first audio if in range
                if (i + 3 < length1)
                {
                    int intValue1 = (data1[i + 3] << 24) | (data1[i + 2] << 16) |
                                   (data1[i + 1] << 8) | data1[i];

                    float sample1 = BitConverter.ToSingle(BitConverter.GetBytes(intValue1), 0);
                    mixed += sample1 * gain1;
                }

                // Add second audio if in range
                if (i + 3 < length2)
                {
                    int intValue2 = (data2[i + 3] << 24) | (data2[i + 2] << 16) |
                                   (data2[i + 1] << 8) | data2[i];

                    float sample2 = BitConverter.ToSingle(BitConverter.GetBytes(intValue2), 0);
                    mixed += sample2 * gain2;
                }

                // Clamp to float range [-1, 1]
                if (mixed > 1.0f) mixed = 1.0f;
                if (mixed < -1.0f) mixed = -1.0f;

                // Convert to bytes
                byte[] floatBytes = BitConverter.GetBytes(mixed);
                Buffer.BlockCopy(floatBytes, 0, output, i, 4);
            }
        }

        /// <summary>
        /// Check if two audio formats are compatible
        /// </summary>
        private static bool IsFormatCompatible(AudioFormat format1, AudioFormat format2)
        {
            return format1.SampleRate == format2.SampleRate &&
                   format1.Channels == format2.Channels &&
                   format1.BitsPerSample == format2.BitsPerSample &&
                   format1.IsFloat == format2.IsFloat;
        }
    }
}
