using System;
using System.Threading.Tasks;
using Universal_Pumpkin.Services;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Foundation.Metadata;
using Windows.System;

namespace Universal_Pumpkin.Helpers
{
    public static class BackgroundKeeper
    {
        private static ExtendedExecutionSession _session = null;
        
        public static async Task<bool> RequestKeepAlive()
        {
            var accessStatus = await BackgroundExecutionManager.RequestAccessAsync();

#if UWP1709
            if (accessStatus == BackgroundAccessStatus.AlwaysAllowed ||
                accessStatus == BackgroundAccessStatus.AllowedSubjectToSystemPolicy ||
                accessStatus == BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity ||
                accessStatus == BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity)
            {
                return await StartExtendedSession();
            }
#else
            if (accessStatus == BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity ||
                accessStatus == BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity)
            {
                return await StartExtendedSession();
            }
#endif

            System.Diagnostics.Debug.WriteLine($"[Background] Access Denied: {accessStatus}");
            return false;
        }

        private static async Task<bool> StartExtendedSession()
        {
            if (!ApiInformation.IsTypePresent("Windows.ApplicationModel.ExtendedExecution.ExtendedExecutionSession"))
                return false;

            StopKeepAlive();

            try
            {
                _session = new ExtendedExecutionSession();
                _session.Reason = ExtendedExecutionReason.Unspecified;
                _session.Description = "Hosting Minecraft Server";
                _session.Revoked += Session_Revoked;

                ExtendedExecutionResult result = await _session.RequestExtensionAsync();

                if (result == ExtendedExecutionResult.Allowed)
                {
                    System.Diagnostics.Debug.WriteLine("[Background] Extended Execution Allowed. App will run minimized.");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Background] Extended Execution Denied by OS.");
                    _session.Dispose();
                    _session = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Background] Error: {ex.Message}");
                return false;
            }
        }

        public static void StopKeepAlive()
        {
            if (_session != null)
            {
                _session.Revoked -= Session_Revoked;
                _session.Dispose();
                _session = null;
                System.Diagnostics.Debug.WriteLine("[Background] Extended Execution Stopped.");
            }
        }

        private static void Session_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine($"[Background] Session Revoked! Reason: {args.Reason}");
            StopKeepAlive();
        }
        
        public static async Task OpenBackgroundSettings()
        {
            if (OSHelper.IsWindows11)
            {
                string pfn = Windows.ApplicationModel.Package.Current.Id.FamilyName;
                var uri = new Uri($"ms-settings:appsfeatures-app?{pfn}");
                _ = Windows.System.Launcher.LaunchUriAsync(uri);
            }
            else
            {
                var uri = new Uri("ms-settings:privacy-backgroundapps");
                _ = Windows.System.Launcher.LaunchUriAsync(uri);
            }
        }
    }
}