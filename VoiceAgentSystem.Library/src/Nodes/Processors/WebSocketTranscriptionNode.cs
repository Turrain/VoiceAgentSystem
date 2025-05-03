using System;
using System.Collections.Generic;
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
    /// WebSocket-based speech transcription node
    /// </summary>
    public class WebSocketTranscriptionNode : WebSocketNodeBase, IAudioProcessorNode
    {
        /// <summary>
        /// Supported audio formats
        /// </summary>
        public IList<AudioFormat> SupportedFormats { get; } = new List<AudioFormat>();
        
        /// <summary>
        /// Output audio format
        /// </summary>
        public AudioFormat OutputFormat { get;  set; }
        
        /// <summary>
        /// Event raised when transcription is received
        /// </summary>
        public event EventHandler<TranscriptionEventArgs> TranscriptionReceived;
        
        // Audio processing queue
        private Queue<byte[]> _audioChunks = new Queue<byte[]>();
        
        // Maximum chunk size for streaming audio
        private const int MaxChunkSize = 8192;
        
        /// <summary>
        /// Create WebSocket transcription node
        /// </summary>
        public WebSocketTranscriptionNode(string id, string name, string webSocketEndpoint) 
            : base(id, name, webSocketEndpoint)
        {
            // Add default format
            OutputFormat = AudioFormat.Default;
            SupportedFormats.Add(AudioFormat.Default);
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
            
            // If streaming mode, queue the audio for processing
            if (IsStreaming)
            {
                // Split into chunks for streaming
                SplitIntoChunks(audioData.RawData);
                
                // Send chunks
                await SendAudioChunksAsync(context);
                return true;
            }
            else
            {
                // Non-streaming mode, process as a single chunk
                return await ProcessAudioAsync(audioData, context) != null;
            }
        }
        
        /// <summary>
        /// Process audio data for transcription
        /// </summary>
        public async Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context)
        {
            if (!IsEnabled || input == null)
                return null;
                
            // Ensure connected
            if (_webSocket?.State != WebSocketState.Open)
            {
                await ConnectAsync();
            }
            
            // Create a transcription request
            var formatMessage = new
            {
                format = new
                {
                    sample_rate = input.Format.SampleRate,
                    channels = input.Format.Channels,
                    bits_per_sample = input.Format.BitsPerSample,
                    is_float = input.Format.IsFloat
                },
                streaming = false
            };
            
            // Send format message
         //   await SendTextAsync(JsonSerializer.Serialize(formatMessage));
            
            // Send audio data
            await SendAsync(input.RawData, WebSocketMessageType.Binary, true);
            
            // Send end of stream marker
         //   await SendTextAsync(JsonSerializer.Serialize(new { eos = true }));
            
            // The result will be handled asynchronously via the TranscriptionReceived event
            
            // Return the input audio
            return input;
        }
        
        /// <summary>
        /// Get audio output from this node
        /// </summary>
        public Task<IAudioData> GetAudioOutputAsync(ProcessingContext context)
        {
            // This node doesn't produce audio output, it produces transcriptions
            return Task.FromResult<IAudioData>(null);
        }
        
        /// <summary>
        /// Process audio chunks for streaming transcription
        /// </summary>
        public async Task ProcessAudioChunkAsync(byte[] audioChunk, ProcessingContext context = null)
        {
            context ??= new ProcessingContext();
            
            // Queue the chunk
            lock (_audioChunks)
            {
                _audioChunks.Enqueue(audioChunk);
            }
            
            // Send chunks
            await SendAudioChunksAsync(context);
        }
        
        /// <summary>
        /// Split audio data into chunks for streaming
        /// </summary>
        private void SplitIntoChunks(byte[] audioData)
        {
            lock (_audioChunks)
            {
                _audioChunks.Clear();
                
                int offset = 0;
                while (offset < audioData.Length)
                {
                    int chunkSize = Math.Min(MaxChunkSize, audioData.Length - offset);
                    byte[] chunk = new byte[chunkSize];
                    Buffer.BlockCopy(audioData, offset, chunk, 0, chunkSize);
                    
                    _audioChunks.Enqueue(chunk);
                    offset += chunkSize;
                }
            }
        }
        
        /// <summary>
        /// Send queued audio chunks
        /// </summary>
        private async Task SendAudioChunksAsync(ProcessingContext context)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                await ConnectAsync();
            }
            
            // Send audio format info if this is the first chunk
            // if (!Metadata.ContainsKey("FormatSent") || !(bool)Metadata["FormatSent"])
            // {
            //     var formatMessage = new
            //     {
            //         format = new
            //         {
            //             sample_rate = OutputFormat.SampleRate,
            //             channels = OutputFormat.Channels,
            //             bits_per_sample = OutputFormat.BitsPerSample,
            //             is_float = OutputFormat.IsFloat
            //         },
            //         streaming = true
            //     };
                
            //     await SendTextAsync(JsonSerializer.Serialize(formatMessage));
            //     Metadata["FormatSent"] = true;
            // }
            
            // Send queued audio chunks
            List<byte[]> chunksToSend;
            lock (_audioChunks)
            {
                chunksToSend = new List<byte[]>(_audioChunks);
                _audioChunks.Clear();
            }
            
            foreach (var chunk in chunksToSend)
            {
                await SendAsync(chunk, WebSocketMessageType.Binary, true);
            }
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
            if (messageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(data);
                
                try
                {
                    // Parse JSON response (simplified for example)
                    using var jsonDoc = JsonDocument.Parse(message);
                    var root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("text", out var textElement))
                    {
                        string text = textElement.GetString();
                        bool isFinal = false;
                        
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
                    context.LogError($"Error parsing transcription response: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Called when streaming starts
        /// </summary>
        protected override async Task OnStartStreamingAsync(CancellationToken cancellationToken)
        {
            await base.OnStartStreamingAsync(cancellationToken);
            
            // Clear format sent flag to ensure we send format on next chunk
            Metadata["FormatSent"] = false;
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
    }
    
    /// <summary>
    /// Event arguments for transcription events
    /// </summary>
    public class TranscriptionEventArgs : EventArgs
    {
        /// <summary>
        /// Transcribed text
        /// </summary>
        public string Text { get; }
        
        /// <summary>
        /// Whether this is a final transcription
        /// </summary>
        public bool IsFinal { get; }
        
        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }
        
        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }
        
        /// <summary>
        /// Create transcription event args
        /// </summary>
        public TranscriptionEventArgs(string text, bool isFinal, ProcessingContext context)
        {
            Text = text;
            IsFinal = isFinal;
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
}