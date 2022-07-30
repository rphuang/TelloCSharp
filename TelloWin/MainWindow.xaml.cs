using SettingsLib;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using tellocs;

namespace TelloWin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            _tello = new TelloCmd();
            _tello.CommandCallback += ShowCommand;
            _tello.CommandResultCallback += ShowCommanResult;

            InitializeComponent();

            // load settings
            string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TelloConfig.txt");
            Settings settings = new Settings(settingsFile);
            settings.LoadSettings();
            // position/size MainWindow 
            Top = settings.GetOrAddSetting<double>("Window.Top", 0);
            Left = settings.GetOrAddSetting<double>("Window.Left", 0);
            Height = settings.GetOrAddSetting<double>("Window.Height", 900);
            Width = settings.GetOrAddSetting<double>("Window.Width", 640);
            // drone settings
            SetSpeed(settings.GetOrAddSetting<int>("DefaultDroneSpeed", 50));
            _tello.DefaultPhotoFolder = settings.GetOrAddSetting<string>("DefaultPhotoFolder", string.Empty);
            _tello.DefaultVideoFolder = settings.GetOrAddSetting<string>("DefaultVideoFolder", string.Empty);
            _tello.FFmpegPath = settings.GetOrAddSetting<string>("FFmpegPath", "ffmpeg.exe");
            _tello.DebugMode = settings.GetOrAddSetting<bool>("DebugMode", false);
            _tello.UseTelloSpeedForRCControl = true;
            // whether to start video streaming/recording after connect
            _videoRecordingWhenConnected = settings.GetOrAddSetting<bool>("Video.Redording", false);
            _videoStreamingWhenConnected = settings.GetOrAddSetting<bool>("Video.Streaming", false);

            _updateStatus = true;
            UpdateStatusAsync();
        }

        private TelloCmd _tello;
        private int _speed;
        private int[] _rcChannels = { 0, 0, 0, 0 };     // 4 channels
        private bool _updateStatus;
        private bool _videoRecordingWhenConnected;
        private bool _videoStreamingWhenConnected;

        private void SetSpeed(int speed)
        {
            _speed = speed;
            speedTxtBox.Text = speed.ToString();
            if (_tello.Connected) _tello.Speed = speed;
        }

        private void SendRCControl(Button button)
        {
            string tag = button.Tag.ToString();
            string[] parts = tag.Split(',');
            bool send = false;
            // todo: keep the 1 for each channel (OR logic)
            for (int jj = 0; jj < 4; jj++)
            {
                int val = (int)(_speed * float.Parse(parts[jj]));
                if (val != _rcChannels[jj])
                {
                    _rcChannels[jj] = val;
                    send = true;
                }
            }
            if (send && _tello.Connected)
            {
                _tello.SendRCControl(_rcChannels[0], _rcChannels[1], _rcChannels[2], _rcChannels[3], button.Content.ToString());
            }
        }

        private void StopRCControl()
        {
            bool send = false;
            for (int jj = 0; jj < 4; jj++)
            {
                if (_rcChannels[jj] != 0)
                {
                    _rcChannels[jj] = 0;
                    send = true;
                }
            }
            if (send && _tello.Connected)
            {
                _tello.SendRCControl(0, 0, 0, 0, "Stop");
            }
        }

        private async void UpdateStatusAsync()
        {
            while (_updateStatus)
            {
                try
                {
                    DisplayStatus();
                }
                catch (Exception err)
                {
                    // todo: debug exception
                }
                await Task.Run(async () =>
                {
                    //int delay = (int)(DeviceSettings.Instance.MonitorRefreshRate * 1000);
                    await Task.Delay(500);   // in milliseconds
                }).ConfigureAwait(true);
            }
        }

        private void DisplayStatus()
        {
            if (_tello.Connected)
            {
                btnConnect.Background= Brushes.Green;
                if (_tello.Flying) statusLabel.Content = "Flying";
                else statusLabel.Content = "Connected";
                heightLabel.Content = _tello.DroneState<string>("h");
                batteryLabel.Content = _tello.DroneState<string>("bat");
                flightTimeLabel.Content = _tello.DroneState<string>("time");
                tofLabel.Content = _tello.DroneState<string>("tof");
                temperatureLabel.Content = (_tello.DroneState<int>("templ") + _tello.DroneState<int>("temph")) / 2;
                if (_tello.VideoStreaming || _tello.VideoRecording) btnStreaming.Background = Brushes.Green;
                else btnStreaming.Background = Brushes.LightGray;
                if (!_tello.VideoRecording) btnVideo.Background = Brushes.LightGray;
                else btnVideo.Background = Brushes.Green;
            }
            else
            {
                btnConnect.Background= Brushes.LightGray;
                btnStreaming.Background = Brushes.LightGray;
                btnVideo.Background = Brushes.LightGray;
                statusLabel.Content = "Not Connected";
                heightLabel.Content = "??";
                batteryLabel.Content = "??";
                flightTimeLabel.Content = "??";
                tofLabel.Content = "??";
                temperatureLabel.Content = "??";
            }
        }

        private void ShowCommand(string cmd)
        {
            ListBox listBox = commandListBox;
            listBox.Items.Add($"{listBox.Items.Count} {cmd}");
            listBox.SelectedIndex = listBox.Items.Count - 1;
            listBox.ScrollIntoView(listBox.SelectedItem);
        }

        private void ShowCommanResult(string cmd, string result)
        {
            if (!string.IsNullOrEmpty(result)) result = result.Trim();
            int index = commandListBox.Items.Count - 1;
            string lastCmd = commandListBox.Items[index].ToString();
            if (lastCmd.StartsWith($"{index} {cmd}"))
            {
                commandListBox.Items[index] = $"{lastCmd} => {result}";
            }
            else
            {
                ShowCommand($"{cmd} => {result}");
            }
        }

        // generic handler to set Tello speed using Button.Tag
        private void btnSpeed_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int speed = int.Parse(btn.Tag.ToString());
            SetSpeed(speed);
        }

        // generic PreviewMouseDown handler to send RC control speed using Button.Tag
        private void btn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            SendRCControl(sender as Button);
        }

        // generic PreviewMouseUp handler to send RC control speed using Button.Tag
        private void btn_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            StopRCControl();
        }

        // generic PreviewTouchDown handler to send RC control speed using Button.Tag
        private void btn_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            SendRCControl(sender as Button);
        }

        // generic PreviewTouchUp handler to send RC control speed using Button.Tag
        private void btn_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            StopRCControl();
        }


        private void btnLand_Click(object sender, RoutedEventArgs e)
        {
            _tello.Land();
        }

        private void btnTakeoff_Click(object sender, RoutedEventArgs e)
        {
            if (_tello.Connected) _tello.Takeoff();
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_tello.Connected)
            {
                _tello.Disconnect();
            }
            else
            {
                _tello.Connect();
                if (_videoRecordingWhenConnected) _tello.StartOrStopVideoRecording();
                else if (_videoStreamingWhenConnected) _tello.StartOrStopVideoStreaming();
            }
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            _tello.RunCommandFromFile(btn.Tag.ToString());
        }

        private void btnTracking_Click(object sender, RoutedEventArgs e)
        {
            //_tello.FaceTracking();
        }

        private void btnStreaming_Click(object sender, RoutedEventArgs e)
        {
            if (_tello.Connected)
            {
                _tello.StartOrStopVideoStreaming();
            }
        }

        private void btnVideo_Click(object sender, RoutedEventArgs e)
        {
            if (_tello.Connected)
            {
                _tello.StartOrStopVideoRecording();
            }
        }

        private void btnPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_tello.Connected)
            {
                _tello.SavePhoto();
            }
        }

        private void speedTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int spd = int.Parse(speedTxtBox.Text);
            if (spd > 1 && spd <= 100) SetSpeed(spd);
        }
    }
}
