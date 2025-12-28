using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;

namespace Universal_Pumpkin
{
    public class PumpkinController
    {
        private const string DllName = "pumpkin.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallback(IntPtr message);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pumpkin_register_logger(LogCallback cb);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pumpkin_run_from_config_dir(string path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pumpkin_request_stop();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void pumpkin_inject_command(string command);

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
                    var h = NativeProbe.TryLoadPumpkin();
                    if (h != IntPtr.Zero) pumpkin_register_logger(_loggerDelegate);
                }
                catch { /* Handle load error if needed */ }
            }
        }

        private static void OnLogCallback(IntPtr messagePtr)
        {
            string message = Marshal.PtrToStringAnsi(messagePtr);
            GlobalLogEvent?.Invoke(null, message);
        }

        public static event EventHandler<string> GlobalLogEvent;

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

        private void OnGlobalLog(object sender, string msg)
        {
            OnLogReceived?.Invoke(this, msg);
        }

        public void StopServer() => pumpkin_request_stop();
        public void SendCommand(string command) => pumpkin_inject_command(command);
    }
}