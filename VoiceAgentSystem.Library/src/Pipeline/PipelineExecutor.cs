using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Pipeline
{
    /// <summary>
    /// Executor for running pipelines with additional features
    /// </summary>
    public class PipelineExecutor
    {
        private readonly Pipeline _pipeline;
        private readonly CancellationTokenSource _cancellationSource;
        private bool _isRunning = false;

        /// <summary>
        /// Event raised when execution starts
        /// </summary>
        public event EventHandler<PipelineExecutionEventArgs> ExecutionStarted;

        /// <summary>
        /// Event raised when execution finishes
        /// </summary>
        public event EventHandler<PipelineExecutionEventArgs> ExecutionFinished;

        /// <summary>
        /// Event raised when execution is cancelled
        /// </summary>
        public event EventHandler<PipelineExecutionEventArgs> ExecutionCancelled;

        /// <summary>
        /// Event raised when an error occurs during execution
        /// </summary>
        public event EventHandler<PipelineErrorEventArgs> ExecutionError;

        /// <summary>
        /// Create a pipeline executor
        /// </summary>
        public PipelineExecutor(Pipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _cancellationSource = new CancellationTokenSource();

            // Forward pipeline events
            _pipeline.ExecutionStarted += (sender, args) => ExecutionStarted?.Invoke(this, args);
            _pipeline.ExecutionCompleted += (sender, args) => ExecutionFinished?.Invoke(this, args);
        }

        /// <summary>
        /// Execute the pipeline with the given input
        /// </summary>
        public async Task<IList<IAudioData>> ExecuteAsync(IAudioData input, ProcessingContext context = null)
        {
            if (_isRunning)
                throw new InvalidOperationException("Pipeline is already running");

            _isRunning = true;

            try
            {
                // Create context if needed
                context ??= new ProcessingContext(cancellationToken: _cancellationSource.Token);

                // Execute pipeline
                var results = await _pipeline.ExecuteAsync(input, context);

                return results;
            }
            catch (OperationCanceledException)
            {
                // Execution was cancelled
                var args = new PipelineExecutionEventArgs(Guid.NewGuid(), context, DateTimeOffset.Now);
                ExecutionCancelled?.Invoke(this, args);

                return new List<IAudioData>();
            }
            catch (Exception ex)
            {
                // Execution failed with an error
                var args = new PipelineErrorEventArgs(Guid.NewGuid(), context, ex);
                ExecutionError?.Invoke(this, args);

                return new List<IAudioData>();
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Cancel the current execution
        /// </summary>
        public void Cancel()
        {
            if (_isRunning)
            {
                _cancellationSource.Cancel();
            }
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            _cancellationSource.Dispose();
        }
    }

    /// <summary>
    /// Event arguments for pipeline errors
    /// </summary>
    public class PipelineErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Execution ID
        /// </summary>
        public Guid ExecutionId { get; }

        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }

        /// <summary>
        /// The error that occurred
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Create pipeline error event args
        /// </summary>
        public PipelineErrorEventArgs(Guid executionId, ProcessingContext context, Exception error)
        {
            ExecutionId = executionId;
            Context = context;
            Error = error;
            Timestamp = DateTimeOffset.Now;
        }
    }
}
