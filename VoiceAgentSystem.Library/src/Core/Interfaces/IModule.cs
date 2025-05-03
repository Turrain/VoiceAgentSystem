using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VoiceBotSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for a module in the system
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Unique identifier for the module
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name for the module
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Module version
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Module description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Node types exported by this module
        /// </summary>
        IEnumerable<Type> ExportedNodeTypes { get; }

        /// <summary>
        /// Initialize the module
        /// </summary>
        Task Initialize();

        /// <summary>
        /// Shutdown the module
        /// </summary>
        Task Shutdown();

        /// <summary>
        /// Module-specific configuration
        /// </summary>
        IDictionary<string, object> Configuration { get; }

        /// <summary>
        /// Module capabilities
        /// </summary>
        IList<string> Capabilities { get; }
    }
}
