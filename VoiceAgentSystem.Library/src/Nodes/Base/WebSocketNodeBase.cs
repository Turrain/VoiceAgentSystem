using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Nodes.Base
{
    /// <summary>
    /// Base implementation for WebSocket nodes
    /// </summary>
    public abstract class WebSocketNodeBase : StreamingNodeBase, IWebSocketNode
    {
        /// <summary>
        /// WebSocket endpoint URL
        /// </summary>
        public string WebSocketEndpoint { get; set; }
        
        /// <summary>
        /// Current WebSocket state
        /// </summary>
        public WebSocketState WebSocketState => _webSocket?.State ?? WebSocketState.None;
        
        /// <summary>
        /// Event raised when a message is received
        /// </summary>
        public event EventHandler<WebSocketMessageEventArgs> MessageReceived;
        
        /// <summary>
        /// Event raised when the connection state changes
        /// </summary>
        public event EventHandler<WebSocketStateEventArgs> ConnectionStateChanged;
        
        // WebSocket instance
        protected ClientWebSocket _webSocket;
        
        // Lock for WebSocket operations
        protected readonly SemaphoreSlim _webSocketLock = new SemaphoreSlim(1, 1);
        
        // Buffer size for receiving data
        protected const int ReceiveBufferSize = 32768;
        
        /// <summary>
        /// Create WebSocket node
        /// </summary>
        protected WebSocketNodeBase(string id, string name, string webSocketEndpoint) 
            : base(id, name)
        {
            WebSocketEndpoint = webSocketEndpoint ?? throw new ArgumentNullException(nameof(webSocketEndpoint));
        }
        
        /// <summary>
        /// Initialize the node
        /// </summary>
        public override async Task Initialize()
        {
            await base.Initialize();
            
            // Create WebSocket instance
            _webSocket = new ClientWebSocket();
            
            // Set any custom options
            await ConfigureWebSocketAsync(_webSocket);
        }
        
        /// <summary>
        /// Configure the WebSocket
        /// </summary>
        protected virtual Task ConfigureWebSocketAsync(ClientWebSocket webSocket)
        {
            // Base implementation does nothing
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Connect to the WebSocket endpoint
        /// </summary>
       public virtual async Task ConnectAsync()
{
    await _webSocketLock.WaitAsync();
    try
    {
        // If already connected, return
        if (_webSocket?.State == WebSocketState.Open)
            return;

        // If socket exists but is in a bad state, dispose it
        if (_webSocket != null && _webSocket.State != WebSocketState.None)
        {
            _webSocket.Dispose();
            _webSocket = null;
        }

        // Create new socket if needed
        if (_webSocket == null)
        {
            _webSocket = new ClientWebSocket();
            await ConfigureWebSocketAsync(_webSocket);
        }

        var previousState = _webSocket.State;
        
        await _webSocket.ConnectAsync(new Uri(WebSocketEndpoint), CancellationToken.None);
        
        // Raise state changed event
        var context = new ProcessingContext();
        OnConnectionStateChanged(new WebSocketStateEventArgs(
            previousState, _webSocket.State, context));
    }
    finally
    {
        _webSocketLock.Release();
    }
}
        
        /// <summary>
        /// Disconnect from the WebSocket endpoint
        /// </summary>
        public virtual async Task DisconnectAsync()
        {
            await _webSocketLock.WaitAsync();
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                {
                    var previousState = _webSocket.State;
                    
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, 
                        "Disconnecting", 
                        CancellationToken.None);
                    
                    // Raise state changed event
                    var context = new ProcessingContext();
                    OnConnectionStateChanged(new WebSocketStateEventArgs(
                        previousState, _webSocket.State, context));
                }
            }
            finally
            {
                _webSocketLock.Release();
            }
        }
        
        /// <summary>
        /// Send data over the WebSocket
        /// </summary>
        public virtual async Task SendAsync(byte[] data, WebSocketMessageType messageType, bool endOfMessage)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
                
            await _webSocketLock.WaitAsync();
            try
            {
                if (_webSocket?.State != WebSocketState.Open)
                {
                    throw new InvalidOperationException("WebSocket is not connected");
                }
                
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(data), 
                    messageType, 
                    endOfMessage, 
                    CancellationToken.None);
            }
            finally
            {
                _webSocketLock.Release();
            }
        }
        
        /// <summary>
        /// Send a text message over the WebSocket
        /// </summary>
        public virtual async Task SendTextAsync(string text, bool endOfMessage = true)
        {
            if (string.IsNullOrEmpty(text))
                return;
                
            byte[] data = Encoding.UTF8.GetBytes(text);
            await SendAsync(data, WebSocketMessageType.Text, endOfMessage);
        }
        
        /// <summary>
        /// Called when streaming starts
        /// </summary>
        protected override async Task OnStartStreamingAsync(CancellationToken cancellationToken)
        {
            await base.OnStartStreamingAsync(cancellationToken);
            
            // Connect if not already connected
            if (_webSocket?.State != WebSocketState.Open)
            {
                await ConnectAsync();
            }
            
            // Start listening for messages
            _ = Task.Run(() => ListenForMessagesAsync(cancellationToken), cancellationToken);
        }
        
        /// <summary>
        /// Called when streaming stops
        /// </summary>
        protected override async Task OnStopStreamingAsync()
        {
            await base.OnStopStreamingAsync();
            
            // Disconnect is handled by the listening task
        }
        
        /// <summary>
        /// Listen for WebSocket messages
        /// </summary>
        protected virtual async Task ListenForMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[ReceiveBufferSize];
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && 
                      _webSocket?.State == WebSocketState.Open)
                {
                    var receiveResult = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cancellationToken);
                        
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        // WebSocket is closing
                        await DisconnectAsync();
                        break;
                    }
                    
                    // Create a copy of the received data
                    var data = new byte[receiveResult.Count];
                    Array.Copy(buffer, data, receiveResult.Count);
                    
                    // Create context and raise event
                    var context = new ProcessingContext(null, cancellationToken);
                    OnMessageReceived(new WebSocketMessageEventArgs(
                        data, receiveResult.MessageType, receiveResult.EndOfMessage, context));
                        
                    // Process the message
                    await OnMessageReceivedAsync(
                        data, receiveResult.MessageType, receiveResult.EndOfMessage, context);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled, just exit
            }
            catch (WebSocketException ex)
            {
                // WebSocket error
                Metadata["LastWebSocketError"] = ex.Message;
                
                // Try to disconnect
                try
                {
                    await DisconnectAsync();
                }
                catch
                {
                    // Ignore errors during disconnect
                }
            }
            catch (Exception ex)
            {
                // Other error
                Metadata["LastError"] = ex.Message;
                
                // Try to disconnect
                try
                {
                    await DisconnectAsync();
                }
                catch
                {
                    // Ignore errors during disconnect
                }
            }
        }
        
        /// <summary>
        /// Called when a message is received
        /// </summary>
        protected virtual Task OnMessageReceivedAsync(
            byte[] data, 
            WebSocketMessageType messageType, 
            bool endOfMessage, 
            ProcessingContext context)
        {
            // Base implementation does nothing
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Raise message received event
        /// </summary>
        protected virtual void OnMessageReceived(WebSocketMessageEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }
        
        /// <summary>
        /// Raise connection state changed event
        /// </summary>
        protected virtual void OnConnectionStateChanged(WebSocketStateEventArgs e)
        {
            ConnectionStateChanged?.Invoke(this, e);
        }
        
        /// <summary>
        /// Shutdown the node
        /// </summary>
        public override async Task Shutdown()
        {
            // Stop streaming if active
            if (IsStreaming)
            {
                await StopStreamingAsync();
            }
            
            // Disconnect WebSocket
            await DisconnectAsync();
            
            // Dispose WebSocket
            _webSocket?.Dispose();
            _webSocket = null;
            
            await base.Shutdown();
        }
    }
}