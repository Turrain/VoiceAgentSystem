// Example showing how to use the streaming voice bot system


using VoiceBotSystem;
using VoiceBotSystem.Core;
using VoiceBotSystem.Examples;

public class StreamingExample
{
    public static async Task RunExample()
    {
        // Create input audio format
        var inputFormat = new AudioFormat
        {
            SampleRate = 8000,
            Channels = 1,
            BitsPerSample = 16,
            IsFloat = false
        };
        
        // Create streaming voice bot
        using var voiceBot = new StreamingVoiceBotSystem(
            "ws://37.151.89.206:8765",
            "ws://37.151.89.206:8766",
            inputFormat);
            
        // Set up event handlers
        voiceBot.TranscriptionReceived += (sender, e) =>
        {
            Console.WriteLine($"Transcription: {e.Text} (Final: {e.IsFinal})");
        };
        
        voiceBot.ResponseGenerated += (sender, e) =>
        {
            Console.WriteLine($"Response: {e.OutputText}");
        };
        
        voiceBot.AudioChunkReceived += (sender, e) =>
        {
            Console.WriteLine($"Audio chunk received: {e.AudioData.DurationInSeconds:F2}s");
            // Here you would send the audio to your output device
            PlayAudio(e.AudioData.RawData);
        };
        
        // Initialize the system
        await voiceBot.InitializeAsync();
        
        // Add custom text processors
        voiceBot.AddTextProcessor(async (text, context) =>
        {
            // Look for specific commands
            if (text.Contains("time"))
            {
                return $"The current time is {DateTime.Now.ToShortTimeString()}";
            }
            else if (text.Contains("weather"))
            {
                return "I'm sorry, I don't have weather information yet.";
            }
            
            // Default response
            return $"You said: {text}. How can I help?";
        });
        
        // Set voice parameters
        voiceBot.SetTtsVoice("default", 1.0f, 0.0f);
        
        // Start streaming mode
        await voiceBot.StartStreamingAsync();
        
        // Simulate microphone input
        Console.WriteLine("Simulating microphone input...");
        for (int i = 0; i < 10; i++)
        {
            // Get audio from microphone (simulated)
            byte[] audioChunk = new byte[3200]; // 100ms of 16kHz audio
            
            // Process the chunk
            await voiceBot.ProcessAudioChunkAsync(audioChunk);
            
            // Wait a bit
            await Task.Delay(100);
        }
           await Task.Delay(20000);
        // Stop streaming and clean up
        await voiceBot.StopStreamingAsync();
        await voiceBot.ShutdownAsync();
    }
    
    private static void PlayAudio(byte[] audioData)
    {
        // In a real implementation, this would play audio through speakers
        Console.WriteLine($"Playing {audioData.Length} bytes of audio...");
    }

    
    static async Task Main(string[] args)
    {
         Console.WriteLine("Ultravox Voice Bot Example");
            Console.WriteLine("=========================");
            
        
            
            // Run the example
            var example = new UltravoxLinuxExample();
            await example.RunExampleAsync("vTfUqMvu.zItOnkw9oQke0xOzwLSsNRYM4jU3xXxp");
     
    }
}