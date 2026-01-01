using System;
using Universal_Pumpkin.Services;
using Universal_Pumpkin.Shared.Views;
using Universal_Pumpkin.Models;

#if UWP1709
using Universal_Pumpkin.Views.Win11;
using Universal_Pumpkin.Views.Win10_1709;
#endif

namespace Universal_Pumpkin
{
    public static class NavigationHelper
    {
#if UWP1709
        public static Type GetPageType(string pageKey)
        {
            var mode = AppearanceService.Current;

            if (pageKey == "OOBE")
            {
                if (mode == AppearanceMode.Win11) return typeof(OobePage_Win11);
                if (mode == AppearanceMode.Win10_1709) return typeof(OobePage);
                return typeof(OobePage);
            }

            if (pageKey == "Shell")
            {
                if (mode == AppearanceMode.Win11) return typeof(ShellPage);
                if (mode == AppearanceMode.Win10_1709) return typeof(MainPage);
                return typeof(MainPage);
            }

            if (pageKey == "Console")
            {
                if (mode == AppearanceMode.Win11) return typeof(ConsolePage_Win11);
                if (mode == AppearanceMode.Win10_1709) return typeof(ConsolePage_Win10_1709);
                return typeof(ConsolePage_Win10_1507);
            }

            if (pageKey == "Players")
            {
                if (mode == AppearanceMode.Win11) return typeof(PlayersPage_Win11);
                if (mode == AppearanceMode.Win10_1709) return typeof(PlayersPage_Win10_1709);
                return typeof(PlayersPage_Win10_1507);
            }

            if (pageKey == "Settings")
            {
                if (mode == AppearanceMode.Win11) return typeof(SettingsPage_Win11);
                if (mode == AppearanceMode.Win10_1709) return typeof(SettingsPage_Win10_1709);
                return typeof(SettingsPage_Win10_1507);
            }

            throw new ArgumentException($"Unknown page key: {pageKey}");
        }

#else
        public static Type GetPageType(string pageKey)
        {
            if (pageKey == "OOBE") return typeof(OobePage);
            if (pageKey == "Shell") return typeof(MainPage);
            if (pageKey == "Console") return typeof(ConsolePage_Win10_1507);
            if (pageKey == "Players") return typeof(PlayersPage_Win10_1507);
            if (pageKey == "Settings") return typeof(SettingsPage_Win10_1507);

            throw new ArgumentException($"Unknown page key: {pageKey}");
        }
#endif
    }
}