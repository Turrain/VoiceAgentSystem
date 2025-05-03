using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoiceBotSystem.Core;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Modules;
using VoiceBotSystem.Pipeline;

namespace VoiceBotSystem.Agent
{
    /// <summary>
    /// Agent for processing voice audio through pipelines
    /// </summary>
    public class VoiceBotAgent
    {
        /// <summary>
        /// Agent ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Agent name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Agent description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Agent configuration
        /// </summary>
        public AgentConfiguration Configuration { get; }

        // Pipelines by ID
        private readonly Dictionary<string, Pipeline.Pipeline> _pipelines = new Dictionary<string, Pipeline.Pipeline>();

        // Module registry
        private readonly ModuleRegistry _moduleRegistry;

        // Template registry
        private readonly TemplateRegistry _templateRegistry;

        // Default pipeline
        private Pipeline.Pipeline _defaultPipeline;

        // Execution lock
        private readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1);

        // State
        private bool _isInitialized = false;
        private bool _isShutdown = false;

        /// <summary>
        /// Event raised when processing starts
        /// </summary>
        public event EventHandler<ProcessingEventArgs> ProcessingStarted;

        /// <summary>
        /// Event raised when processing completes
        /// </summary>
        public event EventHandler<ProcessingEventArgs> ProcessingCompleted;

        /// <summary>
        /// Create a new voice bot agent
        /// </summary>
        public VoiceBotAgent(
            string id,
            string name,
            ModuleRegistry moduleRegistry,
            TemplateRegistry templateRegistry = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _moduleRegistry = moduleRegistry ?? throw new ArgumentNullException(nameof(moduleRegistry));
            _templateRegistry = templateRegistry;

            Configuration = new AgentConfiguration { Id = id, Name = name };
        }

        /// <summary>
        /// Initialize the agent
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            await _processingLock.WaitAsync();
            try
            {
                // Load required modules
                foreach (var moduleId in Configuration.EnabledModules)
                {
                    await _moduleRegistry.LoadModuleAsync(moduleId);
                }

                // Set up pipelines
                foreach (var pipelineConfig in Configuration.Pipelines)
                {
                    var pipeline = await CreatePipelineFromConfigAsync(pipelineConfig);
                    _pipelines[pipelineConfig.Id] = pipeline;

                    if (pipelineConfig.IsDefault)
                    {
                        _defaultPipeline = pipeline;
                    }
                }

                // Ensure there's a default pipeline
                if (_defaultPipeline == null && _pipelines.Count > 0)
                {
                    _defaultPipeline = _pipelines.Values.First();
                }

                _isInitialized = true;
            }
            finally
            {
                _processingLock.Release();
            }
        }

        /// <summary>
        /// Shutdown the agent
        /// </summary>
        public async Task ShutdownAsync()
        {
            if (_isShutdown)
                return;

            await _processingLock.WaitAsync();
            try
            {
                // Shutdown all pipelines
                foreach (var pipeline in _pipelines.Values)
                {
                    await pipeline.ShutdownAsync();
                }

                _isShutdown = true;
            }
            finally
            {
                _processingLock.Release();
            }
        }

        /// <summary>
        /// Process audio with the default pipeline
        /// </summary>
        public async Task<IAudioData> ProcessAudioAsync(IAudioData input, ProcessingContext context = null)
        {
            if (_defaultPipeline == null)
                throw new InvalidOperationException("No default pipeline available");

            return await ProcessAudioWithPipelineAsync(_defaultPipeline.Id, input, context);
        }

        /// <summary>
        /// Process audio with a specific pipeline
        /// </summary>
        public async Task<IAudioData> ProcessAudioWithPipelineAsync(
            string pipelineId,
            IAudioData input,
            ProcessingContext context = null)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Agent not initialized");

            if (_isShutdown)
                throw new InvalidOperationException("Agent has been shut down");

            if (!_pipelines.TryGetValue(pipelineId, out var pipeline))
                throw new ArgumentException($"Pipeline not found: {pipelineId}");

            context ??= new ProcessingContext();

            await _processingLock.WaitAsync();
            try
            {
                // Raise event
                var startArgs = new ProcessingEventArgs(context, input, pipeline);
                OnProcessingStarted(startArgs);

                // Execute pipeline
                var results = await pipeline.ExecuteAsync(input, context);

                // Get result (first output or null)
                var result = results.Count > 0 ? results[0] : null;

                // Raise event
                var completeArgs = new ProcessingEventArgs(context, input, pipeline)
                {
                    Output = result,
                    EndTime = DateTimeOffset.Now
                };
                OnProcessingCompleted(completeArgs);

                return result;
            }
            finally
            {
                _processingLock.Release();
            }
        }

        /// <summary>
        /// Create a pipeline from configuration
        /// </summary>
        private async Task<Pipeline.Pipeline> CreatePipelineFromConfigAsync(AgentConfiguration.PipelineConfig config)
        {
            // Create basic pipeline
            var pipeline = new Pipeline.Pipeline(config.Id, config.Name);
            var builder = new PipelineBuilder(pipeline, _moduleRegistry);

            // Apply template if specified
            if (!string.IsNullOrEmpty(config.TemplateId) && _templateRegistry != null)
            {
                var template = _templateRegistry.GetTemplate(config.TemplateId);
                if (template != null)
                {
                    await template.ApplyToBuilder(builder);
                }
            }

            // Build and initialize
            return await builder.BuildAsync();
        }

        /// <summary>
        /// Add a pipeline to the agent
        /// </summary>
        public async Task<Pipeline.Pipeline> AddPipelineAsync(
            string id,
            string name,
            Func<PipelineBuilder, Task<PipelineBuilder>> builderFunc)
        {
            if (_pipelines.ContainsKey(id))
                throw new ArgumentException($"Pipeline with ID '{id}' already exists");

            var pipeline = new Pipeline.Pipeline(id, name);
            var builder = new PipelineBuilder(pipeline, _moduleRegistry);

            // Apply builder function
            await builderFunc(builder);

            // Build and initialize
            pipeline = await builder.BuildAsync();

            // Add to collection
            _pipelines[id] = pipeline;

            // If no default pipeline, make this the default
            if (_defaultPipeline == null)
            {
                _defaultPipeline = pipeline;
            }

            // Add to configuration
            Configuration.Pipelines.Add(new AgentConfiguration.PipelineConfig
            {
                Id = id,
                Name = name,
                IsDefault = _defaultPipeline == pipeline
            });

            return pipeline;
        }

        /// <summary>
        /// Set the default pipeline
        /// </summary>
        public void SetDefaultPipeline(string pipelineId)
        {
            if (!_pipelines.TryGetValue(pipelineId, out var pipeline))
                throw new ArgumentException($"Pipeline not found: {pipelineId}");

            _defaultPipeline = pipeline;

            // Update configuration
            foreach (var config in Configuration.Pipelines)
            {
                config.IsDefault = config.Id == pipelineId;
            }
        }

        /// <summary>
        /// Get a pipeline by ID
        /// </summary>
        public Pipeline.Pipeline GetPipeline(string pipelineId)
        {
            if (_pipelines.TryGetValue(pipelineId, out var pipeline))
                return pipeline;

            return null;
        }

        /// <summary>
        /// Get all pipelines
        /// </summary>
        public IReadOnlyDictionary<string, Pipeline.Pipeline> GetAllPipelines() => _pipelines;

        /// <summary>
        /// Get the default pipeline
        /// </summary>
        public Pipeline.Pipeline GetDefaultPipeline() => _defaultPipeline;

        /// <summary>
        /// Remove a pipeline
        /// </summary>
        public async Task RemovePipelineAsync(string pipelineId)
        {
            if (!_pipelines.TryGetValue(pipelineId, out var pipeline))
                return;

            // Remove from collection
            _pipelines.Remove(pipelineId);

            // Remove from configuration
            var config = Configuration.Pipelines.FirstOrDefault(p => p.Id == pipelineId);
            if (config != null)
            {
                Configuration.Pipelines.Remove(config);
            }

            // If this was the default pipeline, set a new default
            if (_defaultPipeline == pipeline)
            {
                _defaultPipeline = _pipelines.Count > 0 ? _pipelines.Values.First() : null;

                // Update configuration
                if (_defaultPipeline != null)
                {
                    var newDefaultConfig = Configuration.Pipelines.FirstOrDefault(p => p.Id == _defaultPipeline.Id);
                    if (newDefaultConfig != null)
                    {
                        newDefaultConfig.IsDefault = true;
                    }
                }
            }

            // Shutdown the pipeline
            await pipeline.ShutdownAsync();
        }

        /// <summary>
        /// Get an agent setting
        /// </summary>
        public T GetSetting<T>(string key, T defaultValue = default)
        {
            if (Configuration.Settings.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;

            return defaultValue;
        }

        /// <summary>
        /// Set an agent setting
        /// </summary>
        public void SetSetting<T>(string key, T value)
        {
            Configuration.Settings[key] = value;
        }

        /// <summary>
        /// Add a module to the agent
        /// </summary>
        public async Task AddModuleAsync(string moduleId)
        {
            if (!Configuration.EnabledModules.Contains(moduleId))
            {
                Configuration.EnabledModules.Add(moduleId);

                if (_isInitialized)
                {
                    await _moduleRegistry.LoadModuleAsync(moduleId);
                }
            }
        }

        /// <summary>
        /// Remove a module from the agent
        /// </summary>
        public async Task RemoveModuleAsync(string moduleId)
        {
            if (Configuration.EnabledModules.Contains(moduleId))
            {
                Configuration.EnabledModules.Remove(moduleId);

                // Module unloading can only be done if the agent is already initialized
                if (_isInitialized)
                {
                    await _moduleRegistry.UnloadModuleAsync(moduleId);
                }
            }
        }

        /// <summary>
        /// Raise processing started event
        /// </summary>
        protected virtual void OnProcessingStarted(ProcessingEventArgs e)
        {
            ProcessingStarted?.Invoke(this, e);
        }

        /// <summary>
        /// Raise processing completed event
        /// </summary>
        protected virtual void OnProcessingCompleted(ProcessingEventArgs e)
        {
            ProcessingCompleted?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for processing events
    /// </summary>
    public class ProcessingEventArgs : EventArgs
    {
        /// <summary>
        /// Processing context
        /// </summary>
        public ProcessingContext Context { get; }

        /// <summary>
        /// Input audio
        /// </summary>
        public IAudioData Input { get; }

        /// <summary>
        /// Output audio
        /// </summary>
        public IAudioData Output { get; set; }

        /// <summary>
        /// Pipeline used for processing
        /// </summary>
        public Pipeline.Pipeline Pipeline { get; }

        /// <summary>
        /// Start time
        /// </summary>
        public DateTimeOffset StartTime { get; }

        /// <summary>
        /// End time
        /// </summary>
        public DateTimeOffset? EndTime { get; set; }

        /// <summary>
        /// Processing duration
        /// </summary>
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

        /// <summary>
        /// Create processing event args
        /// </summary>
        public ProcessingEventArgs(ProcessingContext context, IAudioData input, Pipeline.Pipeline pipeline)
        {
            Context = context;
            Input = input;
            Pipeline = pipeline;
            StartTime = DateTimeOffset.Now;
        }
    }
}
