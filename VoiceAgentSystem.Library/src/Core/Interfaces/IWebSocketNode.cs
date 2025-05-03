using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace VoiceBotSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for nodes that can communicate via WebSockets
    /// </summary>
    public interface IWebSocketNode : IStreamingNode
    {
        /// <summary>
        /// WebSocket endpoint URL
        /// </summary>
        string WebSocketEndpoint { get; set; }
        
        /// <summary>
        /// Current WebSocket state
        /// </summary>
        WebSocketState WebSocketState { get; }
        
        /// <summary>
        /// Connect to the WebSocket endpoint
        /// </summary>
        Task ConnectAsync();
        
        /// <summary>
        /// Disconnect from the WebSocket endpoint
        /// </summary>
        Task DisconnectAsync();
        
        /// <summary>
        /// Send data over the WebSocket
        /// </summary>
        Task SendAsync(byte[] data, WebSocketMessageType messageType, bool endOfMessage);
        
        /// <summary>
        /// Event raised when a message is received
        /// </summary>
        event EventHandler<WebSocketMessageEventArgs> MessageReceived;
        
        /// <summary>
        /// Event raised when the connection state changes
        /// </summary>
        event EventHandler<WebSocketStateEventArgs> ConnectionStateChanged;
    }
    
    /// <summary>
    /// Event arguments for WebSocket message events
    /// </summary>
    public class WebSocketMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Message data
        /// </summary>
        public byte[] Data { get; }
        
        /// <summary>
        /// Message type
        /// </summary>
        public WebSocketMessageType MessageType { get; }
        
        /// <summary>
        /// Whether this is the end of the message
        /// </summary>
        public bool EndOfMessage { get; }
        
        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }
        
        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }
        
        /// <summary>
        /// Create WebSocket message event args
        /// </summary>
        public WebSocketMessageEventArgs(
            byte[] data, 
            WebSocketMessageType messageType, 
            bool endOfMessage, 
            ProcessingContext context)
        {
            Data = data;
            MessageType = messageType;
            EndOfMessage = endOfMessage;
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
    
    /// <summary>
    /// Event arguments for WebSocket state events
    /// </summary>
    public class WebSocketStateEventArgs : EventArgs
    {
        /// <summary>
        /// Previous WebSocket state
        /// </summary>
        public WebSocketState PreviousState { get; }
        
        /// <summary>
        /// Current WebSocket state
        /// </summary>
        public WebSocketState CurrentState { get; }
        
        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }
        
        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }
        
        /// <summary>
        /// Create WebSocket state event args
        /// </summary>
        public WebSocketStateEventArgs(
            WebSocketState previousState, 
            WebSocketState currentState, 
            ProcessingContext context)
        {
            PreviousState = previousState;
            CurrentState = currentState;
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
}