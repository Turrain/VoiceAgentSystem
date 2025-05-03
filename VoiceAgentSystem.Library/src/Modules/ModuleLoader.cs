using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using VoiceBotSystem.Core.Interfaces;

namespace VoiceBotSystem.Modules
{
    /// <summary>
    /// Utility for loading modules
    /// </summary>
    public static class ModuleLoader
    {
        /// <summary>
        /// Load embedded modules
        /// </summary>
        public static async Task LoadEmbeddedModulesAsync(ModuleRegistry registry)
        {
            // Create core modules
            await registry.RegisterModuleAsync(new CoreProcessingModule());
        }

        /// <summary>
        /// Load modules from a directory
        /// </summary>
        public static async Task LoadModulesFromDirectoryAsync(ModuleRegistry registry, string directory)
        {
            if (!Directory.Exists(directory))
                return;

            // Find all module assemblies
            foreach (var file in Directory.GetFiles(directory, "*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    await ScanAssemblyForModulesAsync(registry, assembly);
                }
                catch
                {
                    // Ignore errors in individual assemblies
                }
            }

            // Load modules from subdirectories
            foreach (var subdir in Directory.GetDirectories(directory))
            {
                var dirName = Path.GetFileName(subdir);
                var moduleFile = Path.Combine(subdir, $"{dirName}.dll");

                if (File.Exists(moduleFile))
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(moduleFile);
                        await ScanAssemblyForModulesAsync(registry, assembly);
                    }
                    catch
                    {
                        // Ignore errors in individual assemblies
                    }
                }
            }
        }

        /// <summary>
        /// Scan an assembly for modules
        /// </summary>
        private static async Task ScanAssemblyForModulesAsync(ModuleRegistry registry, Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IModule).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    try
                    {
                        var module = (IModule)Activator.CreateInstance(type);

                        // Register the module
                        await registry.RegisterModuleAsync(module);
                    }
                    catch
                    {
                        // Ignore errors in individual modules
                    }
                }
            }
        }
    }

    /// <summary>
    /// Core processing module with basic nodes
    /// </summary>
    public class CoreProcessingModule : ModuleBase
    {
        private static readonly Type[] _exportedTypes = new Type[]
        {
            typeof(Nodes.Processors.AudioMixerNode),
            typeof(Nodes.Processors.AudioPassthroughNode),
            typeof(Nodes.Processors.AudioSplitterNode),
            typeof(Nodes.Processors.VolumeControlNode)
        };

        /// <summary>
        /// Create core processing module
        /// </summary>
        public CoreProcessingModule()
            : base("CoreProcessing", "Core Processing Module", new Version(1, 0, 0),
                  "Core audio processing capabilities")
        {
            // Define capabilities
            AddCapability("audio_mixing");
            AddCapability("audio_splitting");
            AddCapability("volume_control");
        }

        /// <summary>
        /// Node types exported by this module
        /// </summary>
        public override IEnumerable<Type> ExportedNodeTypes => _exportedTypes;
    }
}
