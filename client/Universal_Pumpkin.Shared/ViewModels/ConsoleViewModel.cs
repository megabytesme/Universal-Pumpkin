using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Universal_Pumpkin.Models;
using Windows.System;
using Windows.UI.Xaml;

namespace Universal_Pumpkin.ViewModels
{
    public class ConsoleViewModel
    {
        private DispatcherTimer _metricsTimer;

        public event EventHandler<LogEntry> LogReceived;
        public event EventHandler<int> ServerStopped;
        public event EventHandler MetricsUpdated;

        public string RamUsage { get; private set; } = "0 MB";
        public string TpsText { get; private set; } = "20.0";
        public bool IsTpsGood { get; private set; } = true;
        public string MsptText { get; private set; } = "0ms";
        public string LocalIpAddress { get; private set; }
        public ObservableCollection<LogEntry> LogItems { get; } = new ObservableCollection<LogEntry>();
        public ConsoleViewModel()
        {
            _metricsTimer = new DispatcherTimer();
            _metricsTimer.Interval = TimeSpan.FromSeconds(1);
            _metricsTimer.Tick += MetricsTimer_Tick;
        }

        public void OnNavigatedTo()
        {
            App.Server.OnLogReceived += Controller_OnLogReceived;
            App.Server.OnServerStopped += Controller_OnServerStopped;

            if (App.Server.IsRunning)
            {
                LocalIpAddress = IpAddressHelper.GetLocalIpAddress();
                _metricsTimer.Start();
            }
        }

        public void OnNavigatedFrom()
        {
            App.Server.OnLogReceived -= Controller_OnLogReceived;
            App.Server.OnServerStopped -= Controller_OnServerStopped;
            _metricsTimer.Stop();
        }

        public async Task LoadInitialLogAsync()
        {
            var history = await Task.Run(() => App.Server.GetLogHistory());
            if (string.IsNullOrEmpty(history))
                return;

            var lines = history.Split('\n')
                               .Where(l => !string.IsNullOrWhiteSpace(l))
                               .Select(LogParserHelper.Parse)
                               .ToList();

            await Windows.ApplicationModel.Core.CoreApplication.MainView.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Low,
                () =>
                {
                    foreach (var entry in lines)
                        LogItems.Add(entry);
                });
        }

        public async Task StartServerAsync()
        {
            if (NativeProbe.TryLoadPumpkin() == IntPtr.Zero)
                throw new Exception("DLL missing");

            LocalIpAddress = IpAddressHelper.GetLocalIpAddress();
            await App.Server.StartServerAsync();
            _metricsTimer.Start();
        }

        public void StopServer()
        {
            App.Server.StopServer();
        }

        public void SendCommand(string command)
        {
            if (!string.IsNullOrWhiteSpace(command))
            {
                App.Server.SendCommand(command);
            }
        }

        public async Task<List<string>> GetSuggestionsAsync(string text)
        {
            var suggestions = await App.Server.GetSuggestionsAsync(text);
            return suggestions.Select(s => s.Text).ToList();
        }

        private async void MetricsTimer_Tick(object sender, object e)
        {
            if (App.Server.IsRunning)
            {
                ulong memUsage = MemoryManager.AppMemoryUsage / 1024 / 1024;
                RamUsage = $"{memUsage} MB";

                var metrics = await Task.Run(() => App.Server.GetMetrics());

                if (metrics != null)
                {
                    TpsText = metrics.FmtTPS;
                    IsTpsGood = metrics.TPS >= 18.0;
                    MsptText = metrics.FmtMSPT;
                }

                MetricsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Controller_OnLogReceived(object sender, string e)
        {
            var entry = LogParserHelper.Parse(e);
            LogReceived?.Invoke(this, entry);
        }

        private void Controller_OnServerStopped(object sender, int e)
        {
            ServerStopped?.Invoke(this, e);
        }
    }
}