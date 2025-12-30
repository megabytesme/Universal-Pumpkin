using Windows.Foundation.Metadata;
using Windows.Storage;

namespace Universal_Pumpkin.Services
{
    public static class OSHelper
    {
        public static bool IsWindows11Host => ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 13);

        private static bool ForceWindows10
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Values.TryGetValue("ForceWindows10", out object val))
                {
                    return val is bool b && b;
                }
                return false;
            }
        }

        public static bool IsWin11Mode => IsWindows11Host && !ForceWindows10;

        public static void SetLegacyMode(bool enabled)
        {
            ApplicationData.Current.LocalSettings.Values["ForceWindows10"] = enabled;
        }
    }
}