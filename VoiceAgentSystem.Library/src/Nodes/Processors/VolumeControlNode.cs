using System;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.Processors
{
    /// <summary>
    /// Volume control node for adjusting audio volume
    /// </summary>
    public class VolumeControlNode : AudioProcessorNodeBase
    {
        /// <summary>
        /// Gain factor (1.0 = no change, 0.5 = half volume, 2.0 = double volume)
        /// </summary>
        public double Gain { get; set; } = 1.0;

        /// <summary>
        /// Create volume control node
        /// </summary>
        public VolumeControlNode(string id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// Process audio data
        /// </summary>
        public override async Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context)
        {
            if (Math.Abs(Gain - 1.0) < 0.001)
            {
                // No change needed if gain is approximately 1.0
                context?.LogInformation($"Volume node {Name} skipping processing (gain ≈ 1.0)");
                return input;
            }

            var audioFormat = input.Format;
            byte[] outputBuffer = new byte[input.RawData.Length];

            context?.LogInformation($"Volume node {Name} applying gain: {Gain:F2}");

            await Task.Run(() =>
            {
                if (audioFormat.BitsPerSample == 16 && !audioFormat.IsFloat)
                {
                    Apply16BitGain(input.RawData, outputBuffer, Gain);
                }
                else if (audioFormat.BitsPerSample == 32 && audioFormat.IsFloat)
                {
                    Apply32BitFloatGain(input.RawData, outputBuffer, Gain);
                }
                else
                {
                    // Unsupported format, just copy
                    Buffer.BlockCopy(input.RawData, 0, outputBuffer, 0, input.RawData.Length);
                    context?.LogWarning($"Volume node {Name} unsupported format: {audioFormat}");
                }
            });

            var result = new PCMAudioData(outputBuffer, audioFormat);
            result.Metadata["OriginalGain"] = Gain;

            // Update output format
            OutputFormat = audioFormat;

            return result;
        }

        /// <summary>
        /// Apply gain to 16-bit PCM samples
        /// </summary>
        private void Apply16BitGain(byte[] inputBuffer, byte[] outputBuffer, double gain)
        {
            // Apply gain to 16-bit PCM samples
            for (int i = 0; i < inputBuffer.Length; i += 2)
            {
                if (i + 1 >= inputBuffer.Length)
                    break;

                // Convert bytes to short (16-bit)
                short sample = (short)((inputBuffer[i + 1] << 8) | inputBuffer[i]);

                // Apply gain
                double gainedSample = sample * gain;

                // Clamp to short range
                if (gainedSample > short.MaxValue) gainedSample = short.MaxValue;
                if (gainedSample < short.MinValue) gainedSample = short.MinValue;

                // Convert back to bytes
                short result = (short)gainedSample;
                outputBuffer[i] = (byte)(result & 0xFF);
                outputBuffer[i + 1] = (byte)((result >> 8) & 0xFF);
            }
        }

        /// <summary>
        /// Apply gain to 32-bit float samples
        /// </summary>
        private void Apply32BitFloatGain(byte[] inputBuffer, byte[] outputBuffer, double gain)
        {
            // Apply gain to 32-bit float samples
            for (int i = 0; i < inputBuffer.Length; i += 4)
            {
                if (i + 3 >= inputBuffer.Length)
                    break;

                // Convert bytes to float (32-bit)
                int intValue = (inputBuffer[i + 3] << 24) |
                               (inputBuffer[i + 2] << 16) |
                               (inputBuffer[i + 1] << 8) |
                               inputBuffer[i];

                float sample = BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);

                // Apply gain
                float gainedSample = sample * (float)gain;

                // Clamp to float range [-1.0, 1.0]
                if (gainedSample > 1.0f) gainedSample = 1.0f;
                if (gainedSample < -1.0f) gainedSample = -1.0f;

                // Convert back to bytes
                byte[] floatBytes = BitConverter.GetBytes(gainedSample);
                Buffer.BlockCopy(floatBytes, 0, outputBuffer, i, 4);
            }
        }
    }
}
