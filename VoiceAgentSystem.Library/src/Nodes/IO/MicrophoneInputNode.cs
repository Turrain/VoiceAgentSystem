using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PortAudioSharp;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.IO
{
    /// <summary>
    /// Node for capturing audio from a microphone device using PortAudio (cross-platform)
    /// </summary>
    public class CrossPlatformMicrophoneNode : StreamingNodeBase, INode
    {
           private PortAudioSharp.Stream _inputStream;
        private readonly int _deviceIndex;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _framesPerBuffer;
        private readonly int _bitsPerSample;
        private bool _isInitialized = false;
        private PortAudioSharp.Stream.Callback _streamCallback;
        
        /// <summary>
        /// Output audio format
        /// </summary>
        public AudioFormat OutputFormat { get; private set; }
        
        /// <summary>
        /// Event raised when audio data is captured
        /// </summary>
        public event EventHandler<AudioCapturedEventArgs> AudioCaptured;
        
        /// <summary>
        /// Create cross-platform microphone input node
        /// </summary>
        public CrossPlatformMicrophoneNode(
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
                
                // Log device info
                LogDeviceInfo();
                
                // Setup stream parameters
                var inputParameters = new StreamParameters
                {
                    device = _deviceIndex >= 0 ? _deviceIndex : PortAudio.DefaultInputDevice,
                    channelCount = _channels,
                    sampleFormat = SampleFormat.Int16, // Using Int16 format
                    suggestedLatency = 0.1, // 100ms latency
                    hostApiSpecificStreamInfo = IntPtr.Zero
                };
                
                // Create callback delegate
                _streamCallback = new PortAudioSharp.Stream.Callback(DataAvailableCallback);
                
                // Create stream with null output parameters (input only)
                _inputStream = new PortAudioSharp.Stream(
                    inputParameters,  // Input parameters
                    null,            // No output parameters
                    _sampleRate,     // Sample rate
                    (uint)_framesPerBuffer, // Frames per buffer
                    StreamFlags.ClipOff, // Prevent clipping
                    _streamCallback,  // Callback function
                    IntPtr.Zero      // User data
                );
                
                // Add device info to metadata
                Metadata["DeviceIndex"] = inputParameters.device;
                Metadata["DeviceName"] = GetDeviceName(inputParameters.device);
            }
            catch (Exception ex)
            {
                Metadata["InitError"] = ex.Message;
                throw new InvalidOperationException($"Failed to initialize microphone: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Get audio output from this node
        /// </summary>
        public Task<IAudioData> GetAudioOutputAsync(ProcessingContext context)
        {
            // This node generates audio via events, not direct requests
            return Task.FromResult<IAudioData>(null);
        }
        
        /// <summary>
        /// Called when streaming starts
        /// </summary>
        protected override async Task OnStartStreamingAsync(CancellationToken cancellationToken)
        {
            await base.OnStartStreamingAsync(cancellationToken);
            
            try
            {
                // Start capturing audio
                _inputStream.Start();
                Metadata["IsRecording"] = true;
            }
            catch (Exception ex)
            {
                Metadata["StartError"] = ex.Message;
                throw new InvalidOperationException($"Failed to start recording: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Called when streaming stops
        /// </summary>
        protected override async Task OnStopStreamingAsync()
        {
            try
            {
                // Stop capturing audio
                _inputStream.Stop();
                Metadata["IsRecording"] = false;
            }
            catch (Exception ex)
            {
                Metadata["StopError"] = ex.Message;
            }
            
            await base.OnStopStreamingAsync();
        }
        
        /// <summary>
        /// Process audio data from PortAudio
        /// </summary>
        private StreamCallbackResult DataAvailableCallback(
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
                
                // Create buffer and copy audio data
                byte[] buffer = new byte[bufferSizeInBytes];
                Marshal.Copy(inputBuffer, buffer, 0, bufferSizeInBytes);
                
                // Create audio data
                var audioData = new PCMAudioData(buffer, OutputFormat);
                
                // Create context
                var context = new ProcessingContext(null, StreamingCts?.Token ?? CancellationToken.None);
                
                // Dispatch to the main thread to avoid threading issues
                Task.Run(() => 
                {
                    // Raise event
                    OnAudioCaptured(new AudioCapturedEventArgs(audioData, context));
                    
                    // Also raise data processed event for streaming pipeline
                    OnDataProcessed(new StreamingDataEventArgs(audioData, context));
                    
                    // Propagate to output connections
                    foreach (var connection in OutputConnections)
                    {
                        if (connection.IsEnabled)
                        {
                            // Pass audio through connections
                            _ = connection.TransferDataAsync(audioData, context);
                        }
                    }
                });
                
                return StreamCallbackResult.Continue;
            }
            catch (Exception ex)
            {
                Metadata["ProcessError"] = ex.Message;
                return StreamCallbackResult.Complete;
            }
        }
        
        /// <summary>
        /// Raise audio captured event
        /// </summary>
        protected virtual void OnAudioCaptured(AudioCapturedEventArgs e)
        {
            AudioCaptured?.Invoke(this, e);
        }
        
        /// <summary>
        /// Log information about available devices
        /// </summary>
        private void LogDeviceInfo()
        {
            int deviceCount = PortAudio.DeviceCount;
            Console.WriteLine($"Found {deviceCount} audio devices:");
            
            for (int i = 0; i < deviceCount; i++)
            {
                var deviceInfo = PortAudio.GetDeviceInfo(i);
                string type = deviceInfo.maxInputChannels > 0 ? "Input" : "Output";
                Console.WriteLine($"  [{i}] {deviceInfo.name} ({type})");
            }
            
            Console.WriteLine($"Default input device: {PortAudio.DefaultInputDevice}");
            Console.WriteLine($"Default output device: {PortAudio.DefaultOutputDevice}");
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
            _inputStream?.Dispose();
            _inputStream = null;
            
            await base.Shutdown();
        }
    }
      public class AudioCapturedEventArgs : EventArgs
    {
        /// <summary>
        /// Audio data
        /// </summary>
        public IAudioData AudioData { get; }
        
        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }
        
        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }
        
        /// <summary>
        /// Create audio captured event args
        /// </summary>
        public AudioCapturedEventArgs(IAudioData audioData, ProcessingContext context)
        {
            AudioData = audioData;
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
}