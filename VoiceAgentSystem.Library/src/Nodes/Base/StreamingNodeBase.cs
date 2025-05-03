using System;
using System.Threading;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Nodes.Base
{
    /// <summary>
    /// Base implementation for streaming nodes
    /// </summary>
    public abstract class StreamingNodeBase : NodeBase, IStreamingNode
    {
        /// <summary>
        /// Whether the node is currently streaming
        /// </summary>
        public bool IsStreaming { get; protected set; }
        
        /// <summary>
        /// Cancellation token source for streaming
        /// </summary>
        protected CancellationTokenSource StreamingCts;
        
        /// <summary>
        /// Event raised when streaming starts
        /// </summary>
        public event EventHandler<StreamingEventArgs> StreamingStarted;
        
        /// <summary>
        /// Event raised when streaming stops
        /// </summary>
        public event EventHandler<StreamingEventArgs> StreamingStopped;
        
        /// <summary>
        /// Event raised when streaming data is processed
        /// </summary>
        public event EventHandler<StreamingDataEventArgs> DataProcessed;
        
        /// <summary>
        /// Create streaming node
        /// </summary>
        protected StreamingNodeBase(string id, string name) : base(id, name)
        {
        }
        
        /// <summary>
        /// Start streaming mode
        /// </summary>
        public virtual async Task StartStreamingAsync()
        {
            if (IsStreaming)
                return;
                
            // Create new cancellation token source
            StreamingCts = new CancellationTokenSource();
            
            // Set streaming flag
            IsStreaming = true;
            
            // Start streaming task
            await OnStartStreamingAsync(StreamingCts.Token);
            
            // Raise event
            OnStreamingStarted(new StreamingEventArgs(new ProcessingContext(null, StreamingCts.Token)));
        }
        
        /// <summary>
        /// Stop streaming mode
        /// </summary>
        public virtual async Task StopStreamingAsync()
        {
            if (!IsStreaming)
                return;
                
            // Cancel streaming
            StreamingCts?.Cancel();
            
            // Set streaming flag
            IsStreaming = false;
            
            // Stop streaming task
            await OnStopStreamingAsync();
            
            // Dispose token source
            StreamingCts?.Dispose();
            StreamingCts = null;
            
            // Raise event
            OnStreamingStopped(new StreamingEventArgs(new ProcessingContext()));
        }
        
        /// <summary>
        /// Called when streaming starts
        /// </summary>
        protected virtual Task OnStartStreamingAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Called when streaming stops
        /// </summary>
        protected virtual Task OnStopStreamingAsync()
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Raise streaming started event
        /// </summary>
        protected virtual void OnStreamingStarted(StreamingEventArgs e)
        {
            StreamingStarted?.Invoke(this, e);
        }
        
        /// <summary>
        /// Raise streaming stopped event
        /// </summary>
        protected virtual void OnStreamingStopped(StreamingEventArgs e)
        {
            StreamingStopped?.Invoke(this, e);
        }
        
        /// <summary>
        /// Raise data processed event
        /// </summary>
        protected virtual void OnDataProcessed(StreamingDataEventArgs e)
        {
            DataProcessed?.Invoke(this, e);
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
            
            await base.Shutdown();
        }
    }
}