using System;

namespace VoiceBotSystem.Core.Exceptions
{
    /// <summary>
    /// Exception thrown for pipeline errors
    /// </summary>
    public class PipelineException : Exception
    {
        /// <summary>
        /// Pipeline ID
        /// </summary>
        public string PipelineId { get; }

        /// <summary>
        /// Create pipeline exception
        /// </summary>
        public PipelineException(string message) : base(message)
        {
        }

        /// <summary>
        /// Create pipeline exception with pipeline ID
        /// </summary>
        public PipelineException(string pipelineId, string message) : base(message)
        {
            PipelineId = pipelineId;
        }

        /// <summary>
        /// Create pipeline exception with inner exception
        /// </summary>
        public PipelineException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Create pipeline exception with pipeline ID and inner exception
        /// </summary>
        public PipelineException(string pipelineId, string message, Exception innerException) : base(message, innerException)
        {
            PipelineId = pipelineId;
        }
    }
}
