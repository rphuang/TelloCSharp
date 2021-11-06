using System;
using System.Collections.Generic;

namespace LogLib
{
    /// <summary>
    /// default log to console
    /// </summary>
    public class ConsoleLog : ILog
    {
        /// <summary>
        /// the name of the ILog
        /// </summary>
        public string Name { get; set; } = nameof(ConsoleLog);

        /// <summary>
        /// get/set the level of LogSeverity to be logged - write to console if >= LogSeverityLevel
        /// </summary>
        public LogSeverity LogSeverityLevel { get; set; } = LogSeverity.Info;

        /// <summary>
        /// get foreground text color for LogSeverity
        /// </summary>
        /// <param name="severity">the LogSeverity level</param>
        /// <returns>the foreground text color for the first left bit</returns>
        public ConsoleColor GetTextColor(LogSeverity severity)
        {
            return _textColors[(int)severity];
        }

        /// <summary>
        /// set foreground text color for LogSeverity
        /// </summary>
        /// <param name="severity">the LogSeverity level</param>
        /// <param name="color">the color for the foreground text</param>
        public void SetTextColor(LogSeverity severity, ConsoleColor color)
        {
            _textColors[(int)severity] = color;
        }

        /// <summary>
        /// Write Line
        /// </summary>
        /// <param name="severity">severity of the message</param>
        /// <param name="format">format string</param>
        /// <param name="arg">format arguments</param>
        public void WriteLine(LogSeverity severity, string timeStamp, string format, params object[] arg)
        {
            if (severity < LogSeverityLevel) return;

            string msgFormat = string.Format(System.Globalization.CultureInfo.CurrentUICulture, "{0} {1}", timeStamp, format);
            ConsoleColor color = _textColors[(int)severity];
            WriteLine(color, msgFormat, arg);
        }

        /// <summary>
        /// Write Line
        /// </summary>
        /// <param name="color">console color</param>
        /// <param name="format">format string</param>
        /// <param name="arg">format arguments</param>
        public void WriteLine(ConsoleColor color, string format, params object[] arg)
        {
            lock (Lock)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                try
                {
                    Console.WriteLine(format, arg);
                }
                catch (Exception err)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ConsoleLog exception: {err}");
                }
                Console.ForegroundColor = originalColor;
            }
        }

        protected ConsoleColor[] _textColors = {
            Console.ForegroundColor,    // Debug
            Console.ForegroundColor,    // Info
            ConsoleColor.Yellow,        // Warning
            ConsoleColor.Cyan,          // Action
            ConsoleColor.Green,         // Success
            ConsoleColor.Red,           // Error
            ConsoleColor.DarkRed,       // FatalError
        };

        /// <summary>
        /// used for lock for control color in console output
        /// </summary>
        private static readonly object Lock = new object();
    }
}
