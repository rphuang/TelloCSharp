using System;
using System.Collections.Generic;

namespace LogLib
{
    /// <summary>
    /// Log utility class
    /// </summary>
    public class Log
    {
        /// <summary>
        /// write an action message to console with blue color
        /// </summary>
        public static void Action(string format, params object[] arg)
        {
            WriteLineWithTimeStamp(LogSeverity.Action, format, arg);
        }

        /// <summary>
        /// write a success/pass message to console with green color
        /// </summary>
        public static void Pass(string format, params object[] arg)
        {
            WriteLineWithTimeStamp(LogSeverity.Success, format, arg);
        }

        /// <summary>
        /// write an error message to console with red color
        /// </summary>
        public static void Error(string format, params object[] arg)
        {
            WriteLineWithTimeStamp(LogSeverity.Error, format, arg);
        }

        /// <summary>
        /// write a Warning message to console with yellow color
        /// </summary>
        public static void Warn(string format, params object[] arg)
        {
            WriteLineWithTimeStamp(LogSeverity.Warning, format, arg);
        }

        /// <summary>
        /// write a success/pass message to console with green color
        /// </summary>
        public static void Fail(string format, params object[] arg)
        {
            WriteLineWithTimeStamp(LogSeverity.Error, format, arg);
        }

        /// <summary>
        /// write an info message to console with default color
        /// </summary>
        public static void Info(string format, params object[] arg)
        {
            WriteLineWithTimeStamp(LogSeverity.Info, format, arg);
        }

        /// <summary>
        /// write a debug message to the console with default color
        /// </summary>
        public static void Debug(string format, params object[] arg)
        {
            WriteLineWithTimeStamp(LogSeverity.Debug, "DEBUG " + format, arg);
        }

        /// <summary>
        /// write to console with color without timestamp
        /// </summary>
        public static void WriteLine(string format, params object[] arg)
        {
            WriteLine(LogSeverity.Info, string.Empty, format, arg);
        }

        /// <summary>
        /// get the first ILog.
        /// set will clear all existing ILogs and add the ILog.
        /// </summary>
        public static ILog Instance
        {
            get { return Instances[0]; }
            set
            {
                if (value != null)
                {
                    Instances.Clear();
                    Instances.Add(value);
                }
            }
        }

        /// <summary>
        /// get the list of ILogs. Initialized with ConsoleLog.
        /// </summary>
        public static IList<ILog> Instances { get; } = Initialize();

        /// <summary>
        /// Log Message with particular log level and prepended time stamp
        /// </summary>
        /// <param name="severity">severity of the message</param>
        /// <param name="color">color of message if on console</param>
        /// <param name="format">format string</param>
        /// <param name="arg">format string args</param>
        internal static void WriteLineWithTimeStamp(LogSeverity severity, string format, params object[] arg)
        {
            string now = DateTime.Now.ToString("yyyy'-'MMdd'-'HHmm'-'ss.fff");
            WriteLine(severity, $"{now}", format, arg);
        }

        /// <summary>
        /// Write Line to ILog
        /// </summary>
        /// <param name="severity">severity of the message</param>
        /// <param name="color">console color</param>
        /// <param name="format">format string</param>
        /// <param name="arg">format arguments</param>
        private static void WriteLine(LogSeverity severity, string timeStamp, string format, params object[] arg)
        {
            foreach (ILog log in Instances)
            {
                try
                {
                    // todo: make it async
                    log.WriteLine(severity, timeStamp, format, arg);
                }
                catch (Exception err)
                {
                    Console.WriteLine($"Exception from {log.Name}: {err.ToString()}");
                }
            }
        }

        private static IList<ILog> Initialize()
        {
            List<ILog> list = new List<ILog>();
            list.Add(new ConsoleLog());
            return list;
        }
    }
}
