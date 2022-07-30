using LogLib;
using SettingsLib;
using System;
using System.IO;
using tellocs;

namespace TelloCmdCS
{
    class Program
    {
        static TelloCmd tello;

        static void Main(string[] args)
        {
            ConsoleLog log = Log.Instance as ConsoleLog;
            log.LogSeverityLevel = LogSeverity.Info;

            tello = new TelloCmd();

            // load settings
            string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TelloConfig.txt");
            Log.Action($"Load settings from {settingsFile}");
            Settings settings = new Settings(settingsFile);
            settings.LoadSettings();
            tello.Speed = settings.GetOrAddSetting<int>("DefaultDroneSpeed", 50);
            tello.DefaultPhotoFolder = settings.GetOrAddSetting<string>("DefaultPhotoFolder", string.Empty);
            tello.DefaultVideoFolder = settings.GetOrAddSetting<string>("DefaultVideoFolder", string.Empty);
            tello.FFmpegPath = settings.GetOrAddSetting<string>("FFmpegPath", "ffmpeg.exe");
            tello.DebugMode = settings.GetOrAddSetting<bool>("DebugMode", false);

            Log.Action("Connecting to Tello");
            tello.Connect();
            while (true)
            {
                Log.WriteLine("0-takeoff, 1-land, 2-end, run, photo, video, sleep, or just type tello commands? ");
                string input = Console.ReadLine();

                if (input == null || string.Equals("2", input) || string.Equals("end", input)) break;
                else if ("?" == input || string.Equals("help", input)) Help();
                else
                {
                    string cmd = input;
                    if ("0" == input) cmd = "takeoff";
                    if ("1" == input) cmd = "land";
                    if (!tello.ExecuteCommand(cmd)) break;
                }
            }

            Log.Action($"Disconnecting from Tello! with battery: {tello.Battery}");
            tello.Disconnect();
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
