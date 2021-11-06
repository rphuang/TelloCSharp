using System;

namespace LogLib
{
    /// <summary>
    /// Enum that indicates Log Level
    /// </summary>
    public enum LogSeverity
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Action = 3,
        Success = 4,
        Error = 5,
        FatalError = 6,
    }

    /// <summary>
    /// interface to log
    /// </summary>
    public interface ILog
    {
        /// <summary>
        /// the name of the ILog
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Write Line
        /// </summary>
        /// <param name="severity">severity of the message</param>
        /// <param name="timeStamp">the time stamp for the log</param>
        /// <param name="format">format string</param>
        /// <param name="arg">format arguments</param>
        void WriteLine(LogSeverity severity, string timeStamp, string format, params object[] arg);
    }
}
