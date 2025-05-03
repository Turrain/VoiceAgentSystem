using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.Processors
{
    /// <summary>
    /// WebSocket-based text-to-speech node
    /// </summary>
    public class WebSocketTextToSpeechNode : WebSocketNodeBase
    {
        /// <summary>
        /// Output audio format
        /// </summary>
        public AudioFormat OutputFormat { get; set; }
        
        /// <summary>
        /// Event raised when audio data is received
        /// </summary>
        public event EventHandler<TtsAudioEventArgs> AudioDataReceived;
        
        /// <summary>
        /// Event raised when the speech generation is complete
        /// </summary>
        public event EventHandler<TtsCompletedEventArgs> SpeechCompleted;
        
        // Voice settings
        public string VoiceId { get; set; } = "default";
        public float SpeakingRate { get; set; } = 1.0f;
        public float Pitch { get; set; } = 0.0f;
        
        /// <summary>
        /// Create WebSocket TTS node
        /// </summary>
        public WebSocketTextToSpeechNode(string id, string name, string webSocketEndpoint) 
            : base(id, name, webSocketEndpoint)
        {
            // Set default output format
            OutputFormat = new AudioFormat
            {
                SampleRate = 24000,
                Channels = 1,
                BitsPerSample = 16,
                IsFloat = false
            };
        }
        
        /// <summary>
        /// Generate speech from text
        /// </summary>
        public async Task<bool> SpeakTextAsync(string text, ProcessingContext context = null)
        {
            if (!IsEnabled || string.IsNullOrEmpty(text))
                return false;
                
            context ??= new ProcessingContext();
            
            // Ensure connected
            if (_webSocket?.State != WebSocketState.Open)
            {
                await ConnectAsync();
            }
            
            // Create TTS request
            // var request = new
            // {
            //     text = text,
            //     voice_id = VoiceId,
            //     speaking_rate = SpeakingRate,
            //     pitch = Pitch,
            //     format = new
            //     {
            //         sample_rate = OutputFormat.SampleRate,
            //         channels = OutputFormat.Channels,
            //         bits_per_sample = OutputFormat.BitsPerSample,
            //         is_float = OutputFormat.IsFloat
            //     },
            //     streaming = IsStreaming
            // };
            var request = new
        {
            input = text,
            voice = "default",
            stream = true
        };
            Console.WriteLine($"TTS: REQUEST {text}");
            // Send request
            await SendTextAsync(JsonSerializer.Serialize(request));
            
            return true;
        }
        
        /// <summary>
        /// Called when a message is received from the WebSocket
        /// </summary>
        protected override async Task OnMessageReceivedAsync(
            byte[] data, 
            WebSocketMessageType messageType, 
            bool endOfMessage, 
            ProcessingContext context)
        {
            if (messageType == WebSocketMessageType.Binary)
            {
                // Binary data is audio
                var audioData = new PCMAudioData(data, OutputFormat);
                Console.WriteLine("Data");
                
                // Raise event
                OnAudioDataReceived(new TtsAudioEventArgs(audioData, context));
            }
            else if (messageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(data);
                
                try
                {
                    // Parse JSON message
                    using var jsonDoc = JsonDocument.Parse(message);
                    var root = jsonDoc.RootElement;
                    
                    // Check for audio complete message
                    if (root.TryGetProperty("audio_complete", out var completeElement) &&
                        completeElement.GetBoolean())
                    {
                        // Speech generation is complete
                        OnSpeechCompleted(new TtsCompletedEventArgs(true, context));
                    }
                }
                catch (JsonException ex)
                {
                    context.LogError($"Error parsing TTS response: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Raise audio data received event
        /// </summary>
        protected virtual void OnAudioDataReceived(TtsAudioEventArgs e)
        {
            AudioDataReceived?.Invoke(this, e);
            
            // Also raise data processed event for streaming pipeline
            OnDataProcessed(new StreamingDataEventArgs(e.AudioData, e.Context));
            
            // Propagate to output connections
            foreach (var connection in OutputConnections)
            {
                if (connection.IsEnabled)
                {
                    // Pass audio through connections
                    _ = connection.TransferDataAsync(e.AudioData, e.Context);
                }
            }
        }
        
        /// <summary>
        /// Raise speech completed event
        /// </summary>
        protected virtual void OnSpeechCompleted(TtsCompletedEventArgs e)
        {
            SpeechCompleted?.Invoke(this, e);
        }
    }
    
    /// <summary>
    /// Event arguments for TTS audio events
    /// </summary>
    public class TtsAudioEventArgs : EventArgs
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
        /// Create TTS audio event args
        /// </summary>
        public TtsAudioEventArgs(IAudioData audioData, ProcessingContext context)
        {
            AudioData = audioData;
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
    
    /// <summary>
    /// Event arguments for TTS completed events
    /// </summary>
    public class TtsCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the speech generation was successful
        /// </summary>
        public bool Success { get; }
        
        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }
        
        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }
        
        /// <summary>
        /// Create TTS completed event args
        /// </summary>
        public TtsCompletedEventArgs(bool success, ProcessingContext context)
        {
            Success = success;
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
}