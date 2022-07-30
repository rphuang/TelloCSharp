using LogLib;
using System;
using System.IO;
using System.Threading;

namespace tellocs
{
    /// <summary>
    /// TelloCmd adds text command processing to the Tello drone SDK in C#
    /// </summary>
    public class TelloCmd : Tello
    {
        /// <summary>
        /// whether to multiply current Tello speed to the RC control command
        /// example: if true and speed is 50 then "rc 0.5 1 0 0" will become "rc 25 50 0 0"
        /// </summary>
        public bool UseTelloSpeedForRCControl { get; set; } = true;

        /// <summary>
        /// the format string to get time stamp for file name
        /// </summary>
        public string TimeStampFileNameFormat { get; set; } = "yyyy'-'MMdd'-'HHmm'-'ssff";

        /// <summary>
        /// the folder to save all picture files
        /// </summary>
        public string DefaultPhotoFolder { get; set; } = string.Empty;

        /// <summary>
        /// the folder to save all video files
        /// </summary>
        public string DefaultVideoFolder { get; set; } = string.Empty;

        /// <summary>
        /// constructor
        /// </summary>
        public TelloCmd(string ipAddress = TelloIPAddress) : base(ipAddress)
        {
        }

        /// <summary>
        /// execute a tello command by string
        /// </summary>
        public bool ExecuteCommand(string command)
        {
            command = command.Trim().ToLower();
            if (command == null ||
                string.Equals("end", command)
                )
            {
                return false;
            }

            try
            {
                if (command.Length == 0) { }
                else if (string.Equals("land", command)) Land();
                else if (string.Equals("takeoff", command)) Takeoff();
                else if ("p" == command || string.Equals("photo", command)) SavePhoto();
                else if ("v" == command || string.Equals("video", command)) StartOrStopVideoRecording();
                else if ("s" == command || string.Equals("stream", command)) StartOrStopVideoStreaming();
                else if (command.EndsWith("?")) Query<string>(command);
                else if (string.Equals("sleep", command))
                    Thread.Sleep((int)(1000 * GetCommandValue<float>(command, 1.0f)));
                else if (string.Equals("run", command) || string.Equals("load", command))
                    RunCommandFromFile(GetCommandValue<string>(command, "telloCommands.txt"));
                else if (command.StartsWith("up")) Up(GetCommandValue<int>(command, 20));
                else if (command.StartsWith("down")) Down(GetCommandValue<int>(command, 20));
                else if (command.StartsWith("rigt")) Right(GetCommandValue<int>(command, 20));
                else if (command.StartsWith("left")) Left(GetCommandValue<int>(command, 20));
                else if (command.StartsWith("forward")) Forward(GetCommandValue<int>(command, 20));
                else if (command.StartsWith("back")) Back(GetCommandValue<int>(command, 20));
                else if (command.StartsWith("cw")) Clockwise(GetCommandValue<int>(command, 10));
                else if (command.StartsWith("ccw")) CounterClockwise(GetCommandValue<int>(command, 10));
                else if (command.StartsWith("rc "))
                {
                    if (!UseTelloSpeedForRCControl) Control(command, false);
                    else
                    {
                        string[] values = command.Split(CommandValueDelimiter);
                        if (values.Length != 5) throw new Exception($"Invalid RC control: {command}");
                        SendRCControl(GetRCSpeed(values[1]), GetRCSpeed(values[2]), GetRCSpeed(values[3]), GetRCSpeed(values[4]));
                    }
                }
                else if (command.StartsWith("reboot")) Control(command, false);
                else if (command.StartsWith("usetellospeedforrccontrol"))
                {
                    UseTelloSpeedForRCControl = GetCommandValue<bool>(command, true);
                }
                else
                {
                    Control(command, true, 10000);
                }
                return true;
            }
            catch (Exception err)
            {
                Log.Error($"Exception: {err}");
            }
            return false;
        }

        /// <summary>
        /// run commands from a file
        /// </summary>
        public void RunCommandFromFile(string fileName)
        {
            // preserve original value of UseTelloSpeedForRCControl for running the command file
            bool old_UseTelloSpeedForRCControl = UseTelloSpeedForRCControl;
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
            UseTelloSpeedForRCControl = old_UseTelloSpeedForRCControl;
        }

        /// <summary>
        /// save a picture asynchronously
        /// </summary>
        public void SavePhoto()
        {
            SavePhoto(GetTimeStampFileName(DefaultPhotoFolder, "jpg"));
        }

        /// <summary>
        /// start/stop recording a video asynchronously
        /// </summary>
        public void StartOrStopVideoRecording()
        {
            StartOrStopVideoRecording(GetTimeStampFileName(DefaultVideoFolder, "avi"));
        }

        /// <summary>
        /// get file name using current local time
        /// </summary>
        public string GetTimeStampFileName(string folder, string ext)
        {
            string fileName = $"{DateTime.Now.ToString(TimeStampFileNameFormat)}.{ext}";
            return Path.Combine(folder, fileName);
        }

        private int GetRCSpeed(string value)
        {
            return (int)(_speed * int.Parse(value));
        }

        static T GetCommandValue<T>(string input, T defaultValue)
        {
            string[] keyvalue = input.Split(CommandValueDelimiter);
            if (keyvalue.Length > 1) return (T)Convert.ChangeType(keyvalue[1], typeof(T));
            return defaultValue;
        }
        static readonly char[] CommandValueDelimiter = { ' ' };
    }
}
