using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using PortAudioSharp;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.IO
{
    /// <summary>
    /// Node for playing audio through speakers using PortAudio (cross-platform)
    /// </summary>
    public class CrossPlatformSpeakerNode : StreamingNodeBase, IAudioProcessorNode
    {
        private PortAudioSharp.Stream _outputStream;
        private readonly int _deviceIndex;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _framesPerBuffer;
        private readonly int _bitsPerSample;
        private readonly Queue<byte[]> _audioQueue = new Queue<byte[]>();
        private readonly object _queueLock = new object();
        private bool _isInitialized = false;
        private bool _isPlaying = false;
        private PortAudioSharp.Stream.Callback _streamCallback;
        
        /// <summary>
        /// Supported audio formats
        /// </summary>
        public IList<AudioFormat> SupportedFormats { get; } = new List<AudioFormat>();
        
        /// <summary>
        /// Output audio format
        /// </summary>
        public AudioFormat OutputFormat { get; protected set; }
        
        /// <summary>
        /// Create cross-platform speaker output node
        /// </summary>
        public CrossPlatformSpeakerNode(
            string id, 
            string name, 
            int deviceIndex = -1, 
            int sampleRate = 16000, 
            int channels = 1, 
            int framesPerBuffer = 1024,
            int bitsPerSample = 16
        ) : base(id, name)
        {
            _deviceIndex = deviceIndex;
            _sampleRate = sampleRate;
            _channels = channels;
            _framesPerBuffer = framesPerBuffer;
            _bitsPerSample = bitsPerSample;
            
            // Configure output format
            OutputFormat = new AudioFormat
            {
                SampleRate = sampleRate,
                Channels = channels,
                BitsPerSample = bitsPerSample,
                IsFloat = false
            };
            
            // Add supported format
            SupportedFormats.Add(OutputFormat);
        }
        
        /// <summary>
        /// Initialize the node
        /// </summary>
        public override async Task Initialize()
        {
            await base.Initialize();
            
            try
            {
                // Initialize PortAudio
                if (!_isInitialized)
                {
                    PortAudio.Initialize();
                    _isInitialized = true;
                }
                
                // Setup stream parameters
                var outputParameters = new StreamParameters
                {
                    // Use the correct device index
                    device = _deviceIndex >= 0 ? _deviceIndex : PortAudio.DefaultOutputDevice,
                    channelCount = _channels,
                    sampleFormat = SampleFormat.Int16, // Using Int16 format
                    suggestedLatency = 0.1, // 100ms latency
                    hostApiSpecificStreamInfo = IntPtr.Zero
                };
                
                // Create callback delegate
                _streamCallback = new PortAudioSharp.Stream.Callback(PlaybackCallback);
                
                // Create stream with null input parameters (output only)
                _outputStream = new PortAudioSharp.Stream(
                    null,            // No input parameters
                    outputParameters, // Output parameters
                    _sampleRate,     // Sample rate
                    (uint)_framesPerBuffer, // Frames per buffer
                    StreamFlags.ClipOff, // Prevent clipping
                    _streamCallback,  // Callback function
                    IntPtr.Zero      // User data
                );
                
                // Add device info to metadata
                Metadata["DeviceIndex"] = outputParameters.device;
                Metadata["DeviceName"] = GetDeviceName(outputParameters.device);
            }
            catch (Exception ex)
            {
                Metadata["InitError"] = ex.Message;
                throw new InvalidOperationException($"Failed to initialize speaker output: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Accept audio data for processing
        /// </summary>
        public Task<bool> AcceptAudioAsync(IAudioData audioData, ProcessingContext context)
        {
            if (!IsEnabled || audioData == null)
                return Task.FromResult(false);
                
            // Check format compatibility
            if (!IsFormatSupported(audioData.Format))
            {
                context?.LogWarning($"Unsupported audio format: {audioData.Format}");
                return Task.FromResult(false);
            }
            
            // Add audio to queue
            lock (_queueLock)
            {
                _audioQueue.Enqueue(audioData.RawData);
            }
            
            // Start playback if not already playing
            if (!_isPlaying)
            {
                StartPlayback();
            }
            
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Process audio data
        /// </summary>
        public async Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context)
        {
            if (!IsEnabled || input == null)
                return null;
                
            // Forward to AcceptAudioAsync
            await AcceptAudioAsync(input, context);
            
            // Return the same audio for chaining
            return input;
        }
        
        /// <summary>
        /// Get audio output from this node
        /// </summary>
        public Task<IAudioData> GetAudioOutputAsync(ProcessingContext context)
        {
            // This node consumes audio, doesn't produce it
            return Task.FromResult<IAudioData>(null);
        }
        
        /// <summary>
        /// Called when streaming starts
        /// </summary>
        protected override async Task OnStartStreamingAsync(CancellationToken cancellationToken)
        {
            await base.OnStartStreamingAsync(cancellationToken);
            
            // Clear audio queue
            lock (_queueLock)
            {
                _audioQueue.Clear();
            }
        }
        
        /// <summary>
        /// Called when streaming stops
        /// </summary>
        protected override async Task OnStopStreamingAsync()
        {
            // Stop playback
            StopPlayback();
            
            await base.OnStopStreamingAsync();
        }
        
        /// <summary>
        /// Start audio playback
        /// </summary>
        private void StartPlayback()
        {
            if (_isPlaying)
                return;
                
            try
            {
                _outputStream.Start();
                _isPlaying = true;
                Metadata["IsPlaying"] = true;
            }
            catch (Exception ex)
            {
                Metadata["PlaybackError"] = ex.Message;
            }
        }
        
        /// <summary>
        /// Stop audio playback
        /// </summary>
        private void StopPlayback()
        {
            if (!_isPlaying)
                return;
                
            try
            {
                _outputStream.Stop();
                _isPlaying = false;
                Metadata["IsPlaying"] = false;
            }
            catch (Exception ex)
            {
                Metadata["StopError"] = ex.Message;
            }
        }
        
        /// <summary>
        /// Callback for audio playback
        /// </summary>
        private StreamCallbackResult PlaybackCallback(
            IntPtr inputBuffer, 
            IntPtr outputBuffer, 
            uint framesPerBuffer, 
            ref StreamCallbackTimeInfo timeInfo, 
            StreamCallbackFlags statusFlags, 
            IntPtr userData)
        {
            if (!IsEnabled || !IsStreaming)
                return StreamCallbackResult.Complete;
                
            try
            {
                // Calculate buffer size in bytes
                int bufferSizeInBytes = (int)framesPerBuffer * _channels * (_bitsPerSample / 8);
                
                // Get audio data from queue
                byte[] audioData = null;
                bool hasData = false;
                
                lock (_queueLock)
                {
                    if (_audioQueue.Count > 0)
                    {
                        audioData = _audioQueue.Dequeue();
                        hasData = true;
                    }
                }
                
                if (hasData && audioData != null)
                {
                    // Copy data to output buffer (up to buffer size)
                    int bytesToCopy = Math.Min(audioData.Length, bufferSizeInBytes);
                    Marshal.Copy(audioData, 0, outputBuffer, bytesToCopy);
                    
                    // Fill remaining buffer with silence if needed
                    if (bytesToCopy < bufferSizeInBytes)
                    {
                        IntPtr remainingBuffer = IntPtr.Add(outputBuffer, bytesToCopy);
                        int remainingBytes = bufferSizeInBytes - bytesToCopy;
                        // Use zero for silence
                        for (int i = 0; i < remainingBytes; i++)
                        {
                            Marshal.WriteByte(IntPtr.Add(remainingBuffer, i), 0);
                        }
                    }
                }
                else
                {
                    // No data in queue, fill with silence
                    for (int i = 0; i < bufferSizeInBytes; i++)
                    {
                        Marshal.WriteByte(IntPtr.Add(outputBuffer, i), 0);
                    }
                    
                    // Check if we should stop playback (no more data)
                    lock (_queueLock)
                    {
                        if (_audioQueue.Count == 0)
                        {
                            // Schedule stop on another thread to avoid deadlock
                            Task.Run(() => StopPlayback());
                        }
                    }
                }
                
                return StreamCallbackResult.Continue;
            }
            catch (Exception ex)
            {
                Metadata["PlaybackError"] = ex.Message;
                return StreamCallbackResult.Complete;
            }
        }
        
        /// <summary>
        /// Check if audio format is supported
        /// </summary>
        protected bool IsFormatSupported(AudioFormat format)
        {
            if (SupportedFormats.Count == 0)
                return true;
                
            foreach (var supportedFormat in SupportedFormats)
            {
                if (supportedFormat.SampleRate == format.SampleRate &&
                    supportedFormat.Channels == format.Channels &&
                    supportedFormat.BitsPerSample == format.BitsPerSample)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Get device name by index
        /// </summary>
        private string GetDeviceName(int deviceIndex)
        {
            var deviceInfo = PortAudio.GetDeviceInfo(deviceIndex);
            return deviceInfo.name ?? "Unknown Device";
        }
        
        /// <summary>
        /// Shutdown the node
        /// </summary>
        public override async Task Shutdown()
        {
            if (IsStreaming)
            {
                await StopStreamingAsync();
            }
            
            // Clean up PortAudio stream
            _outputStream?.Dispose();
            _outputStream = null;
            
            await base.Shutdown();
        }
    }
}