using System;
using System.Threading;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.IO;
using VoiceBotSystem.Nodes.Processors;

namespace VoiceBotSystem
{
    /// <summary>
    /// Streaming voice bot system that uses WebSocket-based services
    /// </summary>
    public class StreamingVoiceBotSystem : IDisposable
    {
        // Core components
        private RawPcmInputNode _inputNode;
        private WebSocketTranscriptionNode _transcriptionNode;
        private TextProcessingNode _textProcessingNode;
        private WebSocketTextToSpeechNode _ttsNode;

        // Configuration
        private readonly string _whisperEndpoint;
        private readonly string _ttsEndpoint;
        private readonly AudioFormat _inputFormat;

        // State
        private bool _isInitialized = false;
        private bool _isStreaming = false;
        private CancellationTokenSource _streamingCts;

        // Events
        public event EventHandler<TranscriptionEventArgs> TranscriptionReceived;
        public event EventHandler<TextProcessedEventArgs> ResponseGenerated;
        public event EventHandler<TtsAudioEventArgs> AudioChunkReceived;
        public event EventHandler<TtsCompletedEventArgs> SpeechCompleted;

        /// <summary>
        /// Create streaming voice bot system
        /// </summary>
        public StreamingVoiceBotSystem(
            string whisperEndpoint,
            string ttsEndpoint,
            AudioFormat inputFormat = null)
        {
            _whisperEndpoint = whisperEndpoint ?? throw new ArgumentNullException(nameof(whisperEndpoint));
            _ttsEndpoint = ttsEndpoint ?? throw new ArgumentNullException(nameof(ttsEndpoint));
            _inputFormat = inputFormat ?? AudioFormat.Default;
        }

        /// <summary>
        /// Initialize the system
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            // Create nodes
            _inputNode = new RawPcmInputNode("input", "Audio Input");
            _transcriptionNode = new WebSocketTranscriptionNode("transcription", "Whisper Transcription", _whisperEndpoint);
            _textProcessingNode = new TextProcessingNode("text_processor", "Text Processor");
            _ttsNode = new WebSocketTextToSpeechNode("tts", "Text to Speech", _ttsEndpoint);

            // Configure nodes 
            _transcriptionNode.OutputFormat = _inputFormat;

            // Add default text processor
            _textProcessingNode.AddProcessor(async (text, context) =>
            {
                // Simple echo processor - replace with your actual logic
                return $"You said: {text}. How can I help you?";
            });

            // Connect events
            _transcriptionNode.TranscriptionReceived += async (sender, e) =>
            {
                // Forward event
                TranscriptionReceived?.Invoke(this, e);

                // Only process final transcriptions
                // if (e.IsFinal)
                // {
                    await _textProcessingNode.ProcessTextAsync(e.Text, e.Context);
                //}
            };

            _textProcessingNode.TextProcessed += async (sender, e) =>
            {
                // Forward event
                ResponseGenerated?.Invoke(this, e);

                // Generate speech
                await _ttsNode.SpeakTextAsync(e.OutputText, e.Context);
            };

            _ttsNode.AudioDataReceived += (sender, e) =>
            {
                // Forward event
                
                Console.WriteLine($"Audio chunk received: {e.AudioData:F2}s");
                AudioChunkReceived?.Invoke(this, e);
            };

            _ttsNode.SpeechCompleted += (sender, e) =>
            {
                // Forward event
                SpeechCompleted?.Invoke(this, e);
            };

            // Initialize nodes
            await _inputNode.Initialize();
            await _transcriptionNode.Initialize();
            await _textProcessingNode.Initialize();
            await _ttsNode.Initialize();

            _isInitialized = true;
        }

        /// <summary>
        /// Start streaming mode
        /// </summary>
        public async Task StartStreamingAsync()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Voice bot not initialized");

            if (_isStreaming)
                return;

            _streamingCts = new CancellationTokenSource();
            _isStreaming = true;

            // Start streaming mode for WebSocket nodes
            await _transcriptionNode.StartStreamingAsync();
            await _ttsNode.StartStreamingAsync();
        }

        /// <summary>
        /// Stop streaming mode
        /// </summary>
        public async Task StopStreamingAsync()
        {
            if (!_isStreaming)
                return;

            _streamingCts?.Cancel();
            _isStreaming = false;

            // Stop streaming mode for WebSocket nodes
            await _transcriptionNode.StopStreamingAsync();
            await _ttsNode.StopStreamingAsync();
        }

        /// <summary>
        /// Process an audio chunk
        /// </summary>
        public async Task ProcessAudioChunkAsync(byte[] audioChunk)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Voice bot not initialized");

            if (!_isStreaming)
                throw new InvalidOperationException("Voice bot is not in streaming mode");

            try
            {
                // Create context
                var context = new ProcessingContext(
                    null,
                    _isStreaming ? _streamingCts.Token : CancellationToken.None);

                // Create audio data
                var audioData = new PCMAudioData(audioChunk, _inputFormat);

                // Process through transcription
                await _transcriptionNode.AcceptAudioAsync(audioData, context);
            }
            catch (Exception ex)
            {
                // Log or handle the error
                Console.WriteLine($"Error processing audio chunk: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Add a text processor
        /// </summary>
        public void AddTextProcessor(TextProcessingNode.TextProcessorDelegate processor)
        {
            _textProcessingNode.AddProcessor(processor);
        }

        /// <summary>
        /// Set TTS voice settings
        /// </summary>
        public void SetTtsVoice(string voiceId, float speakingRate = 1.0f, float pitch = 0.0f)
        {
            _ttsNode.VoiceId = voiceId;
            _ttsNode.SpeakingRate = speakingRate;
            _ttsNode.Pitch = pitch;
        }

        /// <summary>
        /// Shutdown the system
        /// </summary>
        public async Task ShutdownAsync()
        {
            if (_isStreaming)
            {
                await StopStreamingAsync();
            }

            if (_isInitialized)
            {
                await _inputNode.Shutdown();
                await _transcriptionNode.Shutdown();
                await _textProcessingNode.Shutdown();
                await _ttsNode.Shutdown();

                _isInitialized = false;
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _streamingCts?.Cancel();
            _streamingCts?.Dispose();

            // Shutdown async
            _ = ShutdownAsync();
        }
    }
}