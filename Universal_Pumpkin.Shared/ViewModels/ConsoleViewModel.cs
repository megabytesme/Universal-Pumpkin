using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Universal_Pumpkin.Helpers;
using Universal_Pumpkin.Models;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Universal_Pumpkin.ViewModels
{
    public class ConsoleViewModel
    {
        private DispatcherTimer _metricsTimer;

        public event EventHandler<LogEntry> LogReceived;
        public event EventHandler<int> ServerStopped;
        public event EventHandler MetricsUpdated;

        private readonly ConcurrentQueue<LogEntry> _pendingLogs = new ConcurrentQueue<LogEntry>();
        private bool _isProcessingLogs = false;

        public ObservableCollection<LogEntry> AllLogs { get; } = new ObservableCollection<LogEntry>();
        public ObservableCollection<LogEntry> VisibleLogs { get; } = new ObservableCollection<LogEntry>();

        public string CurrentSearchQuery = "";
        public HashSet<string> EnabledLevels = new HashSet<string> { "INFO", "WARN", "ERROR", "DEBUG" };

        public string RamUsage { get; private set; } = "0 MB";
        public string TpsText { get; private set; } = "20.0";
        public bool IsTpsGood { get; private set; } = true;
        public string MsptText { get; private set; } = "0ms";
        public string LocalIpAddress { get; private set; }

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
            var lines = string.IsNullOrEmpty(history)
                ? new List<LogEntry>()
                : history.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(LogParserHelper.Parse)
                         .ToList();

            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;

            await dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Low,
                () =>
                {
                    AllLogs.Clear();
                    VisibleLogs.Clear();

                    foreach (var entry in lines)
                    {
                        AllLogs.Add(entry);
                        if (PassesFilter(entry))
                            VisibleLogs.Add(entry);
                    }
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
            if (string.IsNullOrWhiteSpace(e))
                return;

            string raw = e.TrimEnd('\r', '\n');

            var entry = LogParserHelper.Parse(raw);
            LogReceived?.Invoke(this, entry);
        }

        private void Controller_OnServerStopped(object sender, int e)
        {
            ServerStopped?.Invoke(this, e);
        }

        public void EnqueueLog(LogEntry entry)
        {
            _pendingLogs.Enqueue(entry);
            ProcessLogQueue();
        }

        private async void ProcessLogQueue()
        {
            if (_isProcessingLogs)
                return;

            _isProcessingLogs = true;

            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;

            await dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    while (_pendingLogs.TryDequeue(out var entry))
                    {
                        AllLogs.Add(entry);
                        if (PassesFilter(entry))
                            VisibleLogs.Add(entry);
                    }
                });

            _isProcessingLogs = false;
        }

        public void ApplyFilter()
        {
            VisibleLogs.Clear();

            foreach (var entry in AllLogs)
            {
                if (PassesFilter(entry))
                    VisibleLogs.Add(entry);
            }
        }

        private bool PassesFilter(LogEntry entry)
        {
            if (!EnabledLevels.Contains(entry.Level))
                return false;

            if (!string.IsNullOrWhiteSpace(CurrentSearchQuery))
            {
                var q = CurrentSearchQuery;

                bool messageContains = entry.Message != null &&
                                       entry.Message.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

                bool levelContains = entry.Level != null &&
                                     entry.Level.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!messageContains && !levelContains)
                    return false;
            }

            return true;
        }
    }
}