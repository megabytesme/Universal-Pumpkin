using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Universal_Pumpkin.Models;
using Newtonsoft.Json;

namespace Universal_Pumpkin
{
    public class PumpkinController
    {
        private const string DllName = "pumpkin_uwp.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallback(IntPtr message);

#pragma warning disable IDE1006
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pumpkin_register_logger(LogCallback cb);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pumpkin_run_from_config_dir(string path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pumpkin_request_stop();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pumpkin_inject_command(string command);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pumpkin_get_players_json();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pumpkin_get_metrics_json();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pumpkin_free_string(IntPtr ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pumpkin_get_completions_json(string input);
#pragma warning restore IDE1006

        public bool IsRunning { get; private set; }
        public event EventHandler<string> OnLogReceived;
        public event EventHandler<int> OnServerStopped;

        private static LogCallback _loggerDelegate;

        private readonly HashSet<string> _pendingDeops = new HashSet<string>();

        public PumpkinController()
        {
            if (_loggerDelegate == null)
            {
                _loggerDelegate = new LogCallback(OnLogCallback);
                try
                {
                    NativeProbe.TryLoadPumpkin();
                    pumpkin_register_logger(_loggerDelegate);
                }
                catch { /* Handle load error */ }
            }
        }

        public Task StartServerAsync()
        {
            if (IsRunning) return Task.CompletedTask;

            IsRunning = true;
            GlobalLogEvent += OnGlobalLog;

            var folder = ApplicationData.Current.LocalFolder;

            Task.Run(async () =>
            {
                int result = pumpkin_run_from_config_dir(folder.Path);

                if (_pendingDeops.Count > 0)
                {
                    try
                    {
                        var ops = await ManagementHelper.LoadOps();
                        int removed = ops.RemoveAll(x => _pendingDeops.Contains(x.Name));
                        if (removed > 0)
                        {
                            await ManagementHelper.SaveOps(ops);
                            OnGlobalLog(this, $"[System] Processed {removed} pending deops during shutdown.");
                        }
                    }
                    catch { /* Ignore IO errors on shutdown */ }
                }

                IsRunning = false;
                GlobalLogEvent -= OnGlobalLog;
                OnServerStopped?.Invoke(this, result);
            });

            return Task.CompletedTask;
        }

        public void QueueOfflineDeop(string username)
        {
            lock (_pendingDeops)
            {
                _pendingDeops.Add(username);
            }
        }

        public bool IsDeopQueued(string username)
        {
            lock (_pendingDeops) return _pendingDeops.Contains(username);
        }

        public List<PlayerData> GetPlayers()
        {
            if (!IsRunning) return new List<PlayerData>();

            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                jsonPtr = pumpkin_get_players_json();
                if (jsonPtr == IntPtr.Zero) return new List<PlayerData>();

                string json = Marshal.PtrToStringAnsi(jsonPtr);
                pumpkin_free_string(jsonPtr);
                jsonPtr = IntPtr.Zero;

                if (string.IsNullOrEmpty(json)) return new List<PlayerData>();

                return JsonConvert.DeserializeObject<List<PlayerData>>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching players: {ex.Message}");
                return new List<PlayerData>();
            }
        }

        public ServerMetrics GetMetrics()
        {
            if (!IsRunning) return null;

            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                jsonPtr = pumpkin_get_metrics_json();
                if (jsonPtr == IntPtr.Zero) return null;

                string json = Marshal.PtrToStringAnsi(jsonPtr);
                pumpkin_free_string(jsonPtr);

                if (string.IsNullOrEmpty(json) || json == "{}") return null;

                return JsonConvert.DeserializeObject<ServerMetrics>(json);
            }
            catch { return null; }
        }

        public void StopServer() => pumpkin_request_stop();
        public void SendCommand(string command) => pumpkin_inject_command(command);

        private static void OnLogCallback(IntPtr messagePtr)
        {
            string message = Marshal.PtrToStringAnsi(messagePtr);
            GlobalLogEvent?.Invoke(null, message);
        }

        private System.Text.StringBuilder _logHistory = new System.Text.StringBuilder();
        public static event EventHandler<string> GlobalLogEvent;
        private void OnGlobalLog(object sender, string msg)
        {
            lock (_logHistory)
            {
                _logHistory.AppendLine(msg);
            }
            OnLogReceived?.Invoke(this, msg);
        }

        public string GetLogHistory()
        {
            lock (_logHistory)
            {
                return _logHistory.ToString();
            }
        }

        public async Task<List<CommandSuggestion>> GetSuggestionsAsync(string input)
        {
            if (!IsRunning || string.IsNullOrWhiteSpace(input)) return new List<CommandSuggestion>();

            return await Task.Run(() =>
            {
                IntPtr jsonPtr = IntPtr.Zero;
                try
                {
                    jsonPtr = pumpkin_get_completions_json(input);
                    if (jsonPtr == IntPtr.Zero) return new List<CommandSuggestion>();

                    string json = Marshal.PtrToStringAnsi(jsonPtr);
                    pumpkin_free_string(jsonPtr);

                    return JsonConvert.DeserializeObject<List<CommandSuggestion>>(json)
                           ?? new List<CommandSuggestion>();
                }
                catch { return new List<CommandSuggestion>(); }
            });
        }
    }
}