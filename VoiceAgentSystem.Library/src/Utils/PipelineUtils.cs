using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceBotSystem.Core.Interfaces;
using VoiceBotSystem.Pipeline;

namespace VoiceBotSystem.Utils
{
    /// <summary>
    /// Utilities for working with pipelines
    /// </summary>
    public static class PipelineUtils
    {
        /// <summary>
        /// Save a pipeline to a JSON file
        /// </summary>
        public static async Task SavePipelineToJsonAsync(Pipeline.Pipeline pipeline, string filePath)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            var pipelineData = new
            {
                Id = pipeline.Id,
                Name = pipeline.Name,
                Nodes = pipeline.GetAllNodes().Select(n => new
                {
                    Id = n.Id,
                    Name = n.Name,
                    Type = n.GetType().AssemblyQualifiedName,
                    IsEnabled = n.IsEnabled,
                    Configuration = n.Configuration
                }).ToArray(),
                Connections = pipeline.GetAllConnections().Select(c => new
                {
                    Id = c.Id,
                    SourceId = c.Source.Id,
                    TargetId = c.Target.Id,
                    Label = c.Label,
                    IsEnabled = c.IsEnabled,
                    Priority = c.Priority,
                    Configuration = c.Configuration
                }).ToArray()
            };

            var json = JsonSerializer.Serialize(pipelineData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// Get a visualization of the pipeline as a DOT graph
        /// </summary>
        public static string GetPipelineDotGraph(Pipeline.Pipeline pipeline)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            var nodes = pipeline.GetAllNodes();
            var connections = pipeline.GetAllConnections();

            var dot = new List<string>
            {
                $"digraph \"{pipeline.Name}\" {{",
                "  rankdir=LR;",
                "  node [shape=box, style=\"rounded,filled\", fillcolor=lightblue];",
                ""
            };

            // Add nodes
            foreach (var node in nodes)
            {
                string nodeType = node.GetType().Name;
                string label = $"{node.Name}\\n({nodeType})";
                string color = GetNodeColor(node);

                dot.Add($"  \"{node.Id}\" [label=\"{label}\", fillcolor={color}];");
            }

            dot.Add("");

            // Add connections
            foreach (var conn in connections)
            {
                string style = conn.IsEnabled ? "solid" : "dashed";
                string label = string.IsNullOrEmpty(conn.Label) ? "" : $" [label=\"{conn.Label}\"]";

                dot.Add($"  \"{conn.Source.Id}\" -> \"{conn.Target.Id}\"{label} [style={style}];");
            }

            dot.Add("}");

            return string.Join(Environment.NewLine, dot);
        }

        /// <summary>
        /// Determine the color for a node based on its type
        /// </summary>
        private static string GetNodeColor(INode node)
        {
            if (node is IAudioInputNode && node is IAudioOutputNode)
                return "lightyellow"; // Both input and output
            else if (node is IAudioInputNode)
                return "lightgreen"; // Input node
            else if (node is IAudioOutputNode)
                return "lightpink"; // Output node
            else
                return "lightblue"; // Other node
        }

        /// <summary>
        /// Check if a pipeline has a path from an input node to an output node
        /// </summary>
        public static bool HasValidPath(Pipeline.Pipeline pipeline)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            var entryPoints = pipeline.GetEntryPoints();
            var exitPoints = pipeline.GetExitPoints();

            if (entryPoints.Count == 0 || exitPoints.Count == 0)
                return false;

            // Check if there's a path from any entry point to any exit point
            foreach (var entry in entryPoints)
            {
                if (HasPath(entry, exitPoints))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if there's a path from a node to any of the specified targets
        /// </summary>
        private static bool HasPath(INode start, IReadOnlyList<IAudioOutputNode> targets)
        {
            // Breadth-first search
            var visited = new HashSet<INode>();
            var queue = new Queue<INode>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                // Check if this is a target
                if (targets.Contains(node))
                    return true;

                // Add all connected nodes
                foreach (var conn in node.OutputConnections.Where(c => c.IsEnabled))
                {
                    if (!visited.Contains(conn.Target))
                    {
                        visited.Add(conn.Target);
                        queue.Enqueue(conn.Target);
                    }
                }
            }

            return false;
        }
    }
}
