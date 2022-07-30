using LogLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace tellocs
{
    /// <summary>
    /// CommandCallback will be called when sending a command to Tello
    /// </summary>
    /// <param name="cmd">the command send to Tello</param>
    public delegate void CommandCallback(string cmd);

    /// <summary>
    /// CommandResultCallback will be called when receiving a command response from Tello
    /// </summary>
    /// <param name="cmd">the command send to Tello</param>
    /// <param name="result">the result of the command after receiving response</param>
    public delegate void CommandResultCallback(string cmd, string result);

    /// <summary>
    /// Tello drone SDK in C#
    /// </summary>
    public class Tello
    {
        /// <summary>
        /// Tello's factory setting for IP address
        /// </summary>
        public const string TelloIPAddress = "192.168.10.1";

        /// <summary>
        /// Tello's factory setting for out-going message port for command and query
        /// </summary>
        public const int TelloMessageUdpPort = 8889;

        /// <summary>
        /// Tello's factory setting for port to receive drone state
        /// </summary>
        public const int TelloStateUdpPort = 8890;

        /// <summary>
        /// Tello's factory setting for port to receive video
        /// </summary>
        public const int TelloVideoUdpPort = 11111;

        /// <summary>
        /// constructor
        /// </summary>
        public Tello(string ipAddress = TelloIPAddress)
        {
            _telloIpAddress = ipAddress;
            _udpMessageClient = new UdpClient(_messageUdpPort);
            _remoteMessageIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
        }

        /// <summary>
        /// CommandCallback will be called when sending a command to Tello
        /// </summary>
        public CommandCallback CommandCallback { get; set; }

        /// <summary>
        /// CommandResultCallback will be called when receiving a command response from Tello
        /// </summary>
        public CommandResultCallback CommandResultCallback { get; set; }

        /// <summary>
        /// ffmpeg program path (& file name)
        /// </summary>
        public string FFmpegPath { get; set; } = "ffmpeg.exe";

        /// <summary>
        /// enable debug mode
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// connected to the drone ("command" sent successfully)
        /// </summary>
        public bool Connected { get; private set; }

        /// <summary>
        /// the drone is flying ("takeoff" send successfully)
        /// </summary>
        public bool Flying { get; private set; }

        /// <summary>
        /// whether video recording is in progress
        /// </summary>
        public bool VideoRecording { get; private set; }

        /// <summary>
        /// whether video streaming is requested in progress
        /// </summary>
        public bool VideoStreaming { get; private set; }

        /// <summary>
        /// get the drone's state by name
        /// </summary>
        /// <param name="stateName">name of the state (pitch, h, bat, baro, ...) as defined in Tello SDK</param>
        /// <returns></returns>
        public T DroneState<T>(string stateName)
        {
            return (T)Convert.ChangeType(_droneState[stateName], typeof(T));
        }

        /// <summary>
        /// send query message to drone
        /// </summary>
        /// <param name="query">the query with ? at the end</param>
        /// <returns></returns>
        public T Query<T>(string query)
        {
            TelloResponse response = SendMessage(query, waitForResponse: true, timeOutMs: 1000, expectedResponse: null);
            if (response.Ok) return (T)Convert.ChangeType(response.Response, typeof(T));
            return default(T);
        }

        /// <summary>
        /// send control command to drone
        /// </summary>
        /// <param name="command">the command string sent to drone</param>
        /// <param name="waitForResponse">whether to wait for response from tello</param>
        /// <param name="timeOutMs">time in millisecond to wait for response</param>
        /// <returns>the raw response from drone</returns>
        public string Control(string command, bool waitForResponse = true, int timeOutMs = 2000)
        {
            TelloResponse response = SendMessage(command, waitForResponse: waitForResponse, timeOutMs: timeOutMs, expectedResponse: "ok");
            return response.Response;
        }

        /// <summary>
        /// send query to drone to get battery
        /// </summary>
        public int Battery { get { return Query<int>("battery?"); } }

        /// <summary>
        /// send query to drone to get/set spped setting
        /// </summary>
        public float Speed
        {
            get
            {
                _speed = Query<float>("speed?");
                return _speed;
            }
            set
            {
                TelloResponse response = SendMessage($"speed {value}");
                if (response.Ok) _speed = value;
            }
        }
        
        /// <summary>
        /// connect to the drone
        /// </summary>
        /// <returns>returns true if the drone is connected</returns>
        public bool Connect()
        {
            TelloResponse response = SendMessage("command");
            Connected = response.Ok;
            if (Connected)
            {
                try
                {
                    // sometimes Tello fail when query battery after cold start/boot
                    Log.Info($"Connected to Tello: battery={Battery}");
                }
                catch { }
                StartTelloStateTask();
            }
            return Connected;
        }

        /// <summary>
        /// dis-connect from the drone
        /// </summary>
        public void Disconnect()
        {
            if (Flying) Land();
            if (_streaming) StreamOff();
            _updateState = false;
            Connected = false;
            _udpMessageClient.Close();
        }

        /// <summary>
        /// send takeoff command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Takeoff()
        {
            TelloResponse response = SendMessage("takeoff", waitForResponse: true, timeOutMs: 10000);
            Flying = response.Ok;
            return Flying;
        }

        /// <summary>
        /// send land command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Land()
        {
            TelloResponse response = SendMessage("land", waitForResponse: true, timeOutMs: 10000);
            Flying = !response.Ok;
            return response.Ok;
        }

        /// <summary>
        /// send emergency command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Emergency()
        {
            TelloResponse response = SendMessage("emergency", waitForResponse: true, timeOutMs: 10000);
            Flying = !response.Ok;
            return response.Ok;
        }

        /// <summary>
        /// send fly (forward, back, left, right) command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Fly(string cmd, int distance)
        {
            TelloResponse response = SendMessage($"{cmd} {distance}", waitForResponse: true, timeOutMs: GetTimeoutByDistance(distance));
            return response.Ok;
        }

        /// <summary>
        /// send up command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Up(int distance)
        {
            return Fly("up", distance);
        }

        /// <summary>
        /// send down command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Down(int distance)
        {
            return Fly("down", distance);
        }

        /// <summary>
        /// send forward command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Forward(int distance)
        {
            return Fly("forward", distance);
        }

        /// <summary>
        /// send back command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Back(int distance)
        {
            return Fly("back", distance);
        }

        /// <summary>
        /// send left command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Left(int distance)
        {
            return Fly("left", distance);
        }

        /// <summary>
        /// send right command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Right(int distance)
        {
            return Fly("right", distance);
        }

        /// <summary>
        /// send rotate (clockwise, counterclockwise) command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Rotate(string direction, int degree)
        {
            TelloResponse response = SendMessage($"{direction} {degree}", waitForResponse: true, timeOutMs: GetTimeoutByAngle(degree));
            return response.Ok;
        }

        /// <summary>
        /// send clockwise command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Clockwise(int degree)
        {
            return Rotate("cw", degree);
        }

        /// <summary>
        /// send counterclockwise command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool CounterClockwise(int degree)
        {
            return Rotate("ccw", degree);
        }

        /// <summary>
        /// send flip (left, right, forward, back) command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Flip(string direction)
        {
            TelloResponse response = SendMessage($"flip {direction}", waitForResponse: true, timeOutMs: 5000);
            return response.Ok;
        }

        /// <summary>
        /// send FlipLeft command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool FlipLeft()
        {
            return Flip("l");
        }

        /// <summary>
        /// send FlipRight command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool FlipRight()
        {
            return Flip("r");
        }

        /// <summary>
        /// send FlipForward command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool FlipForward()
        {
            return Flip("f");
        }

        /// <summary>
        /// send FlipBack command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool FlipBack()
        {
            return Flip("b");
        }

        /// <summary>
        /// Send RC control via four channels
        /// </summary>
        /// <param name="left_right_velocity">-100~100 (left/right)</param>
        /// <param name="forward_backward_velocity">-100~100 (backward/forward)</param>
        /// <param name="up_down_velocity">-100~100 (down/up)</param>
        /// <param name="yaw_velocity">-100~100 (yaw)</param>
        /// <param name="context">optional command context for logging/debugging</param>
        public bool SendRCControl(int left_right_velocity, int forward_backward_velocity, int up_down_velocity, int yaw_velocity, string context = null)
        {
            string cmd = $"rc {left_right_velocity} {forward_backward_velocity} {up_down_velocity} {yaw_velocity}";
            // send command without waiting for response
            TelloResponse response = SendMessage(cmd, false);
            return response.Ok;
        }

        /// <summary>
        /// send streamon command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool StreamOn()
        {
            TelloResponse response = SendMessage($"streamon");
            _streaming = response.Ok;
            return response.Ok;
        }

        /// <summary>
        /// send streamoff command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool StreamOff()
        {
            TelloResponse response = SendMessage($"streamoff");
            if (response.Ok) _streaming = false;
            return response.Ok;
        }

        /// <summary>
        /// save a picture asynchronously
        /// </summary>
        /// <param name="fileName">the file path/name for the photo</param>
        public void SavePhoto(string fileName)
        {
            if (VideoRecording || VideoStreaming) return;

            if (!_streaming) StreamOn();
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited) _ffmpegProcess.Kill();
            string msg = $"Saving photo to file: {fileName}";
            Log.Action(msg);
            if (CommandCallback != null) CommandCallback(msg);
            StartFFmpegProcess("-frames:v 1", fileName);
        }

        /// <summary>
        /// start/stop recording a video asynchronously
        /// </summary>
        /// <param name="filePath">the file path/name for the video</param>
        public void StartOrStopVideoRecording(string filePath)
        {
            if (VideoRecording) StopVideoRecording();
            else StartVideoRecording(filePath);
        }

        /// <summary>
        /// start recording a video asynchronously
        /// </summary>
        /// <param name="filePath">the file path/name for the video</param>
        public void StartVideoRecording(string filePath)
        {
            if (!VideoRecording)
            {
                StartFFmpegVideo($"Saving video to file: {filePath}", filePath);
                VideoRecording = true;
            }
        }

        /// <summary>
        /// stop the video recording
        /// </summary>
        public void StopVideoRecording()
        {
            if (VideoRecording)
            {
                StopFFmpegVideo();
                if (CommandCallback != null) CommandCallback("Stopped video recording");
                VideoRecording = false;
                if (VideoStreaming)
                {
                    StartFFmpegVideo($"Re-starting video streaming", null);
                }
            }
        }

        /// <summary>
        /// start/stop stream video asynchronously
        /// </summary>
        public void StartOrStopVideoStreaming()
        {
            if (VideoStreaming) StopVideoStreaming();
            else StartVideoStreaming();
        }

        /// <summary>
        /// start stream video asynchronously
        /// </summary>
        public void StartVideoStreaming()
        {
            if (!VideoStreaming)
            {
                StartFFmpegVideo($"Starting video streaming", null);
                VideoStreaming = true;
            }
        }

        /// <summary>
        /// stop stream video
        /// </summary>
        public void StopVideoStreaming()
        {
            if (VideoStreaming)
            {
                if (!VideoRecording)
                {
                    StopFFmpegVideo();
                    if (CommandCallback != null) CommandCallback("Stopped video streaming");
                }
                VideoStreaming = false;
            }
        }

        /// <summary>
        /// the response from Tello drone after SendMessage
        /// </summary>
        protected class TelloResponse
        {
            public bool Ok { get; set; }
            public string Response { get; set; }
            public string Exception { get; set; }
            public int ErrorCode { get; set; }
        }

        /// <summary>
        /// send a message to drone and wait for response if waitForResponse is true
        /// </summary>
        /// <param name="message">the command or query string to be sent to drone</param>
        /// <param name="waitForResponse">whether to wait for the drone's response</param>
        /// <param name="timeOutMs">time in millisecond to wait for response</param>
        /// <exception cref=""></exception>
        /// <returns>return SendResponse object.</returns>
        protected TelloResponse SendMessage(string message, bool waitForResponse = true, int timeOutMs = 2000, string expectedResponse = "ok", string context = null)
        {
            TelloResponse result = new TelloResponse();
            try
            {
                Log.Action($"Send message: {message}");
                if (CommandCallback != null) CommandCallback(context+message);
                _udpMessageClient.Connect(_telloIpAddress, _messageUdpPort);
                Byte[] sendBytes = Encoding.ASCII.GetBytes(message);

                _udpMessageClient.Send(sendBytes, sendBytes.Length);

                if (waitForResponse)
                {
                    _udpMessageClient.Client.ReceiveTimeout = timeOutMs;
                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = _udpMessageClient.Receive(ref _remoteMessageIpEndPoint);
                    string response = Encoding.ASCII.GetString(receiveBytes);
                    result.Response = response;
                    Log.Info($"Received response: {response}");
                    if (CommandResultCallback != null) CommandResultCallback(message, response);
                    if (string.Equals(response, "error auto land", StringComparison.OrdinalIgnoreCase))
                    {
                        string msg = $"Tello error: {response}";
                        Log.Error(msg);
                        throw new Exception(msg);
                    }
                    else if (string.Equals(response, "error motor stop", StringComparison.OrdinalIgnoreCase))
                    {
                        string msg = $"Tello error: {response}";
                        Log.Error(msg);
                        throw new Exception(msg);
                    }
                    else if (string.Equals(response, "unknown command", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Error($"Error: {response}");
                    }
                    else if (string.IsNullOrEmpty(expectedResponse)) result.Ok = true;
                    else result.Ok = string.Equals(expectedResponse, result.Response, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    result.Ok = true;
                    return result;
                }
            }
            catch (Exception err)
            {
                result.Ok = false;
                SocketException socketException = err as SocketException;
                if (socketException != null)
                {
                    result.ErrorCode = socketException.ErrorCode;
                }
                result.Exception = err.Message;
                Log.Error($"Exception: {err.ToString()}");
                if (CommandResultCallback != null) CommandResultCallback(message, err.Message);
            }
            return result;
        }

        private void StartTelloStateTask()
        {
            _updateState = true;
            _udpStateClient = new UdpClient(_stateUdpPort);
            _remoteStateIpEndPoint = new IPEndPoint(IPAddress.Any, _stateUdpPort);
            //_udpStateClient.Connect(_remoteStateIpEndPoint);
            _udpStateClient.Client.ReceiveTimeout = 10000;
            Task.Run( () =>
            {
                Log.Action("Started task to update Tello state ...");
                while (_updateState)
                {
                    UpdateTelloState();
                }
                _udpStateClient.Close();
                Log.Action("Stopped task to update Tello state.");
            });
        }

        private void UpdateTelloState()
        {
            try
            {
                Byte[] receiveBytes = _udpStateClient.Receive(ref _remoteStateIpEndPoint);
                string response = Encoding.ASCII.GetString(receiveBytes);
                //Log.Info($"Tello state: {response}");
                string[] parts = response.Trim().Split(StateDelimiter, StringSplitOptions.RemoveEmptyEntries);
                foreach (string item in parts)
                {
                    try
                    {
                        string[] keyvalue = item.Split(StateValueDelimiter);
                        string key = keyvalue[0];
                        string value = keyvalue[1];
                        if (_droneState.ContainsKey(key))
                        {
                            _droneState[key] = value;
                        }
                        else
                        {
                            _droneState.Add(key, value);
                        }
                    }
                    catch (Exception)
                    { }
                }
            }
            catch (Exception ex)
            {
            // todo:
            }
        }

        protected int GetTimeoutByDistance(int distance)
        {
            return (int)((distance / _speed) * 1000) + 2000;    // in millisecond
        }

        protected int GetTimeoutByAngle(int degree)
        {
            // todo: verify whether this is impacted by _speed 
            return (int)((degree / 30) * 1000) + 2000;    // in millisecond
        }

        protected void StartFFmpegVideo(string context, string videoFile)
        {
            if (!_streaming) StreamOn();
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited) _ffmpegProcess.Kill();
            //else
            {
                Log.Action(context);
                if (CommandCallback != null) CommandCallback(context);
                string args = string.Empty;
                if (!string.IsNullOrEmpty(videoFile)) args = $"{videoFile} ";
                // output to video file plus streaming
                args = $"{args} -f sdl Tello";
                StartFFmpegProcess(args);
            }
        }

        protected void StopFFmpegVideo()
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited) _ffmpegProcess.Kill();
        }

        protected void StartFFmpegProcess(string options, string output)
        {
            StartFFmpegProcess($"{options} {output}");
        }

        protected void StartFFmpegProcess(string outputArguments)
        {
            string arguments = $"-i udp://0.0.0.0:{_videoUdpPort} {outputArguments}";
            ProcessStartInfo info = new ProcessStartInfo()
            {
                FileName = FFmpegPath,
                Arguments = arguments,
                UseShellExecute = true,
                //LoadUserProfile = true,
                //WindowStyle = ProcessWindowStyle.Minimized
            };
            if (DebugMode)
            {
                info.UseShellExecute = false;
                info.RedirectStandardOutput = true;
                //info.WindowStyle = ProcessWindowStyle.Normal;
            }
            Log.Action($"Starting: ffmpeg {arguments}");
            _ffmpegProcess = Process.Start(info);
            //_ffmpegProcess.WaitForExit();
        }

        protected string _telloIpAddress;
        protected int _messageUdpPort = TelloMessageUdpPort;    // the out-going message port for command and query
        protected int _stateUdpPort = TelloStateUdpPort;        // the inbound port for Tello's state

        // video address and port
        protected string _videoIpAddress = "0.0.0.0";
        protected int _videoUdpPort = TelloVideoUdpPort;        // the inbound port for Tello's video

        protected UdpClient _udpMessageClient;
        protected IPEndPoint _remoteMessageIpEndPoint;

        protected UdpClient _udpStateClient;
        protected IPEndPoint _remoteStateIpEndPoint;
        private static readonly char[] StateDelimiter = { ';' };
        private static readonly char[] StateValueDelimiter = { ':' };

        // drone status variables
        protected float _speed = 100;           // speed setting for the drone (10 - 100)
        protected bool _updateState;
        protected bool _streaming;
        protected bool _recording;
        protected Dictionary<string, string> _droneState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // for launch external ffmpeg process
        protected Process _ffmpegProcess;
    }
}
