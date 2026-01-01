using System;
using Universal_Pumpkin.Services;
using Universal_Pumpkin.Shared.Views;
#if UWP1709
using Universal_Pumpkin.Views.Win11;
#endif

namespace Universal_Pumpkin
{
    public static class NavigationHelper
    {
#if UWP1709
        public static Type GetPageType(string pageKey)
        {
            bool useModern = OSHelper.IsWin11Mode;

            switch (pageKey)
            {
                case "OOBE":
                    return useModern ? typeof(OobePage_Win11) : typeof(OobePage);

                case "Shell":
                    return useModern ? typeof(ShellPage) : typeof(MainPage);

                case "Console":
                    return useModern ? typeof(ConsolePage_Win11) : typeof(ConsolePage_Win10);

                case "Players":
                    return useModern ? typeof(PlayersPage_Win11) : typeof(PlayersPage_Win10);

                case "Settings":
                    return useModern ? typeof(SettingsPage_Win11) : typeof(SettingsPage_Win10);

                default:
                    throw new ArgumentException($"Unknown page key: {pageKey}");
            }
        }
#else
        public static Type GetPageType(string pageKey)
        {
            switch (pageKey)
            {
                case "OOBE":
                    return typeof(OobePage);

                case "Shell":
                    return typeof(MainPage);

                case "Console":
                    return typeof(ConsolePage_Win10);

                case "Players":
                    return typeof(PlayersPage_Win10);

                case "Settings":
                    return typeof(SettingsPage_Win10);

                default:
                    throw new ArgumentException($"Unknown page key: {pageKey}");
            }
        }
#endif
    }
}