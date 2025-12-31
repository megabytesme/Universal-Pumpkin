using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Universal_Pumpkin.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Universal_Pumpkin
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public static PumpkinController Server { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

            Server = new PumpkinController();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (OSHelper.IsWin11Mode)
            {
                try
                {
                    var win11Dict = new ResourceDictionary
                    {
                        Source = new Uri("ms-appx:///Themes/Win11Resources.xaml")
                    };

                    this.Resources.MergedDictionaries.Add(win11Dict);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load WinUI: {ex.Message}");
                }
            }

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    if (FirstRunService.IsFirstRun)
                    {
                        rootFrame.Navigate(NavigationHelper.GetPageType("OOBE"), e.Arguments);
                    }
                    else
                    {
                        // When the navigation stack isn't restored navigate to the first page,
                        // configuring the new page by passing required information as a navigation
                        // parameter
                        Type shellType = NavigationHelper.GetPageType("Shell");
                        rootFrame.Navigate(shellType, e.Arguments);
                    }
                }
                Window.Current.Activate();

                _ = CheckForUpdatesAtStartup();
            }
        }

        private async Task CheckForUpdatesAtStartup()
        {
            var updateInfo = await UpdateService.CheckForUpdatesAsync();

            if (updateInfo.IsUpdateAvailable)
            {
                await Window.Current.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () =>
                {
                    var scrollViewer = new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        MaxHeight = 350
                    };

                    var panel = new StackPanel();

                    var headerText = new TextBlock
                    {
                        Text = $"Version {updateInfo.LatestVersion} is available to download!",
                        FontWeight = Windows.UI.Text.FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 12)
                    };

                    var bodyText = new TextBlock
                    {
                        Text = updateInfo.Body,
                        TextWrapping = TextWrapping.Wrap
                    };

                    panel.Children.Add(headerText);
                    panel.Children.Add(bodyText);
                    scrollViewer.Content = panel;

                    var dialog = new ContentDialog
                    {
                        Title = "Update Available",
                        Content = scrollViewer,
                        PrimaryButtonText = "Download",
                        CloseButtonText = "Skip",
                        DefaultButton = ContentDialogButton.Primary
                    };

                    if (Window.Current.Content is FrameworkElement fe && fe.XamlRoot != null)
                    {
                        dialog.XamlRoot = fe.XamlRoot;
                    }

                    try
                    {
                        var result = await dialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            if (!string.IsNullOrEmpty(updateInfo.ReleaseUrl))
                            {
                                await Windows.System.Launcher.LaunchUriAsync(new Uri(updateInfo.ReleaseUrl));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("[CheckForUpdatesAtStartup] Dialog failed to show");
                    }
                });
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            try
            {
                if (Server != null && Server.IsRunning)
                {
                    await Server.ShutdownSafelyAsync();
                }
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
