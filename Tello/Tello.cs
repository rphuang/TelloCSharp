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
        /// connected to the drone ("command" sent successfully)
        /// </summary>
        public bool Connected { get; private set; }

        /// <summary>
        /// the drone is flying ("takeoff" send successfully)
        /// </summary>
        public bool Flying { get; private set; }

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
            SendResponse response = SendMessage(query, waitForResponse: true, timeOutMs: 1000, expectedResponse: null);
            if (response.Ok) return (T)Convert.ChangeType(response.Response, typeof(T));
            return default(T);
        }

        /// <summary>
        /// send control command to drone
        /// </summary>
        /// <param name="command">the command string sent to drone</param>
        /// <param name="timeOutMs">time in millisecond to wait for response</param>
        /// <returns>the raw response from drone</returns>
        public string Control(string command, int timeOutMs = 2000)
        {
            SendResponse response = SendMessage(command, waitForResponse: true, timeOutMs: timeOutMs, expectedResponse: "ok");
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
                SendResponse response = SendMessage($"speed {value}");
                if (response.Ok) _speed = value;
            }
        }
        
        /// <summary>
        /// connect to the drone
        /// </summary>
        /// <returns>returns true if the drone is connected</returns>
        public bool Connect()
        {
            SendResponse response = SendMessage("command");
            Connected = response.Ok;
            if (Connected)
            {
                // get speed setting to calculate timeout
                float speed = Speed;
                Log.Info($"Connected to Tello: battery={Battery} speed={speed}");
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
            SendResponse response = SendMessage("takeoff", waitForResponse: true, timeOutMs: 10000);
            Flying = response.Ok;
            return Flying;
        }

        /// <summary>
        /// send land command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Land()
        {
            SendResponse response = SendMessage("land", waitForResponse: true, timeOutMs: 10000);
            Flying = !response.Ok;
            return response.Ok;
        }

        /// <summary>
        /// send emergency command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Emergency()
        {
            SendResponse response = SendMessage("emergency", waitForResponse: true, timeOutMs: 10000);
            Flying = !response.Ok;
            return response.Ok;
        }

        /// <summary>
        /// send fly (forward, back, left, right) command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool Fly(string cmd, int distance)
        {
            SendResponse response = SendMessage($"{cmd} {distance}", waitForResponse: true, timeOutMs: GetTimeoutByDistance(distance));
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
            SendResponse response = SendMessage($"{direction} {degree}", waitForResponse: true, timeOutMs: GetTimeoutByAngle(degree));
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
            SendResponse response = SendMessage($"flip {direction}", waitForResponse: true, timeOutMs: 5000);
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
        /// send streamon command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool StreamOn()
        {
            SendResponse response = SendMessage($"streamon");
            _streaming = response.Ok;
            return response.Ok;
        }

        /// <summary>
        /// send streamoff command to the drone
        /// </summary>
        /// <returns>returns true if the drone performs the action successfully</returns>
        public bool StreamOff()
        {
            SendResponse response = SendMessage($"streamoff");
            if (response.Ok) _streaming = false;
            return response.Ok;
        }

        /// <summary>
        /// save a picture asynchronously
        /// </summary>
        /// <param name="fileName">the file path/name for the photo</param>
        public void SavePhoto(string fileName)
        {
            if (!_streaming) StreamOn();
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited) _ffmpegProcess.Kill();
            Log.Action($"Saving photo to file: {fileName}");
            StartFFmpegProcess("-frames:v 1", fileName);
        }

        /// <summary>
        /// recording a video asynchronously
        /// </summary>
        /// <param name="fileName">the file path/name for the video</param>
        public void StartOrStopVideoRecording(string fileName)
        {
            StartOrStopFFmpegVideo($"Saving video to file: {fileName}", string.Empty, fileName);
        }

        /// <summary>
        /// stream video asynchronously
        /// </summary>
        public void StartOrStopVideoStreaming()
        {
            StartOrStopFFmpegVideo($"Starting video streaming", "-f sdl", "Tello");
        }

        /// <summary>
        /// stop the video. called after SaveVideoAsync or ViewVideoAsync
        /// </summary>
        public void StopVideo()
        {
            if (_ffmpegProcess == null || _ffmpegProcess.HasExited) return;
            _ffmpegProcess.Kill();
        }

        /// <summary>
        /// the response from Tello drone after SendMessage
        /// </summary>
        protected class SendResponse
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
        protected SendResponse SendMessage(string message, bool waitForResponse = true, int timeOutMs = 2000, string expectedResponse = "ok")
        {
            SendResponse result = new SendResponse();
            try
            {
                Log.Action($"Send message: {message}");
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
            }
            return result;
        }

        private void StartTelloStateTask()
        {
            _updateState = true;
            _udpStateClient = new UdpClient(_stateUdpPort);
            _remoteStateIpEndPoint = new IPEndPoint(IPAddress.Any, _stateUdpPort);
            //_udpStateClient.Connect(_remoteStateIpEndPoint);
            _udpStateClient.Client.ReceiveTimeout = 5000;
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
            catch (Exception)
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

        protected void StartOrStopFFmpegVideo(string context, string options, string output)
        {
            if (!_streaming) StreamOn();
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited) _ffmpegProcess.Kill();
            else
            {
                Log.Action($"Saving video to file: {output}");
                StartFFmpegProcess(options, output);
            }
        }

        protected void StartFFmpegProcess(string options, string output)
        {
            string arguments = $"-i udp://0.0.0.0:{_videoUdpPort} {options} {output}";
            ProcessStartInfo info = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                UseShellExecute = true,
                //UseShellExecute = false,
                //RedirectStandardOutput = true,
                //LoadUserProfile = true,
                //WindowStyle = ProcessWindowStyle.Minimized
            };
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
