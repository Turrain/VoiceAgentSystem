using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Nodes.Base;

namespace VoiceBotSystem.Nodes.Processors
{
    /// <summary>
    /// Node for processing text data
    /// </summary>
    public class TextProcessingNode : StreamingNodeBase
    {
        /// <summary>
        /// Delegate for text processing functions
        /// </summary>
        public delegate Task<string> TextProcessorDelegate(string text, ProcessingContext context);
        
        /// <summary>
        /// Event raised when text processing is complete
        /// </summary>
        public event EventHandler<TextProcessedEventArgs> TextProcessed;
        
        // Text processors chain
        private readonly List<TextProcessorDelegate> _processors = new List<TextProcessorDelegate>();
        
        /// <summary>
        /// Create text processing node
        /// </summary>
        public TextProcessingNode(string id, string name) : base(id, name)
        {
        }
        
        /// <summary>
        /// Add a text processor to the chain
        /// </summary>
        public void AddProcessor(TextProcessorDelegate processor)
        {
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));
                
            _processors.Add(processor);
        }
        
        /// <summary>
        /// Process text through the processor chain
        /// </summary>
        public async Task<string> ProcessTextAsync(string inputText, ProcessingContext context = null)
        {
            if (!IsEnabled || string.IsNullOrEmpty(inputText))
                return inputText;
                
            context ??= new ProcessingContext();
            context.LogInformation($"Processing text: {inputText}");
            
            string processedText = inputText;
            
            foreach (var processor in _processors)
            {
                processedText = await processor(processedText, context);
            }
            
            context.LogInformation($"Processed text: {processedText}");
            
            // Raise event
            OnTextProcessed(new TextProcessedEventArgs(inputText, processedText, context));
            
            // Raise data processed event for streaming pipeline
            OnDataProcessed(new StreamingDataEventArgs(processedText, context));
            
            return processedText;
        }
        
        /// <summary>
        /// Raise text processed event
        /// </summary>
        protected virtual void OnTextProcessed(TextProcessedEventArgs e)
        {
            TextProcessed?.Invoke(this, e);
            
            // Propagate to output connections
            foreach (var connection in OutputConnections)
            {
                if (connection.IsEnabled)
                {
                    // Pass text through connections
                    _ = connection.TransferDataAsync(e.OutputText, e.Context);
                }
            }
        }
    }
    
    /// <summary>
    /// Event arguments for text processed events
    /// </summary>
    public class TextProcessedEventArgs : EventArgs
    {
        /// <summary>
        /// Input text
        /// </summary>
        public string InputText { get; }
        
        /// <summary>
        /// Output text
        /// </summary>
        public string OutputText { get; }
        
        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }
        
        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }
        
        /// <summary>
        /// Create text processed event args
        /// </summary>
        public TextProcessedEventArgs(string inputText, string outputText, ProcessingContext context)
        {
            InputText = inputText;
            OutputText = outputText;
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
}