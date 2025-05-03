using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.Processors
{
    /// <summary>
    /// Audio mixer node that can combine multiple audio streams
    /// </summary>
    public class AudioMixerNode : AudioProcessorNodeBase
    {
        /// <summary>
        /// Input buffers by source ID
        /// </summary>
        private readonly Dictionary<string, IAudioData> _inputBuffers = new Dictionary<string, IAudioData>();

        /// <summary>
        /// Lock for buffer access
        /// </summary>
        private readonly object _bufferLock = new object();

        /// <summary>
        /// Maximum age of buffers in milliseconds before they're discarded
        /// </summary>
        public int MaxBufferAge { get; set; } = 5000;

        /// <summary>
        /// Whether to normalize the output audio
        /// </summary>
        public bool NormalizeOutput { get; set; } = true;

        /// <summary>
        /// Channel weights for mixing
        /// </summary>
        public double[] ChannelWeights { get; set; } = null;

        /// <summary>
        /// Create audio mixer node
        /// </summary>
        public AudioMixerNode(string id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// Accept audio data for processing
        /// </summary>
        public override async Task<bool> AcceptAudioAsync(IAudioData audioData, ProcessingContext context)
        {
            if (!IsEnabled || audioData == null)
                return false;

            // Get or generate source ID
            string sourceId = context.GetTransientData<string>("SourceId", Guid.NewGuid().ToString());

            // Store in buffer with timestamp
            lock (_bufferLock)
            {
                _inputBuffers[sourceId] = audioData;
                context.SetTransientData($"Buffer_{sourceId}_Timestamp", DateTimeOffset.Now);
            }

            context.LogInformation($"Mixer node {Name} received audio from source {sourceId}: {audioData.DurationInSeconds:F2}s");

            // Process and propagate as normal
            return await base.AcceptAudioAsync(audioData, context);
        }

        /// <summary>
        /// Process audio data
        /// </summary>
        public override async Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context)
        {
            // Clean old buffers
            CleanExpiredBuffers(context);

            // If no buffers, just pass through the input
            lock (_bufferLock)
            {
                if (_inputBuffers.Count == 0)
                    return input;

                // If only one buffer and it's the input, just pass it through
                if (_inputBuffers.Count == 1 && _inputBuffers.Values.Contains(input))
                    return input;
            }

            // Mix all available buffers
            IAudioData mixedAudio = await MixAudioBuffers(context);

            if (mixedAudio != null)
            {
                // Update output format
                OutputFormat = mixedAudio.Format;

                context.LogInformation($"Mixer node {Name} mixed {_inputBuffers.Count} audio streams: {mixedAudio.DurationInSeconds:F2}s");
            }

            return mixedAudio;
        }

        /// <summary>
        /// Mix audio buffers
        /// </summary>
        private async Task<IAudioData> MixAudioBuffers(ProcessingContext context)
        {
            List<IAudioData> compatibleAudio;

            lock (_bufferLock)
            {
                // No buffers available
                if (_inputBuffers.Count == 0)
                    return null;

                // Get the first format as reference
                var referenceFormat = _inputBuffers.Values.First().Format;

                // Get all audio with the same format
                compatibleAudio = _inputBuffers.Values
                    .Where(a => IsCompatibleFormat(a.Format, referenceFormat))
                    .ToList();
            }

            if (compatibleAudio.Count == 0)
                return null;

            if (compatibleAudio.Count == 1)
                return compatibleAudio[0];

            // Find the longest buffer
            int maxLength = compatibleAudio.Max(a => a.RawData.Length);
            byte[] mixedBuffer = new byte[maxLength];

            // Get reference format
            var audioFormat = compatibleAudio[0].Format;

            if (audioFormat.BitsPerSample == 16 && !audioFormat.IsFloat)
            {
                // 16-bit PCM mixing
                await Task.Run(() => Mix16BitPcm(compatibleAudio, mixedBuffer, audioFormat));
            }
            else if (audioFormat.BitsPerSample == 32 && audioFormat.IsFloat)
            {
                // 32-bit float mixing
                await Task.Run(() => Mix32BitFloat(compatibleAudio, mixedBuffer, audioFormat));
            }
            else
            {
                // Default to just using the first buffer for unsupported formats
                context.LogWarning($"Mixing not supported for format: {audioFormat}");
                return compatibleAudio[0];
            }

            var result = new PCMAudioData(mixedBuffer, audioFormat);
            result.Metadata["MixedBuffers"] = compatibleAudio.Count;

            return result;
        }

        /// <summary>
        /// Mix 16-bit PCM audio
        /// </summary>
        private void Mix16BitPcm(List<IAudioData> sources, byte[] targetBuffer, AudioFormat format)
        {
            int byteDepth = 2; // 16-bit = 2 bytes
            int channels = format.Channels;
            int frameSize = byteDepth * channels;

            // Initialize channel weights if needed
            if (ChannelWeights == null || ChannelWeights.Length != channels)
            {
                ChannelWeights = Enumerable.Repeat(1.0, channels).ToArray();
            }

            // Process each frame
            for (int frameOffset = 0; frameOffset < targetBuffer.Length; frameOffset += frameSize)
            {
                // For each channel in the frame
                for (int channel = 0; channel < channels; channel++)
                {
                    int channelByteOffset = frameOffset + (channel * byteDepth);

                    // Skip if beyond buffer length
                    if (channelByteOffset + 1 >= targetBuffer.Length)
                        continue;

                    // Mix samples from all sources
                    int mixedSample = 0;
                    int sourceCount = 0;

                    foreach (var source in sources)
                    {
                        if (channelByteOffset + 1 < source.RawData.Length)
                        {
                            // Convert bytes to short (16-bit)
                            short sample = (short)((source.RawData[channelByteOffset + 1] << 8) |
                                                   source.RawData[channelByteOffset]);

                            // Apply channel weight
                            sample = (short)(sample * ChannelWeights[channel]);

                            mixedSample += sample;
                            sourceCount++;
                        }
                    }

                    // Average or normalize
                    if (sourceCount > 0)
                    {
                        if (NormalizeOutput)
                        {
                            // Normalize to avoid clipping
                            mixedSample = (int)(mixedSample / (double)sourceCount);
                        }

                        // Clamp to short range
                        if (mixedSample > short.MaxValue) mixedSample = short.MaxValue;
                        if (mixedSample < short.MinValue) mixedSample = short.MinValue;

                        // Write back to target buffer
                        short result = (short)mixedSample;
                        targetBuffer[channelByteOffset] = (byte)(result & 0xFF);
                        targetBuffer[channelByteOffset + 1] = (byte)((result >> 8) & 0xFF);
                    }
                }
            }
        }

        /// <summary>
        /// Mix 32-bit float audio
        /// </summary>
        private void Mix32BitFloat(List<IAudioData> sources, byte[] targetBuffer, AudioFormat format)
        {
            int byteDepth = 4; // 32-bit = 4 bytes
            int channels = format.Channels;
            int frameSize = byteDepth * channels;

            // Initialize channel weights if needed
            if (ChannelWeights == null || ChannelWeights.Length != channels)
            {
                ChannelWeights = Enumerable.Repeat(1.0, channels).ToArray();
            }

            // Process each frame
            for (int frameOffset = 0; frameOffset < targetBuffer.Length; frameOffset += frameSize)
            {
                // For each channel in the frame
                for (int channel = 0; channel < channels; channel++)
                {
                    int channelByteOffset = frameOffset + (channel * byteDepth);

                    // Skip if beyond buffer length
                    if (channelByteOffset + 3 >= targetBuffer.Length)
                        continue;

                    // Mix samples from all sources
                    float mixedSample = 0;
                    int sourceCount = 0;

                    foreach (var source in sources)
                    {
                        if (channelByteOffset + 3 < source.RawData.Length)
                        {
                            // Convert bytes to float (32-bit)
                            int intValue = (source.RawData[channelByteOffset + 3] << 24) |
                                          (source.RawData[channelByteOffset + 2] << 16) |
                                          (source.RawData[channelByteOffset + 1] << 8) |
                                          source.RawData[channelByteOffset];

                            float sample = BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);

                            // Apply channel weight
                            sample *= (float)ChannelWeights[channel];

                            mixedSample += sample;
                            sourceCount++;
                        }
                    }

                    // Average or normalize
                    if (sourceCount > 0)
                    {
                        if (NormalizeOutput)
                        {
                            // Normalize to avoid clipping
                            mixedSample /= sourceCount;
                        }

                        // Clamp to float range [-1.0, 1.0]
                        if (mixedSample > 1.0f) mixedSample = 1.0f;
                        if (mixedSample < -1.0f) mixedSample = -1.0f;

                        // Write back to target buffer
                        byte[] floatBytes = BitConverter.GetBytes(mixedSample);
                        Buffer.BlockCopy(floatBytes, 0, targetBuffer, channelByteOffset, 4);
                    }
                }
            }
        }

        /// <summary>
        /// Clean expired buffers
        /// </summary>
        private void CleanExpiredBuffers(ProcessingContext context)
        {
            var now = DateTimeOffset.Now;
            var expiredKeys = new List<string>();

            lock (_bufferLock)
            {
                foreach (var key in _inputBuffers.Keys)
                {
                    var timestamp = context.GetTransientData<DateTimeOffset>($"Buffer_{key}_Timestamp", DateTimeOffset.MinValue);

                    if ((now - timestamp).TotalMilliseconds > MaxBufferAge)
                    {
                        expiredKeys.Add(key);
                    }
                }

                foreach (var key in expiredKeys)
                {
                    _inputBuffers.Remove(key);
                    context.RemoveTransientData($"Buffer_{key}_Timestamp");
                }
            }
        }

        /// <summary>
        /// Check if formats are compatible for mixing
        /// </summary>
        private bool IsCompatibleFormat(AudioFormat format1, AudioFormat format2)
        {
            return format1.SampleRate == format2.SampleRate &&
                   format1.Channels == format2.Channels &&
                   format1.BitsPerSample == format2.BitsPerSample &&
                   format1.IsFloat == format2.IsFloat;
        }

        /// <summary>
        /// Reset the node
        /// </summary>
        public override async Task Reset()
        {
            await base.Reset();

            lock (_bufferLock)
            {
                _inputBuffers.Clear();
            }
        }
    }
}
