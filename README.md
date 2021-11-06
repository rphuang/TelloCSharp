# TelloCSharp
This is a simple C# package that controls DJI Tello drone. It contains:
* C# API for Tello drone - class Tello.cs
* C# console app to control Tello drone using command line or file that contains commands.

# Getting Started for Tello API class
1. download/clone the respository.
2. (Optional) Install ffmpeg if you want to use photo, video, streaming functions. Add the installed forlder to PATH env variable. https://ffmpeg.org/download.html
3. Just reference the proj or simply copy the Tello.cs file.

# Getting Started for TelloCmd console app
1. download/clone the respository.
2. build using the VS .sln/*.csproj
3. (Optional) Install ffmpeg if you want to use photo, video, streaming functions. Add the installed forlder to PATH env variable. https://ffmpeg.org/download.html
4. Start Tello drone and connect to its WiFi
5. Run command: TelloCmd

# TelloCmd Usage
On starting, TelloCmd will connect to the Tello drone by sending "command" to the drone. Once connected, it prompts for user inputs to control Tello drone. Available commands:
* 0 or takeoff - send takeoff command to Tello drone
* 1 or land - send land command to Tello drone
* 2 or end - this instructs telloCmd to exit and send necessary commands to the drone (such as land, streamoff).
* p or photo - take a picture and save to file. The file name is yyyy-mmdd-hhmmss.png under the current folder.
* v or video - start/stop video recording. The video file is yyyy-mmdd-hhmmss.avi under the current folder.
* s or stream - start/stop video streaming in a separate window.
* run <file> - load and execute commands from file. If no file is specified telloCmd load from telloCommands.txt. The run command can be nested but there is no check for infinite loop. See examples in the files under samples folder.
* sleep <sec> - sleep in seconds. The default value is 1.0. This is useful in the command file.
* help        - print help menu
* enter a valid Tello commands like "up 20", "left 50", "cw 90", "flip l". Available commands is defined in: https://dl-cdn.ryzerobotics.com/downloads/tello/20180910/Tello%20SDK%20Documentation%20EN_1.3.pdf

# Tello class Usage
Sample code:
```
    Tello tello = new Tello();
    tello.Connect();
	tello.Takeoff();
	tello.StartOrStopVideoStreaming();
	tello.Forward(50);
	tello.Up(50);
	tello.FlipForward();
	tello.Clockwise(360);
	tello.Left(100);
	tello.StartOrStopVideoStreaming();
	tello.Land();
```

# C# Projects
* Tello - Tello (Tello.cs) is a simple API to control Tello drone. Besides the Tello commands and queries, it adds the basic support for taking photo, video, and streaming using ffmpeg.
* TelloCmd - this is the console app that takes user inputs and excute the command.
* LogLib - contains log utilities. Implement ILog and set Log.Instance to override the default console log or add to Log.Instances to support multiple logs.
* The samples folder contains examples of command file that can be loaded/run by telloCmd.

