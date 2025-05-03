using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.Processors
{
    /// <summary>
    /// Node for Ultravox AI service that handles both STT and TTS in a single WebSocket connection.
    /// </summary>
    public class UltravoxWebSocketNode : WebSocketNodeBase, IAudioProcessorNode
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private string _joinUrl;
        
        // Configuration properties
        public string Voice { get; }
        public string Model { get; }
        public int SampleRate { get; }
        public string SystemPrompt { get; }
        
        /// <summary>
        /// Supported audio formats
        /// </summary>
        public IList<AudioFormat> SupportedFormats { get; } = new List<AudioFormat>();
        
        /// <summary>
        /// Output audio format
        /// </summary>
        public AudioFormat OutputFormat { get; protected set; }
        
        /// <summary>
        /// Event raised when transcription is received
        /// </summary>
        public event EventHandler<TranscriptionEventArgs> TranscriptionReceived;
        
        /// <summary>
        /// Event raised when audio data is received
        /// </summary>
        public event EventHandler<TtsAudioEventArgs> AudioDataReceived;
        
        /// <summary>
        /// Create Ultravox WebSocket node
        /// </summary>
        /// <param name="id">Node ID</param>
        /// <param name="name">Node name</param>
        /// <param name="apiKey">Ultravox API key</param>
        /// <param name="voice">Voice to use (default: "Mark")</param>
        /// <param name="model">Model to use (default: "fixie-ai/ultravox")</param>
        /// <param name="sampleRate">Audio sample rate (default: 8000)</param>
        /// <param name="systemPrompt">System prompt (default: helpful assistant)</param>
        public UltravoxWebSocketNode(
            string id, 
            string name, 
            string apiKey, 
            string voice = "Mark", 
            string model = "fixie-ai/ultravox", 
            int sampleRate = 8000, 
            string systemPrompt = "You are a helpful assistant answering phone calls."
        ) : base(id, name, "wss://ultravox.ai")
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            
            // Store configuration
            Voice = voice;
            Model = model;
            SampleRate = sampleRate;
            SystemPrompt = systemPrompt;
            
            // Set up audio format
            OutputFormat = new AudioFormat
            {
                SampleRate = sampleRate,
                Channels = 1,
                BitsPerSample = 16,
                IsFloat = false
            };
            
            SupportedFormats.Add(OutputFormat);
            
            // Initialize HTTP client
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }
        
        /// <summary>
        /// Initialize the node
        /// </summary>
        public override async Task Initialize()
        {
            await base.Initialize();
            
            // The base Initialize method creates a WebSocket, but we need to get a join URL first,
            // so we'll dispose the default one and create our own after getting the URL
            _webSocket?.Dispose();
            _webSocket = null;
        }
        
        /// <summary>
        /// Get a join URL from the Ultravox API
        /// </summary>
        public async Task<string> GetJoinUrlAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(_joinUrl))
                return _joinUrl;

            var payload = new Dictionary<string, object>
            {
                ["systemPrompt"] = SystemPrompt,
                ["model"] = Model,
                ["voice"] = Voice,
                ["medium"] = new Dictionary<string, object>
                {
                    ["serverWebSocket"] = new Dictionary<string, int>
                    {
                        ["inputSampleRate"] = SampleRate,
                        ["outputSampleRate"] = SampleRate
                    }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("https://api.ultravox.ai/api/calls", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to get Ultravox join URL: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("joinUrl", out var joinUrlElement))
            {
                _joinUrl = joinUrlElement.GetString();
                Metadata["JoinUrl"] = _joinUrl;
                return _joinUrl;
            }

            throw new InvalidOperationException("Ultravox API response did not contain joinUrl");
        }
        
        /// <summary>
        /// Connect to Ultravox using the join URL
        /// </summary>
        public async Task ConnectToUltravoxAsync(CancellationToken cancellationToken = default)
        {
            string joinUrl = await GetJoinUrlAsync(cancellationToken);
            
            // Update WebSocket endpoint and URI
            WebSocketEndpoint = joinUrl;
            await ConnectAsync();
        }
        
        /// <summary>
        /// Connect to the WebSocket endpoint
        /// </summary>
        public override async Task ConnectAsync()
        {
            if (string.IsNullOrEmpty(_joinUrl))
            {
                // Get join URL first
                await GetJoinUrlAsync();
            }
            
            await base.ConnectAsync();
        }
        
        /// <summary>
        /// Accept audio data for processing
        /// </summary>
        public async Task<bool> AcceptAudioAsync(IAudioData audioData, ProcessingContext context)
        {
            if (!IsEnabled || audioData == null)
                return false;
                
            // Check format compatibility
            if (!IsFormatSupported(audioData.Format))
            {
                context?.LogWarning($"Unsupported audio format: {audioData.Format}");
                return false;
            }
            
            // Ensure connected
            if (_webSocket?.State != WebSocketState.Open)
            {
                await ConnectToUltravoxAsync();
            }
            
            // Send audio to Ultravox
            await SendAudioAsync(audioData.RawData, context.CancellationToken);
            return true;
        }
        
        /// <summary>
        /// Process audio data
        /// </summary>
        public async Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context)
        {
            if (!IsEnabled || input == null)
                return null;
                
            // Just forward to AcceptAudioAsync
            await AcceptAudioAsync(input, context);
            
            // Return the same audio for chaining
            return input;
        }
        
        /// <summary>
        /// Get audio output from the node
        /// </summary>
        public Task<IAudioData> GetAudioOutputAsync(ProcessingContext context)
        {
            // This node generates audio via events, not direct requests
            return Task.FromResult<IAudioData>(null);
        }
        
        /// <summary>
        /// Send audio data to Ultravox
        /// </summary>
        public Task SendAudioAsync(byte[] pcmAudioData, CancellationToken cancellationToken = default)
        {
            return SendAsync(pcmAudioData, WebSocketMessageType.Binary, true);
        }
        
        /// <summary>
        /// Send text to Ultravox for speech synthesis
        /// </summary>
        public async Task SpeakTextAsync(string text, ProcessingContext context = null)
        {
            if (!IsEnabled || string.IsNullOrEmpty(text))
                return;
                
            context ??= new ProcessingContext();
            
            // Ensure connected
            if (_webSocket?.State != WebSocketState.Open)
            {
                await ConnectToUltravoxAsync(context.CancellationToken);
            }
            
            // For Ultravox, we need to send a JSON command with the text
            var command = new { text = text };
            var json = JsonSerializer.Serialize(command);
            
            await SendTextAsync(json);
        }
        
        /// <summary>
        /// Called when streaming starts
        /// </summary>
        protected override async Task OnStartStreamingAsync(CancellationToken cancellationToken)
        {
            await base.OnStartStreamingAsync(cancellationToken);
            
            // Connect to Ultravox if not already connected
            if (_webSocket?.State != WebSocketState.Open)
            {
                await ConnectToUltravoxAsync(cancellationToken);
            }
        }
        
        /// <summary>
        /// Called when a message is received
        /// </summary>
        protected override async Task OnMessageReceivedAsync(
            byte[] data, 
            WebSocketMessageType messageType, 
            bool endOfMessage, 
            ProcessingContext context)
        {
            if (messageType == WebSocketMessageType.Text)
            {
                // Text messages are JSON from Ultravox (speech recognition results)
                string message = Encoding.UTF8.GetString(data);
                
                try
                {
                    using var jsonDoc = JsonDocument.Parse(message);
                    var root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("text", out var textElement) && 
                        !string.IsNullOrWhiteSpace(textElement.GetString()))
                    {
                        string text = textElement.GetString();
                        bool isFinal = true; // Assume final for now
                        
                        if (root.TryGetProperty("is_final", out var isFinalElement))
                        {
                            isFinal = isFinalElement.GetBoolean();
                        }
                        
                        // Raise transcription event
                        OnTranscriptionReceived(new TranscriptionEventArgs(text, isFinal, context));
                    }
                }
                catch (JsonException ex)
                {
                    context.LogError($"Error parsing Ultravox response: {ex.Message}");
                }
            }
            else if (messageType == WebSocketMessageType.Binary)
            {
                // Binary messages are audio from Ultravox (text-to-speech results)
                var audioData = new PCMAudioData(data, OutputFormat);
                
                // Raise audio data event
                OnAudioDataReceived(new TtsAudioEventArgs(audioData, context));
            }
        }
        
        /// <summary>
        /// Raise transcription received event
        /// </summary>
        protected virtual void OnTranscriptionReceived(TranscriptionEventArgs e)
        {
            TranscriptionReceived?.Invoke(this, e);
            
            // Also raise data processed event for streaming pipeline
            OnDataProcessed(new StreamingDataEventArgs(e.Text, e.Context));
        }
        
        /// <summary>
        /// Raise audio data received event
        /// </summary>
        protected virtual void OnAudioDataReceived(TtsAudioEventArgs e)
        {
            AudioDataReceived?.Invoke(this, e);
            
            // Also raise data processed event for streaming pipeline
            OnDataProcessed(new StreamingDataEventArgs(e.AudioData, e.Context));
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
        /// Shutdown the node
        /// </summary>
        public override async Task Shutdown()
        {
            await base.Shutdown();
            
            _httpClient?.Dispose();
        }
    }
}