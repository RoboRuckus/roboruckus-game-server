using System.Collections.Generic;

namespace RoboRuckus.Logging
{
    /// <summary>
    /// Class for collecting loggers to use
    /// </summary>
    public static class Loggers
    {
        /// <summary>
        /// Loggers currently used
        /// </summary>
        private static List<ILogger> _loggers = new List<ILogger>();

        /// <summary>
        /// Loggers currently used
        /// </summary>
        public static List<ILogger> loggers
        {
            get { return _loggers; }
        }

        /// <summary>
        /// Add a logger to the list of currently used loggers
        /// </summary>
        /// <param name="logger">The logger to add</param>
        public static void AddLogger(ILogger logger) 
        {
            _loggers.Add(logger);
        }
    }
}
