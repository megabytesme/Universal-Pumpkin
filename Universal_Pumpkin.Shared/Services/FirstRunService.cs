using Windows.Storage;

namespace Universal_Pumpkin.Services
{
    public static class FirstRunService
    {
        private const string Key = "IsFirstRun";

        public static bool IsFirstRun
        {
            get
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                return !localSettings.Values.ContainsKey(Key);
            }
        }

        public static void MarkAsCompleted()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[Key] = false;
        }
    }
}