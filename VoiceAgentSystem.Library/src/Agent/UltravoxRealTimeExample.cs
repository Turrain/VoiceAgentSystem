using System;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Nodes.IO;
using VoiceBotSystem.Nodes.Processors;

namespace VoiceBotSystem.Examples
{
    /// <summary>
    /// Example demonstrating real-time voice interaction using Ultravox with 
    /// cross-platform audio input and output for Linux systems
    /// </summary>
    public class UltravoxLinuxExample
    {
        private CrossPlatformMicrophoneNode _microphoneNode;
        private UltravoxWebSocketNode _ultravoxNode;
        private CrossPlatformSpeakerNode _speakerNode;
        private bool _isRunning = false;
        
        /// <summary>
        /// Run the Ultravox Linux example
        /// </summary>
        public async Task RunExampleAsync(string apiKey)
        {
            try
            {
                await InitializeNodesAsync(apiKey);
                await ConnectNodesAsync();
                await StartStreamingAsync();
                
                Console.WriteLine("Ultravox Linux example is running.");
                Console.WriteLine("Speak into your microphone to interact.");
                Console.WriteLine("Press Enter to exit.");
                
                _isRunning = true;
                Console.ReadLine();
                
                await StopStreamingAsync();
                await ShutdownNodesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                
                // Try to shut down gracefully even if we hit an error
                try
                {
                    await ShutdownNodesAsync();
                }
                catch
                {
                    // Ignore shutdown errors at this point
                }
            }
        }
        
        /// <summary>
        /// Initialize all nodes
        /// </summary>
        private async Task InitializeNodesAsync(string apiKey)
        {
            Console.WriteLine("Initializing nodes...");
            
            // Configure audio format for Ultravox
            var audioFormat = new AudioFormat
            {
                SampleRate = 8000, // Ultravox works well with 8kHz
                Channels = 1,
                BitsPerSample = 16,
                IsFloat = false
            };
            
            // Create cross-platform microphone input node
            _microphoneNode = new CrossPlatformMicrophoneNode(
                "microphone", 
                "Linux Microphone Input",
                deviceIndex: -1, // Default device
                sampleRate: audioFormat.SampleRate,
                channels: audioFormat.Channels,
                framesPerBuffer: 160,
                bitsPerSample: audioFormat.BitsPerSample
            );
            
            // Create Ultravox node
            _ultravoxNode = new UltravoxWebSocketNode(
                "ultravox", 
                "Ultravox AI",
                apiKey,
                voice: "Mark",
                model: "fixie-ai/ultravox",
                sampleRate: audioFormat.SampleRate,
                systemPrompt: "You are a helpful assistant answering calls. Keep responses brief and conversational."
            );
            
            // Create cross-platform speaker output node
            _speakerNode = new CrossPlatformSpeakerNode(
                "speaker", 
                "Linux Speaker Output",
                deviceIndex: -1, // Default device
                sampleRate: audioFormat.SampleRate,
                channels: audioFormat.Channels,
                framesPerBuffer: 160,
                bitsPerSample: audioFormat.BitsPerSample
            );
            
            // Initialize all nodes
            await _microphoneNode.Initialize();
            await _ultravoxNode.Initialize();
            await _speakerNode.Initialize();
            
            // Set up event handlers for debugging
            _microphoneNode.AudioCaptured += (sender, e) =>
            {
                Console.WriteLine($"Microphone: Captured {e.AudioData.DurationInSeconds:F2}s of audio");
            };
            
            _ultravoxNode.TranscriptionReceived += (sender, e) =>
            {
                if (e.IsFinal)
                {
                    Console.WriteLine($"Ultravox: Transcribed text: \"{e.Text}\"");
                }
            };
            
            _ultravoxNode.AudioDataReceived += (sender, e) =>
            {
                Console.WriteLine($"Ultravox: Received {e.AudioData.DurationInSeconds:F2}s of audio");
            };
            
            Console.WriteLine("Nodes initialized.");
        }
        
        /// <summary>
        /// Connect nodes together
        /// </summary>
        private async Task ConnectNodesAsync()
        {
            Console.WriteLine("Connecting to Ultravox...");
            
            // Connect to Ultravox service
            await _ultravoxNode.ConnectToUltravoxAsync();
            
            Console.WriteLine($"Connected to Ultravox: {_ultravoxNode.WebSocketEndpoint}");
        }
        
        /// <summary>
        /// Start streaming audio
        /// </summary>
        private async Task StartStreamingAsync()
        {
            Console.WriteLine("Starting streaming...");
            
            // Start streaming on all nodes
            await _microphoneNode.StartStreamingAsync();
            await _ultravoxNode.StartStreamingAsync();
            await _speakerNode.StartStreamingAsync();
            
            // Set up pipeline: Microphone -> Ultravox -> Speaker
            _microphoneNode.AudioCaptured += async (sender, e) =>
            {
                await _ultravoxNode.AcceptAudioAsync(e.AudioData, e.Context);
            };
            
            _ultravoxNode.AudioDataReceived += async (sender, e) =>
            {
                await _speakerNode.AcceptAudioAsync(e.AudioData, e.Context);
            };
            
            Console.WriteLine("Streaming started.");
        }
        
        /// <summary>
        /// Stop streaming audio
        /// </summary>
        private async Task StopStreamingAsync()
        {
            if (!_isRunning)
                return;
                
            Console.WriteLine("Stopping streaming...");
            
            // Stop streaming on all nodes
            if (_speakerNode != null && _speakerNode.IsStreaming)
                await _speakerNode.StopStreamingAsync();
                
            if (_ultravoxNode != null && _ultravoxNode.IsStreaming)
                await _ultravoxNode.StopStreamingAsync();
                
            if (_microphoneNode != null && _microphoneNode.IsStreaming)
                await _microphoneNode.StopStreamingAsync();
                
            _isRunning = false;
            
            Console.WriteLine("Streaming stopped.");
        }
        
        /// <summary>
        /// Shutdown all nodes
        /// </summary>
        private async Task ShutdownNodesAsync()
        {
            Console.WriteLine("Shutting down...");
            
            // Shutdown all nodes in reverse order
            if (_speakerNode != null)
            {
                try
                {
                    await _speakerNode.Shutdown();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error shutting down speaker: {ex.Message}");
                }
            }
                
            if (_ultravoxNode != null)
            {
                try
                {
                    await _ultravoxNode.Shutdown();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error shutting down Ultravox: {ex.Message}");
                }
            }
                
            if (_microphoneNode != null)
            {
                try
                {
                    await _microphoneNode.Shutdown();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error shutting down microphone: {ex.Message}");
                }
            }
                
            Console.WriteLine("Shutdown complete.");
        }
    }
}