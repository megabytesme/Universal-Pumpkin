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
        private const string DllName = "pumpkin.dll";

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
        private static extern void pumpkin_free_string(IntPtr ptr);
#pragma warning restore IDE1006

        public bool IsRunning { get; private set; }
        public event EventHandler<string> OnLogReceived;
        public event EventHandler<int> OnServerStopped;

        private static LogCallback _loggerDelegate;

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

        public async Task StartServerAsync()
        {
            if (IsRunning) return;
            IsRunning = true;

            GlobalLogEvent += OnGlobalLog;
            var folder = ApplicationData.Current.LocalFolder;

            await Task.Run(() =>
            {
                int result = pumpkin_run_from_config_dir(folder.Path);
                IsRunning = false;
                GlobalLogEvent -= OnGlobalLog;
                OnServerStopped?.Invoke(this, result);
            });
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
    }
}