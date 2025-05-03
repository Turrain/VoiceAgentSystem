
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Modules
{
    /// <summary>
    /// Registry for managing modules
    /// </summary>
    public class ModuleRegistry
    {
        private readonly Dictionary<string, IModule> _modules = new Dictionary<string, IModule>();
        private readonly List<string> _searchPaths = new List<string>();
        private readonly Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();

        /// <summary>
        /// Event raised when a module is loaded
        /// </summary>
        public event EventHandler<ModuleEventArgs> ModuleLoaded;

        /// <summary>
        /// Event raised when a module is unloaded
        /// </summary>
        public event EventHandler<ModuleEventArgs> ModuleUnloaded;

        /// <summary>
        /// Create a new module registry
        /// </summary>
        public ModuleRegistry()
        {
            // Add default search paths
            _searchPaths.Add(AppDomain.CurrentDomain.BaseDirectory);
            _searchPaths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules"));
        }

        /// <summary>
        /// Add a search path for modules
        /// </summary>
        public void AddSearchPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            if (!_searchPaths.Contains(path))
                _searchPaths.Add(path);
        }

        /// <summary>
        /// Get all registered modules
        /// </summary>
        public IReadOnlyCollection<IModule> GetAllModules() => _modules.Values;

        /// <summary>
        /// Get a module by ID
        /// </summary>
        public IModule GetModule(string moduleId)
        {
            if (_modules.TryGetValue(moduleId, out var module))
                return module;

            return null;
        }

        /// <summary>
        /// Register a module directly
        /// </summary>
        public async Task RegisterModuleAsync(IModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            if (_modules.ContainsKey(module.Id))
                throw new InvalidOperationException($"Module with ID '{module.Id}' already registered");

            await module.Initialize();
            _modules[module.Id] = module;

            OnModuleLoaded(new ModuleEventArgs(module));
        }

        /// <summary>
        /// Load a module from an assembly
        /// </summary>
        public async Task LoadModuleAsync(string moduleId)
        {
            // Check if already loaded
            if (_modules.ContainsKey(moduleId))
                return;

            // Try to find and load the module
            foreach (var path in _searchPaths)
            {
                // Check for module dll
                var modulePath = Path.Combine(path, $"{moduleId}.dll");
                if (File.Exists(modulePath))
                {
                    await LoadModuleFromAssemblyAsync(modulePath, moduleId);
                    return;
                }

                // Check for module directory with dll inside
                var moduleDir = Path.Combine(path, moduleId);
                if (Directory.Exists(moduleDir))
                {
                    var moduleFilePath = Path.Combine(moduleDir, $"{moduleId}.dll");
                    if (File.Exists(moduleFilePath))
                    {
                        await LoadModuleFromAssemblyAsync(moduleFilePath, moduleId);
                        return;
                    }
                }
            }

            // Try to find in already loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (await LoadModuleFromAssemblyAsync(assembly, moduleId))
                        return;
                }
                catch
                {
                    // Ignore errors in loaded assemblies
                }
            }

            throw new FileNotFoundException($"Module not found: {moduleId}");
        }

        /// <summary>
        /// Unload a module
        /// </summary>
        public async Task UnloadModuleAsync(string moduleId)
        {
            if (_modules.TryGetValue(moduleId, out var module))
            {
                await module.Shutdown();
                _modules.Remove(moduleId);

                OnModuleUnloaded(new ModuleEventArgs(module));
            }
        }

        /// <summary>
        /// Get all node types from all modules
        /// </summary>
        public IEnumerable<Type> GetAllNodeTypes()
        {
            return _modules.Values
                .SelectMany(m => m.ExportedNodeTypes)
                .Where(t => typeof(INode).IsAssignableFrom(t) && !t.IsAbstract);
        }

        /// <summary>
        /// Create a node instance from a type name
        /// </summary>
        public INode CreateNodeInstance(string typeName, string id, string name)
        {
            var nodeType = GetAllNodeTypes().FirstOrDefault(t => t.FullName == typeName);
            if (nodeType == null)
                throw new ArgumentException($"Node type not found: {typeName}");

            return (INode)Activator.CreateInstance(nodeType, id, name);
        }

        /// <summary>
        /// Load a module from an assembly file
        /// </summary>
        private async Task<bool> LoadModuleFromAssemblyAsync(string assemblyPath, string expectedModuleId = null)
        {
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");

            // Load the assembly
            Assembly assembly;
            if (_loadedAssemblies.TryGetValue(assemblyPath, out var loadedAssembly))
            {
                assembly = loadedAssembly;
            }
            else
            {
                assembly = Assembly.LoadFrom(assemblyPath);
                _loadedAssemblies[assemblyPath] = assembly;
            }

            return await LoadModuleFromAssemblyAsync(assembly, expectedModuleId);
        }

        /// <summary>
        /// Load a module from an already loaded assembly
        /// </summary>
        private async Task<bool> LoadModuleFromAssemblyAsync(Assembly assembly, string expectedModuleId = null)
        {
            // Find module types
            var moduleTypes = assembly.GetTypes()
                .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            if (moduleTypes.Count == 0)
                return false;

            // Load modules
            bool anyLoaded = false;
            foreach (var moduleType in moduleTypes)
            {
                try
                {
                    var module = (IModule)Activator.CreateInstance(moduleType);

                    // Check if this is the module we're looking for
                    if (expectedModuleId != null && module.Id != expectedModuleId)
                        continue;

                    // Skip if already loaded
                    if (_modules.ContainsKey(module.Id))
                        continue;

                    // Initialize and register
                    await module.Initialize();
                    _modules[module.Id] = module;

                    OnModuleLoaded(new ModuleEventArgs(module));
                    anyLoaded = true;

                    // If we found the expected module, we can return
                    if (expectedModuleId != null && module.Id == expectedModuleId)
                        return true;
                }
                catch
                {
                    // Ignore errors in individual modules
                }
            }

            return anyLoaded;
        }

        /// <summary>
        /// Raise module loaded event
        /// </summary>
        protected virtual void OnModuleLoaded(ModuleEventArgs e)
        {
            ModuleLoaded?.Invoke(this, e);
        }

        /// <summary>
        /// Raise module unloaded event
        /// </summary>
        protected virtual void OnModuleUnloaded(ModuleEventArgs e)
        {
            ModuleUnloaded?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for module events
    /// </summary>
    public class ModuleEventArgs : EventArgs
    {
        /// <summary>
        /// The module
        /// </summary>
        public IModule Module { get; }

        /// <summary>
        /// Create module event args
        /// </summary>
        public ModuleEventArgs(IModule module)
        {
            Module = module;
        }
    }
}
