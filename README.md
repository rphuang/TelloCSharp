# TelloCSharp
This is a simple C# package that controls DJI Tello drone. It contains:
* C# API for Tello drone - class Tello.cs, TelloCmd.cs
* C# UI app to control Tello drone using WPF 
* C# console app to control Tello drone using command line or file that contains commands

# Getting Started for Tello API class
1. download/clone the TelloCSharp respository and LibsCSharp respository (https://github.com/rphuang/LibsCSharp)
2. (Optional) Install ffmpeg if you want to use photo, video, streaming functions. https://ffmpeg.org/download.html
3. Just reference the proj or simply copy the Tello.cs/TelloCmd.cs files.

# Getting Started for TelloWin or TelloCmd app
1. download/clone the TelloCSharp respository and LibsCSharp respository (https://github.com/rphuang/LibsCSharp)
2. build using the VS .sln/*.csproj. Note that the sln/csproj assume that LibsCSharp is in folder "..\LibsCSharp" relative to TelloCSharp folder.
3. (Optional) Install ffmpeg if you want to use photo, video, streaming functions. https://ffmpeg.org/download.html
4. Start Tello drone and connect to its WiFi
5. Run command: TelloWin or TelloCmd

# Configurations
Use the TelloConfig.txt file in either TelloWin or TelloCmd to configure the app.
* Window.Top - the top of the main window
* Window.Left - the left of the main window
* Window.Height - the height of the main window
* Window.Width - the width of the main window
* DefaultDroneSpeed - default drone speed
* DefaultPhotoFolder - default folder for saving photo file
* DefaultVideoFolder - default folder for saving video file
* Video.Streaming - whether to start video streaming after connect
* Video.Redording - whether to start video recording after connect
* DebugMode - debug mode (use this to degun ffmpeg)
* FFmpegPath - the full path/name for ffmpeg program 

# TelloWin UI Usage
There are three sections on the UI
* Buttons on the top - the specified action/command will be performed when the button is clicked.
* The section in the middle is used to display commands to the drone (left) and inputs/status on the right. The inputs are:
    * Speed - the speed will be sent to the drone and used for all the buttons on the bottom section to control the flight.
* Buttons on the bottom section - these buttons will send corresponding command to Tello when they are clicked and command will be cleared when the button is released.

# TelloCmd Usage
On starting, TelloCmd will connect to the Tello drone by sending "command" to the drone. Once connected, it prompts for user inputs to control Tello drone. Available commands:
* 0 or takeoff - send takeoff command to Tello drone
* 1 or land - send land command to Tello drone
* 2 or end - this instructs telloCmd to exit and send necessary commands to the drone (such as land, streamoff).
* p or photo - take a picture and save to file. The file name is yyyy-MMdd-HHmmss-ff.jpg under the current folder.
* v or video - start/stop video recording. The video file is yyyy-MMdd-HHmmss-ff.avi under the current folder.
* s or stream - start/stop video streaming in a separate window.
* run <file> - load and execute commands from file. If no file is specified telloCmd load from telloCommands.txt. The run command can be nested but there is no check for infinite loop. See examples in the files under samples folder.
* sleep <sec> - sleep in seconds. The default value is 1.0. This is useful in the command file.
* help - print help menu
* enter a valid Tello commands like "up 20", "left 50", "cw 90", "flip l". Available commands are defined in: https://dl-cdn.ryzerobotics.com/downloads/tello/20180910/Tello%20SDK%20Documentation%20EN_1.3.pdf

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
* TelloCmd - this is the console app that takes user inputs and excute the command. The samples folder contains examples of command file that can be loaded/run by TelloCmd.
* TelloWin - this is the UI app to control Tello drone. It is based on Windows WPF.

# Troubleshoot
* Cannot take photo or record video but streaming works file - make sure that the current folder has permission granted to Users.
* To troubleshoot ffmpeg problems, change the DebugMode to True in TelloConfig.txt file to enable the ffmpeg output to the console.

