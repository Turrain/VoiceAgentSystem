using System.Collections.Generic;

namespace VoiceBotSystem.Agent
{
    /// <summary>
    /// Configuration for a voice bot agent
    /// </summary>
    public class AgentConfiguration
    {
        /// <summary>
        /// Agent ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Agent name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Agent description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Agent-specific settings
        /// </summary>
        public Dictionary<string, object> Settings { get; } = new Dictionary<string, object>();

        /// <summary>
        /// IDs of enabled modules
        /// </summary>
        public List<string> EnabledModules { get; } = new List<string>();

        /// <summary>
        /// Pipeline configurations
        /// </summary>
        public List<PipelineConfig> Pipelines { get; } = new List<PipelineConfig>();

        /// <summary>
        /// Pipeline configuration
        /// </summary>
        public class PipelineConfig
        {
            /// <summary>
            /// Pipeline ID
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Pipeline name
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// ID of the template to use
            /// </summary>
            public string TemplateId { get; set; }

            /// <summary>
            /// Whether this is the default pipeline
            /// </summary>
            public bool IsDefault { get; set; }

            /// <summary>
            /// Pipeline-specific settings
            /// </summary>
            public Dictionary<string, object> Settings { get; } = new Dictionary<string, object>();
        }
    }
}
