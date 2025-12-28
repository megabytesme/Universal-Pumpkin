using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Runtime.InteropServices;
using Windows.Storage;
using System.Diagnostics;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Universal_Pumpkin
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void StartPumpkin_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Probing…";
            StartPumpkinButton.IsEnabled = false;

            var h = NativeProbe.TryLoadPumpkin();
            if (h == IntPtr.Zero)
            {
                StatusText.Text = "DLL failed to load";
                StartPumpkinButton.IsEnabled = true;
                return;
            }

            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var configPath = folder.Path;
            StatusText.Text = "Server running...";

            int result = await System.Threading.Tasks.Task.Run(() =>
            {
                System.Diagnostics.Debug.WriteLine("[Pumpkin] Starting server loop...");
                return PumpkinNative.pumpkin_run_from_config_dir(configPath);
            });

            System.Diagnostics.Debug.WriteLine("[Pumpkin] Server exited with: " + result);
            StatusText.Text = $"Server stopped. Code: {result}";
            StartPumpkinButton.IsEnabled = true;

            NativeProbe.TryFree(h); 
        }

        private void StopPumpkin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PumpkinNative.pumpkin_request_stop();
                StatusText.Text = "Stop requested";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error: " + ex.Message;
            }
        }

    }

    public static class PumpkinNative
    {
        [DllImport("pumpkin.dll",
            CharSet = CharSet.Ansi,
            CallingConvention = CallingConvention.Cdecl)]
        public static extern int pumpkin_run_from_config_dir(string path);

        [DllImport("pumpkin.dll",
            CallingConvention = CallingConvention.Cdecl)]
        public static extern void pumpkin_request_stop();
    }
}
