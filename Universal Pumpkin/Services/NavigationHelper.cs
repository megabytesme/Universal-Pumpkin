using System;
using Universal_Pumpkin.Views.Win11;

namespace Universal_Pumpkin.Services
{
    public static class NavigationHelper
    {
        public static Type GetPageType(string pageKey)
        {
            bool useModern = OSHelper.IsWindows11;

            switch (pageKey)
            {
                case "Shell":
                    return useModern ? typeof(ShellPage) : typeof(MainPage);

                case "Console":
                    return useModern ? typeof(ConsolePage_Win11) : typeof(ConsolePage);

                case "Players":
                    return useModern ? typeof(PlayersPage_Win11) : typeof(PlayersPage);

                case "Settings":
                    return useModern ? typeof(SettingsPage_Win11) : typeof(SettingsPage);

                default:
                    throw new ArgumentException($"Unknown page key: {pageKey}");
            }
        }
    }
}