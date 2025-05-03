using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Modules
{
    /// <summary>
    /// Base implementation for a module
    /// </summary>
    public abstract class ModuleBase : IModule
    {
        /// <summary>
        /// Unique identifier for the module
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Display name for the module
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Module version
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Module description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Module-specific configuration
        /// </summary>
        public IDictionary<string, object> Configuration { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Module capabilities
        /// </summary>
        public IList<string> Capabilities { get; } = new List<string>();

        // Module state
        protected bool IsInitialized = false;

        /// <summary>
        /// Create a new module
        /// </summary>
        protected ModuleBase(string id, string name, Version version, string description = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Description = description ?? $"Module: {name}";
        }

        /// <summary>
        /// Initialize the module
        /// </summary>
        public virtual async Task Initialize()
        {
            if (IsInitialized)
                return;

            await OnInitializeAsync();
            IsInitialized = true;
        }

        /// <summary>
        /// Shutdown the module
        /// </summary>
        public virtual async Task Shutdown()
        {
            if (!IsInitialized)
                return;

            await OnShutdownAsync();
            IsInitialized = false;
        }

        /// <summary>
        /// Node types exported by this module
        /// </summary>
        public abstract IEnumerable<Type> ExportedNodeTypes { get; }

        /// <summary>
        /// Called during initialization
        /// </summary>
        protected virtual Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called during shutdown
        /// </summary>
        protected virtual Task OnShutdownAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get a configuration value
        /// </summary>
        public T GetConfigurationValue<T>(string key, T defaultValue = default)
        {
            if (Configuration.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;

            return defaultValue;
        }

        /// <summary>
        /// Set a configuration value
        /// </summary>
        public void SetConfigurationValue<T>(string key, T value)
        {
            Configuration[key] = value;
        }

        /// <summary>
        /// Add a capability to the module
        /// </summary>
        protected void AddCapability(string capability)
        {
            if (!Capabilities.Contains(capability))
                Capabilities.Add(capability);
        }
    }
}
