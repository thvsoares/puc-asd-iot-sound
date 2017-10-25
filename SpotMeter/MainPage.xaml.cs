using SpotMeter.Domain;
using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SpotMeter
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private AudioMonitor _monitor;
        private SpotifyVolumeController _spotifyVolumeController;

        public MainPage()
        {
            this.InitializeComponent();
            _monitor = new AudioMonitor();
            _monitor.OnNotify += Monitor_OnNotify;
            _monitor.OnAverageLevelChanged += Monitor_OnAverageLevelChanged;
            _spotifyVolumeController = new SpotifyVolumeController();
        }

        private async void ChangeVolume(double noiseLevel)
        {
            // Normalize the noise value
            if (MinNoiseLevel.Value != 0 || MaxNoiseLevel.Value != 100)
            {
                double range = (100 - MaxNoiseLevel.Value + MinNoiseLevel.Value) / 100;
                if (range <= 0)
                    range = 1;
                noiseLevel = noiseLevel / range * 100;
                if (noiseLevel > 100)
                    noiseLevel = 100;
            }
            else
            {
                noiseLevel = noiseLevel * 100;
            }
            NoiseLevel.Value = noiseLevel;

            // Update volume if volume + tolerance is out of the noise range
            if (Volume.Value + Delta.Value < noiseLevel)
            {
                if (Volume.Value < 100)
                {
                    Volume.Value++;
                    await _spotifyVolumeController.SetVolume(Convert.ToByte(Volume.Value));
                }
            }
            else if (Volume.Value - Delta.Value > noiseLevel)
            {
                if (Volume.Value > 0)
                {
                    Volume.Value--;
                    await _spotifyVolumeController.SetVolume(Convert.ToByte(Volume.Value));
                }
            }
        }

        private async void Monitor_OnAverageLevelChanged(double obj)
        {
            if (Dispatcher.HasThreadAccess)
            {
                ChangeVolume(obj);
            }
            else
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Monitor_OnAverageLevelChanged(obj));
            }
        }

        private async void Monitor_OnNotify(string obj)
        {
            if (Dispatcher.HasThreadAccess)
            {
                OutputList.Items.Add(obj);
            }
            else
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Monitor_OnNotify(obj));
            }
        }

        private async void ToggleMonitor_Checked(object sender, RoutedEventArgs e)
        {
            if (_monitor.IsMonitoring)
            {
                ToggleMonitor.Label = "Start monitor";
                _monitor.Stop();
            }
            else
            {
                OutputList.Items.Clear();
                ToggleMonitor.Label = "Stop monitor";
                await _monitor.Start();
            }
        }

        private void SpotifyKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            _spotifyVolumeController.Token = SpotifyKey.Text;
        }
    }
}
