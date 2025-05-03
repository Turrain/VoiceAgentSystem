using System;
using System.Threading.Tasks;

namespace VoiceBotSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for nodes that can handle streaming data
    /// </summary>
    public interface IStreamingNode : INode
    {
        /// <summary>
        /// Whether the node is currently streaming
        /// </summary>
        bool IsStreaming { get; }
        
        /// <summary>
        /// Start streaming mode
        /// </summary>
        Task StartStreamingAsync();
        
        /// <summary>
        /// Stop streaming mode
        /// </summary>
        Task StopStreamingAsync();
        
        /// <summary>
        /// Event raised when streaming starts
        /// </summary>
        event EventHandler<StreamingEventArgs> StreamingStarted;
        
        /// <summary>
        /// Event raised when streaming stops
        /// </summary>
        event EventHandler<StreamingEventArgs> StreamingStopped;
        
        /// <summary>
        /// Event raised when streaming data is processed
        /// </summary>
        event EventHandler<StreamingDataEventArgs> DataProcessed;
    }
    
    /// <summary>
    /// Event arguments for streaming events
    /// </summary>
    public class StreamingEventArgs : EventArgs
    {
        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }
        
        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }
        
        /// <summary>
        /// Create streaming event args
        /// </summary>
        public StreamingEventArgs(ProcessingContext context)
        {
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
    
    /// <summary>
    /// Event arguments for streaming data events
    /// </summary>
    public class StreamingDataEventArgs : StreamingEventArgs
    {
        /// <summary>
        /// Data being processed
        /// </summary>
        public object Data { get; }
        
        /// <summary>
        /// Create streaming data event args
        /// </summary>
        public StreamingDataEventArgs(object data, ProcessingContext context)
            : base(context)
        {
            Data = data;
        }
    }
}