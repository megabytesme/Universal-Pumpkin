using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Universal_Pumpkin.ViewModels;

namespace Universal_Pumpkin.Shared.Views
{
    public abstract class SettingsPageBase : Page
    {
        protected readonly SettingsViewModel _vm;
        protected bool _loading = true;

        protected SettingsPageBase()
        {
            _vm = new SettingsViewModel();
        }

        protected async Task LoadAllAsync()
        {
            _loading = true;

            InitializeModernUiSection();

            await BindToggle(SwJava, "", "java_edition");
            await BindText(TxtJavaAddr, "", "java_edition_address");
            await BindToggle(SwBedrock, "", "bedrock_edition");
            await BindText(TxtBedrockAddr, "", "bedrock_edition_address");

            await BindToggle(SwOnline, "", "online_mode");
            await BindToggle(SwEncrypt, "", "encryption");
            await BindToggle(SwScrub, "", "scrub_ips");

            await BindText(TxtMotd, "", "motd");
            await BindCombo(CmbGamemode, "", "default_gamemode");
            await BindToggle(SwForceGame, "", "force_gamemode");
            await BindCombo(CmbDifficulty, "", "default_difficulty");
            await BindToggle(SwHardcore, "", "hardcore");
            await BindToggle(SwNether, "", "allow_nether");
            await BindText(TxtSeed, "", "seed");

            await BindText(TxtMaxPlayers, "", "max_players");
            await BindText(TxtTPS, "", "tps");

            await BindText(TxtOpLevel, "", "op_permission_level");
            await BindToggle(SwWhite, "", "white_list");
            await BindToggle(SwEnforceWhite, "", "enforce_whitelist");
            await BindToggle(SwChatReport, "", "allow_chat_reports");

            await BindFeature(SwLan);
            await BindFeature(SwQuery);

            string vd = await ConfigHelper.GetValueAsync("", "view_distance");
            if (double.TryParse(vd, out double v))
            {
                SldView.Value = v;
                TxtViewVal.Text = v.ToString();
            }

            string sd = await ConfigHelper.GetValueAsync("", "simulation_distance");
            if (double.TryParse(sd, out double s))
            {
                SldSim.Value = s;
                TxtSimVal.Text = s.ToString();
            }

            UpdateStorageSize();

            _loading = false;
        }

        protected abstract void InitializeModernUiSection();

        protected virtual ContentDialog CreateDialog()
        {
            return new ContentDialog();
        }

        protected async void SwModernUI_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (SwModernUI == null) return;

            _vm.IsModernUIEnabled = SwModernUI.IsOn;

            var dialog = CreateDialog();
            dialog.Title = "Restart Required";
            dialog.Content = "The app must restart to apply the theme change.";
            dialog.PrimaryButtonText = "Restart Now";
            dialog.SecondaryButtonText = "Later";

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
#if UWP1709
                await CoreApplication.RequestRestartAsync("");
#else
                CoreApplication.Exit();
#endif
            }
        }

        protected async void UpdateStorageSize()
        {
            if (TxtWorldSize == null) return;

            TxtWorldSize.Text = "Calculating...";
            TxtWorldSize.Text = await _vm.GetFormattedStorageSizeAsync();
        }

        protected async void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                await ShowSimpleDialogAsync(
                    "Server Running",
                    "Please stop the server before creating a backup to ensure data integrity.");
                return;
            }

            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                SuggestedFileName = $"Pumpkin_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm}"
            };
            savePicker.FileTypeChoices.Add("Zip Archive", new List<string> { ".zip" });

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                BtnBackup.IsEnabled = false;
                BtnBackup.Content = "Zipping...";

                try
                {
                    await _vm.CreateBackupZip(file);
                    await ShowSimpleDialogAsync("Success", "Backup created successfully.");
                }
                catch (Exception ex)
                {
                    await ShowSimpleDialogAsync("Error", ex.Message);
                }
                finally
                {
                    BtnBackup.IsEnabled = true;
                    BtnBackup.Content = "Backup to Zip";
                }
            }
        }

        protected async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                await ShowSimpleDialogAsync(
                    "Server Running",
                    "Please stop the server before restoring a backup.");
                return;
            }

            var openPicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.Desktop
            };
            openPicker.FileTypeFilter.Add(".zip");

            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var confirm = CreateDialog();
                confirm.Title = "Confirm Restore";
                confirm.Content = "This will OVERWRITE your current world, players, and config. This cannot be undone.";
                confirm.PrimaryButtonText = "Restore";
                confirm.SecondaryButtonText = "Cancel";

                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                {
                    BtnRestore.IsEnabled = false;
                    BtnRestore.Content = "Restoring...";

                    try
                    {
                        await _vm.RestoreBackupZip(file);
                        UpdateStorageSize();

                        await ShowSimpleDialogAsync(
                            "Success",
                            "Backup restored. Please restart the app to ensure configs are reloaded.");
                    }
                    catch (Exception ex)
                    {
                        await ShowSimpleDialogAsync("Restore Failed", ex.Message);
                    }
                    finally
                    {
                        BtnRestore.IsEnabled = true;
                        BtnRestore.Content = "Restore Zip";
                    }
                }
            }
        }

        protected async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
#if UWP1709
            bool canRestart = true;
#else
            bool canRestart = false;
#endif

            if (App.Server.IsRunning)
            {
                await ShowSimpleDialogAsync(
                    "Server Running",
                    "Please stop the server before resetting data.");
                return;
            }

            var deleteDialog = CreateDialog();
            deleteDialog.Title = "Delete World Files";
            deleteDialog.Content = "Are you sure you want to continue? This action is permanent.";
            deleteDialog.PrimaryButtonText = "Yes";
            deleteDialog.SecondaryButtonText = "No";

            if (await deleteDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    await _vm.DeleteWorldData();

                    var deleteCompleteDialog = CreateDialog();
                    deleteCompleteDialog.Title = "World Files Deleted";
                    deleteCompleteDialog.Content = canRestart ? "Restart now?" : "Close app now?";
                    deleteCompleteDialog.PrimaryButtonText = "Yes";
                    deleteCompleteDialog.SecondaryButtonText = "No";

                    if (await deleteCompleteDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
#if UWP1709
                        await CoreApplication.RequestRestartAsync("");
#else
                        CoreApplication.Exit();
#endif
                    }

                    if (sender is Button btn)
                    {
                        btn.Content = "Data Deleted!";
                        btn.IsEnabled = false;
                    }
                }
                catch (Exception ex)
                {
                    if (sender is Button btn)
                    {
                        btn.Content = "Error: " + ex.Message;
                    }
                }
            }
        }

        protected async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
        }

        protected async Task ShowSimpleDialogAsync(string title, string content)
        {
            var dialog = CreateDialog();
            dialog.Title = title;
            dialog.Content = content;
            dialog.PrimaryButtonText = "OK";
            await dialog.ShowAsync();
        }

        protected async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var scrollContent = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Inlines =
                    {
                        new Run { Text = "Universal Pumpkin", FontWeight = FontWeights.Bold, FontSize = 18 },
                        new LineBreak(),
                        new Run { Text = $"Version {_vm.GetAppVersion()} ({_vm.GetAppName()}) {_vm.GetArchitecture()}" },
                        new LineBreak(),

                        new Run { Text = $"Pumpkin {_vm.GetPumpkinVersion()}" },
                        new LineBreak(),
                        new Run { Text = $"(Minecraft {_vm.GetMinecraftVersion()}, Protocol {_vm.GetProtocolVersion()})" },
                        new LineBreak(),

                        new Run { Text = "Server Commit: " },
                        new Hyperlink
                        {
                            NavigateUri = _vm.GetPumpkinCommitUri(),
                            Inlines = { new Run { Text = _vm.GetPumpkinCommitId() } }
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Copyright © 2025 MegaBytesMe" },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Universal Pumpkin is a native, high-performance Minecraft server wrapper for UWP. It brings the power of the Rust-based " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/Pumpkin-MC/Pumpkin"),
                            Inlines = { new Run { Text = "Pumpkin" } }
                        },
                        new Run { Text = " engine to devices supporting the Universal Windows Platform (UWP)." },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Source code available on " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/Universal-Pumpkin"),
                            Inlines = { new Run { Text = "GitHub" } }
                        },
                        new LineBreak(),

                        new Run { Text = "Found a bug? Report it here: " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/Universal-Pumpkin/issues"),
                            Inlines = { new Run { Text = "Issue Tracker" } }
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Run { Text = "Like what you see? Consider supporting me on " },
                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://ko-fi.com/megabytesme"),
                            Inlines = { new Run { Text = "Ko-fi!" } }
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Hyperlink
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/Universal-Pumpkin/blob/master/LICENSE.md"),
                            Inlines = { new Run { Text = "License:" } }
                        },
                        new LineBreak(),
                        new Run { Text = "• App (Client): CC BY-NC-SA 4.0" },
                        new LineBreak(),
                        new Run { Text = "• Server Core (Rust): MIT (Pumpkin Project)" }
                    },
                    TextWrapping = TextWrapping.Wrap
                }
            };

            var dialog = CreateDialog();
            dialog.Title = "About";
            dialog.Content = scrollContent;
            dialog.PrimaryButtonText = "OK";
            await dialog.ShowAsync();
        }

        protected async void DisclaimerButton_Click(object sender, RoutedEventArgs e)
        {
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

            textBlock.Inlines.Add(new Run
            {
                Text = "This is an unofficial, third-party server implementation. This project is "
            });
            textBlock.Inlines.Add(new Run
            {
                Text = "not affiliated with, endorsed, or sponsored by Mojang Studios, Microsoft, or the official Pumpkin project.",
                FontWeight = FontWeights.Bold
            });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run { Text = "\"Minecraft\" is a trademark of Mojang Synergies AB." });

            var dialog = CreateDialog();
            dialog.Title = "Disclaimer";
            dialog.Content = new ScrollViewer { Content = textBlock };
            dialog.PrimaryButtonText = "I Understand";
            await dialog.ShowAsync();
        }

        protected async Task BindToggle(ToggleSwitch ts, string section, string key)
        {
            try
            {
                if (ts == null) return;
                string val = await ConfigHelper.GetValueAsync(section, key);
                if (bool.TryParse(val, out bool b)) ts.IsOn = b;
            }
            catch (System.IO.FileNotFoundException) { }
            catch (Exception) { }
        }

        protected async Task BindText(TextBox tb, string section, string key)
        {
            try
            {
                if (tb == null) return;
                string val = await ConfigHelper.GetValueAsync(section, key);
                if (val != null) tb.Text = val;
            }
            catch (System.IO.FileNotFoundException) { }
            catch (Exception) { }
        }

        protected async Task BindCombo(ComboBox cb, string section, string key)
        {
            try
            {
                if (cb == null) return;
                string val = await ConfigHelper.GetValueAsync(section, key);
                if (val == null) return;

                foreach (ComboBoxItem item in cb.Items)
                {
                    if (item.Content.ToString().Equals(val, StringComparison.OrdinalIgnoreCase))
                    {
                        cb.SelectedItem = item;
                        break;
                    }
                }
            }
            catch (System.IO.FileNotFoundException) { }
            catch (Exception) { }
        }

        protected async Task BindFeature(ToggleSwitch ts)
        {
            try
            {
                if (ts == null || ts.Tag == null) return;

                string[] parts = ts.Tag.ToString().Split('|');
                if (parts.Length == 2)
                {
                    string val = await ConfigHelper.GetValueAsync(parts[0], parts[1]);
                    if (bool.TryParse(val, out bool b)) ts.IsOn = b;
                }
            }
            catch (System.IO.FileNotFoundException) { }
            catch (Exception) { }
        }

        protected async void Setting_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is ToggleSwitch c && c.Tag != null)
                await ConfigHelper.SaveValueAsync("", c.Tag.ToString(), c.IsOn.ToString().ToLower());
        }

        protected async void Feature_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is ToggleSwitch c && c.Tag != null)
            {
                string[] parts = c.Tag.ToString().Split('|');
                if (parts.Length == 2)
                    await ConfigHelper.SaveValueAsync(parts[0], parts[1], c.IsOn.ToString().ToLower());
            }
        }

        protected async void Setting_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            if (sender is TextBox c && c.Tag != null)
                await ConfigHelper.SaveValueAsync("", c.Tag.ToString(), c.Text);
        }

        protected async void Setting_ComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            if (sender is ComboBox c && c.Tag != null && c.SelectedItem is ComboBoxItem item)
                await ConfigHelper.SaveValueAsync("", c.Tag.ToString(), item.Content.ToString());
        }

        protected async void SldView_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_loading) return;
            if (TxtViewVal != null)
                TxtViewVal.Text = e.NewValue.ToString();
            await ConfigHelper.SaveValueAsync("", "view_distance", e.NewValue.ToString());
        }

        protected async void SldSim_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_loading) return;
            if (TxtSimVal != null)
                TxtSimVal.Text = e.NewValue.ToString();
            await ConfigHelper.SaveValueAsync("", "simulation_distance", e.NewValue.ToString());
        }

        protected ToggleSwitch SwModernUI => FindName("SwModernUI") as ToggleSwitch;
        protected FrameworkElement AppearanceCard => FindName("AppearanceCard") as FrameworkElement;

        protected ToggleSwitch SwJava => FindName("SwJava") as ToggleSwitch;
        protected TextBox TxtJavaAddr => FindName("TxtJavaAddr") as TextBox;
        protected ToggleSwitch SwBedrock => FindName("SwBedrock") as ToggleSwitch;
        protected TextBox TxtBedrockAddr => FindName("TxtBedrockAddr") as TextBox;

        protected ToggleSwitch SwOnline => FindName("SwOnline") as ToggleSwitch;
        protected ToggleSwitch SwEncrypt => FindName("SwEncrypt") as ToggleSwitch;
        protected ToggleSwitch SwScrub => FindName("SwScrub") as ToggleSwitch;

        protected TextBox TxtMotd => FindName("TxtMotd") as TextBox;
        protected ComboBox CmbGamemode => FindName("CmbGamemode") as ComboBox;
        protected ToggleSwitch SwForceGame => FindName("SwForceGame") as ToggleSwitch;
        protected ComboBox CmbDifficulty => FindName("CmbDifficulty") as ComboBox;
        protected ToggleSwitch SwHardcore => FindName("SwHardcore") as ToggleSwitch;
        protected ToggleSwitch SwNether => FindName("SwNether") as ToggleSwitch;
        protected TextBox TxtSeed => FindName("TxtSeed") as TextBox;

        protected TextBox TxtMaxPlayers => FindName("TxtMaxPlayers") as TextBox;
        protected TextBox TxtTPS => FindName("TxtTPS") as TextBox;

        protected TextBox TxtOpLevel => FindName("TxtOpLevel") as TextBox;
        protected ToggleSwitch SwWhite => FindName("SwWhite") as ToggleSwitch;
        protected ToggleSwitch SwEnforceWhite => FindName("SwEnforceWhite") as ToggleSwitch;
        protected ToggleSwitch SwChatReport => FindName("SwChatReport") as ToggleSwitch;

        protected ToggleSwitch SwLan => FindName("SwLan") as ToggleSwitch;
        protected ToggleSwitch SwQuery => FindName("SwQuery") as ToggleSwitch;

        protected Slider SldView => FindName("SldView") as Slider;
        protected TextBlock TxtViewVal => FindName("TxtViewVal") as TextBlock;
        protected Slider SldSim => FindName("SldSim") as Slider;
        protected TextBlock TxtSimVal => FindName("TxtSimVal") as TextBlock;

        protected TextBlock TxtWorldSize => FindName("TxtWorldSize") as TextBlock;
        protected Button BtnBackup => FindName("BtnBackup") as Button;
        protected Button BtnRestore => FindName("BtnRestore") as Button;
    }
}