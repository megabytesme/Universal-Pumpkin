using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Navigation;
using Universal_Pumpkin.ViewModels;

namespace Universal_Pumpkin.Views.Win11
{
    public sealed partial class SettingsPage_Win11 : Page
    {
        private bool _loading = true;
        private readonly SettingsViewModel _vm;

        public SettingsPage_Win11()
        {
            this.InitializeComponent();
            _vm = new SettingsViewModel();
            LoadAll();
        }

        private async void LoadAll()
        {
            _loading = true;

            SwModernUI.IsOn = _vm.IsModernUIEnabled;

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
            if (double.TryParse(vd, out double v)) { SldView.Value = v; TxtViewVal.Text = v.ToString(); }

            string sd = await ConfigHelper.GetValueAsync("", "simulation_distance");
            if (double.TryParse(sd, out double s)) { SldSim.Value = s; TxtSimVal.Text = s.ToString(); }

            UpdateStorageSize();

            _loading = false;
        }

        private async void SwModernUI_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            _vm.IsModernUIEnabled = SwModernUI.IsOn;

            var result = await new ContentDialog
            {
                Title = "Restart Required",
                Content = "The app must restart to apply the theme change.",
                PrimaryButtonText = "Restart Now",
                SecondaryButtonText = "Later",
                XamlRoot = this.XamlRoot
            }.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await CoreApplication.RequestRestartAsync("");
            }
        }

        private async void UpdateStorageSize()
        {
            TxtWorldSize.Text = "Calculating...";
            TxtWorldSize.Text = await _vm.GetFormattedStorageSizeAsync();
        }

        private async void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                await ShowDialog("Server Running", "Please stop the server before creating a backup.");
                return;
            }

            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                SuggestedFileName = $"Pumpkin_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm}"
            };
            savePicker.FileTypeChoices.Add("Zip Archive", new List<string>() { ".zip" });

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                BtnBackup.IsEnabled = false;
                BtnBackup.Content = "Zipping...";

                try
                {
                    await _vm.CreateBackupZip(file);
                    await ShowDialog("Success", "Backup created successfully.");
                }
                catch (Exception ex)
                {
                    await ShowDialog("Error", ex.Message);
                }
                finally
                {
                    BtnBackup.IsEnabled = true;
                    BtnBackup.Content = "Backup to Zip";
                }
            }
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                await ShowDialog("Server Running", "Please stop the server before restoring a backup.");
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
                var confirm = new ContentDialog
                {
                    Title = "Confirm Restore",
                    Content = "This will OVERWRITE your current world, players, and config. This cannot be undone.",
                    PrimaryButtonText = "Restore",
                    SecondaryButtonText = "Cancel",
                    XamlRoot = this.XamlRoot
                };

                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                {
                    BtnRestore.IsEnabled = false;
                    BtnRestore.Content = "Restoring...";

                    try
                    {
                        await _vm.RestoreBackupZip(file);
                        UpdateStorageSize();
                        await ShowDialog("Success", "Backup restored. Please restart the app.");
                    }
                    catch (Exception ex)
                    {
                        await ShowDialog("Restore Failed", ex.Message);
                    }
                    finally
                    {
                        BtnRestore.IsEnabled = true;
                        BtnRestore.Content = "Restore Zip";
                    }
                }
            }
        }

        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                await ShowDialog("Server Running", "Please stop the server before resetting data.");
                return;
            }
            var deleteDialog = new ContentDialog
            {
                Title = "Delete World Files",
                Content = "Are you sure you want to continue? This action is permanent.",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No",
                XamlRoot = this.XamlRoot
            };

            if (await deleteDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    await _vm.DeleteWorldData();

                    var completeDialog = new ContentDialog
                    {
                        Title = "World Files Deleted",
                        Content = "Restart now?",
                        PrimaryButtonText = "Yes",
                        SecondaryButtonText = "No",
                        XamlRoot = this.XamlRoot
                    };

                    if (await completeDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        await CoreApplication.RequestRestartAsync("");
                    }

                    ((Button)sender).Content = "Data Deleted!";
                    ((Button)sender).IsEnabled = false;
                }
                catch (Exception ex)
                {
                    ((Button)sender).Content = "Error: " + ex.Message;
                }
            }
        }

        private async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
        }

        private async Task ShowDialog(string title, string content)
        {
            await new ContentDialog { Title = title, Content = content, PrimaryButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();
        }

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var scrollContent = new ScrollViewer()
            {
                Content = new TextBlock()
                {
                    Inlines =
                    {
                        new Run() { Text = "Universal Pumpkin", FontWeight = Windows.UI.Text.FontWeights.Bold, FontSize = 18 },
                        new LineBreak(),
                        new Run() { Text = $"Version {_vm.GetAppVersion()} ({_vm.GetAppName()})" },
                        new LineBreak(),

                        new Run() { Text = $"Pumpkin {_vm.GetPumpkinVersion()}" },
                        new LineBreak(),
                        new Run() { Text = $"(Minecraft {_vm.GetMinecraftVersion()}, Protocol {_vm.GetProtocolVersion()})" },
                        new LineBreak(),

                        new Run() { Text = "Server Commit: " },
                        new Hyperlink()
                        {
                            NavigateUri = _vm.GetPumpkinCommitUri(),
                            Inlines = { new Run() { Text = _vm.GetPumpkinCommitId() } }
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Run() { Text = "Copyright © 2025 MegaBytesMe" },
                        new LineBreak(),
                        new LineBreak(),

                        new Run() { Text = "Universal Pumpkin is a native, high-performance Minecraft server wrapper for UWP. It brings the power of the Rust-based " },
                        new Hyperlink()
                        {
                            NavigateUri = new Uri("https://github.com/Pumpkin-MC/Pumpkin"),
                            Inlines = { new Run() { Text = "Pumpkin" } }
                        },
                        new Run() { Text = " engine to devices supporting the Universal Windows Platform (UWP)." },
                        new LineBreak(),
                        new LineBreak(),

                        new Run() { Text = "Source code available on " },
                        new Hyperlink()
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/Universal-Pumpkin"),
                            Inlines = { new Run() { Text = "GitHub" } },
                        },
                        new LineBreak(),

                        new Run() { Text = "Found a bug? Report it here: " },
                        new Hyperlink()
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/Universal-Pumpkin/issues"),
                            Inlines = { new Run() { Text = "Issue Tracker" } },
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Run() { Text = "Like what you see? Consider supporting me on " },
                        new Hyperlink()
                        {
                            NavigateUri = new Uri("https://ko-fi.com/megabytesme"),
                            Inlines = { new Run() { Text = "Ko-fi!" } },
                        },
                        new LineBreak(),
                        new LineBreak(),

                        new Hyperlink()
                        {
                            NavigateUri = new Uri("https://github.com/megabytesme/Universal-Pumpkin/blob/master/LICENSE.md"),
                            Inlines = { new Run() { Text = "License:" } },
                        },
                        new LineBreak(),
                        new Run() { Text = "• App (Client): CC BY-NC-SA 4.0" },
                        new LineBreak(),
                        new Run() { Text = "• Server Core (Rust): MIT (Pumpkin Project)" },
                    },
                    TextWrapping = TextWrapping.Wrap,
                },
            };

            await new ContentDialog { Title = "About", Content = scrollContent, PrimaryButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();
        }

        private async void DisclaimerButton_Click(object sender, RoutedEventArgs e)
        {
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

            textBlock.Inlines.Add(new Run { Text = "This is an unofficial, third-party server implementation. This project is " });
            textBlock.Inlines.Add(new Run { Text = "not affiliated with, endorsed, or sponsored by Mojang Studios, Microsoft, or the official Pumpkin project.", FontWeight = Windows.UI.Text.FontWeights.Bold });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run { Text = "\"Minecraft\" is a trademark of Mojang Synergies AB." });

            await new ContentDialog { Title = "Disclaimer", Content = new ScrollViewer { Content = textBlock }, PrimaryButtonText = "I Understand", XamlRoot = this.XamlRoot }.ShowAsync();
        }

        private async Task BindToggle(ToggleSwitch ts, string section, string key)
        {
            try
            {
                string val = await ConfigHelper.GetValueAsync(section, key);
                if (bool.TryParse(val, out bool b)) ts.IsOn = b;
            }
            catch (System.IO.FileNotFoundException) { }
            catch (Exception) { }
        }

        private async Task BindText(TextBox tb, string section, string key)
        {
            try
            {
                string val = await ConfigHelper.GetValueAsync(section, key);
                if (val != null) tb.Text = val;
            }
            catch (System.IO.FileNotFoundException) { }
            catch (Exception) { }
        }

        private async Task BindCombo(ComboBox cb, string section, string key)
        {
            try
            {
                string val = await ConfigHelper.GetValueAsync(section, key);
                if (val == null) return;
                foreach (ComboBoxItem item in cb.Items)
                {
                    if (item.Content.ToString().Equals(val, StringComparison.OrdinalIgnoreCase))
                    {
                        cb.SelectedItem = item; break;
                    }
                }
            }
            catch (System.IO.FileNotFoundException) { }
            catch (Exception) { }
        }

        private async Task BindFeature(ToggleSwitch ts)
        {
            try
            {
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

        private async void Setting_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var c = (ToggleSwitch)sender;
            await ConfigHelper.SaveValueAsync("", c.Tag.ToString(), c.IsOn.ToString().ToLower());
        }
        private async void Feature_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var c = (ToggleSwitch)sender;
            string[] parts = c.Tag.ToString().Split('|');
            if (parts.Length == 2) await ConfigHelper.SaveValueAsync(parts[0], parts[1], c.IsOn.ToString().ToLower());
        }
        private async void Setting_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            var c = (TextBox)sender;
            await ConfigHelper.SaveValueAsync("", c.Tag.ToString(), c.Text);
        }
        private async void Setting_ComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            var c = (ComboBox)sender;
            if (c.SelectedItem is ComboBoxItem item) await ConfigHelper.SaveValueAsync("", c.Tag.ToString(), item.Content.ToString());
        }
        private async void SldView_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_loading) return;
            TxtViewVal.Text = e.NewValue.ToString();
            await ConfigHelper.SaveValueAsync("", "view_distance", e.NewValue.ToString());
        }
        private async void SldSim_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_loading) return;
            TxtSimVal.Text = e.NewValue.ToString();
            await ConfigHelper.SaveValueAsync("", "simulation_distance", e.NewValue.ToString());
        }
    }
}