using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Exceptions;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Pipeline
{
    /// <summary>
    /// Implementation of a connection between nodes
    /// </summary>
    public class NodeConnection : INodeConnection
    {
        /// <summary>
        /// Unique identifier for the connection
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Source node of the connection
        /// </summary>
        public INode Source { get; }

        /// <summary>
        /// Target node of the connection
        /// </summary>
        public INode Target { get; }

        /// <summary>
        /// Optional label for the connection
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Whether the connection is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Connection priority (lower numbers have higher priority)
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Connection-specific configuration
        /// </summary>
        public IDictionary<string, object> Configuration { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Event raised when data passes through the connection
        /// </summary>
        public event EventHandler<DataTransferEventArgs> DataTransferred;

        /// <summary>
        /// Connection type (e.g., "audio", "control", "data")
        /// </summary>
        public string ConnectionType { get; set; } = "audio";

        /// <summary>
        /// Creates a new node connection
        /// </summary>
        public NodeConnection(string id, INode source, INode target)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));

            Label = $"{source.Name} → {target.Name}";
        }

        /// <summary>
        /// Validates the connection
        /// </summary>
        public async Task<bool> ValidateAsync()
        {
            // Check if source and target are valid
            if (Source == null || Target == null)
                return false;

            // Ensure source has output capabilities for audio connections
            if (ConnectionType == "audio")
            {
                if (Source is IAudioOutputNode sourceOutput && Target is IAudioInputNode targetInput)
                {
                    // Check format compatibility
                    bool formatCompatible = targetInput.SupportedFormats.Count == 0 ||
                        targetInput.SupportedFormats.Contains(sourceOutput.OutputFormat);

                    if (!formatCompatible)
                    {
                        throw new NodeConnectionException(
                            Source.Id, Target.Id,
                            $"Incompatible formats between {Source.Name} and {Target.Name}");
                    }
                }
                else
                {
                    throw new NodeConnectionException(
                        Source.Id, Target.Id,
                        $"Invalid audio connection: {Source.Name} cannot output audio or {Target.Name} cannot accept audio");
                }
            }

            // Run any additional validation from the nodes
            var sourceValid = await Source.ValidateAsync();
            var targetValid = await Target.ValidateAsync();

            return sourceValid && targetValid;
        }

        /// <summary>
        /// Transfer data through the connection
        /// </summary>
        public async Task<bool> TransferDataAsync(object data, ProcessingContext context)
        {
            if (!IsEnabled)
                return false;

            if (data is IAudioData audioData && Target is IAudioInputNode audioInput)
            {
                var result = await audioInput.AcceptAudioAsync(audioData, context);

                if (result)
                {
                    OnDataTransferred(new DataTransferEventArgs(data, context));
                }

                return result;
            }

            return false;
        }

        /// <summary>
        /// Get configuration value
        /// </summary>
        public T GetConfigurationValue<T>(string key, T defaultValue = default)
        {
            if (Configuration.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;

            return defaultValue;
        }

        /// <summary>
        /// Set configuration value
        /// </summary>
        public void SetConfigurationValue<T>(string key, T value)
        {
            Configuration[key] = value;
        }

        /// <summary>
        /// Raise data transferred event
        /// </summary>
        protected virtual void OnDataTransferred(DataTransferEventArgs e)
        {
            DataTransferred?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for data transfer events
    /// </summary>
    public class DataTransferEventArgs : EventArgs
    {
        /// <summary>
        /// Transferred data
        /// </summary>
        public object Data { get; }

        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }

        /// <summary>
        /// Event timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Create data transfer event args
        /// </summary>
        public DataTransferEventArgs(object data, ProcessingContext context)
        {
            Data = data;
            Context = context;
            Timestamp = DateTimeOffset.Now;
        }
    }
}
