using LogLib;
using System;
using System.IO;
using System.Threading;
using tellocs;

namespace TelloCmdCS
{
    class Program
    {
        static Tello tello;

        static void Main(string[] args)
        {
            ConsoleLog log = Log.Instance as ConsoleLog;
            log.LogSeverityLevel = LogSeverity.Info;

            Log.Action("Connecting to Tello");
            tello = new Tello();

            tello.Connect();
            while (true)
            {
                Log.WriteLine("0-takeoff, 1-land, 2-end, run, photo, video, sleep, or just type tello commands? ");
                string input = Console.ReadLine();

                if (!ExecuteCommand(input)) break;
            }

            Log.Action($"Disconnecting from Tello! with battery: {tello.Battery}");
            tello.Disconnect();
        }

        static bool ExecuteCommand(string input)
        {
            input = input.Trim().ToLower();
            if (input == null ||
                string.Equals("2", input) ||
                string.Equals("end", input)
                )
            {
                return false;
            }

            try
            {
                if (input.Length == 0) { }
                else if ("1" == input || string.Equals("land", input)) tello.Land();
                else if ("0" == input || string.Equals("takeoff", input)) tello.Takeoff();
                else if ("p" == input || string.Equals("photo", input)) tello.SavePhoto(TimeStampFileName("jpg"));
                else if ("v" == input || string.Equals("video", input)) tello.StartOrStopVideoRecording(TimeStampFileName("avi"));
                else if ("s" == input || string.Equals("stream", input)) tello.StartOrStopVideoStreaming();
                else if ("?" == input || string.Equals("help", input)) Help();
                else if (input.EndsWith('?')) tello.Query<string>(input);
                else if (string.Equals("sleep", input))
                    Thread.Sleep((int)(1000 * GetCommandValue<float>(input, 1.0f)));
                else if (string.Equals("run", input) || string.Equals("load", input))
                    DoCommandFromFile(GetCommandValue<string>(input, "telloCommands.txt"));
                else if (input.StartsWith("up")) tello.Up(GetCommandValue<int>(input, 20));
                else if (input.StartsWith("down")) tello.Down(GetCommandValue<int>(input, 20));
                else if (input.StartsWith("rigt")) tello.Right(GetCommandValue<int>(input, 20));
                else if (input.StartsWith("left")) tello.Left(GetCommandValue<int>(input, 20));
                else if (input.StartsWith("forward")) tello.Forward(GetCommandValue<int>(input, 20));
                else if (input.StartsWith("back")) tello.Back(GetCommandValue<int>(input, 20));
                else if (input.StartsWith("cw")) tello.Clockwise(GetCommandValue<int>(input, 10));
                else if (input.StartsWith("ccw")) tello.CounterClockwise(GetCommandValue<int>(input, 10));
                else
                {
                    tello.Control(input, 10000);
                }
                return true;
            }
            catch (Exception err)
            {
                Log.Error($"Exception: {err}");
            }
            return false;
        }

        static void DoCommandFromFile(string fileName)
        {
            try
            {
                Log.Action($"Loading command file: {fileName}");
                using (TextReader reader = File.OpenText(fileName))
                {
                    string buffer;
                    while ((buffer = reader.ReadLine()) != null)
                    {
                        buffer = buffer.Trim();
                        if (string.IsNullOrEmpty(buffer) || buffer.StartsWith("#")) continue;

                        ExecuteCommand(buffer);
                    }
                    reader.Close();
                }
            }
            catch (Exception err)
            {
                Log.Error($"Exception: {err}");
            }
        }

        static T GetCommandValue<T>(string input, T defaultValue)
        {
            string[] keyvalue = input.Split(CommandValueDelimiter);
            if (keyvalue.Length > 1) return (T)Convert.ChangeType(keyvalue[1], typeof(T));
            return defaultValue;
        }
        static readonly char[] CommandValueDelimiter = { ' ' };

        static string TimeStampFileName(string ext)
        {
            return $"{DateTime.Now.ToString("yyyy'-'MMdd'-'HHmmss'-'ff")}.{ext}";
        }
        static void Help()
        {
            Log.WriteLine("Control Tello drone with command line or text file contains commands. Available commands:");
            Log.WriteLine("  0 - takeoff");
            Log.WriteLine("  1 - land");
            Log.WriteLine("  2 - end and exit");
            Log.WriteLine("  p[hoto]     - take a picture and save to file (yyyy-MMdd-HHmmss-ff.jpg)");
            Log.WriteLine("  v[ideo]     - start/stop video recording and save to file (yyyy-MMdd-HHmmss-ff.avi)");
            Log.WriteLine("  s[tream]    - start/stop video streaming");
            Log.WriteLine("  run <file>  - load and execute commands from file (default: telloCommands.txt)");
            Log.WriteLine("  sleep <sec> - sleep in seconds (default: 1.0)");
            Log.WriteLine("  help        - print this help menu");
            Log.WriteLine("  or just enter a valid Tello commands like 'up 20', 'left 50', 'cw 90', 'flip r'");
        }
    }
}
