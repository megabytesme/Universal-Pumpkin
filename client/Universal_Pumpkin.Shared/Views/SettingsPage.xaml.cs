using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;

namespace Universal_Pumpkin
{
    public sealed partial class SettingsPage : Page
    {
        private bool _loading = true;

        public SettingsPage()
        {
            this.InitializeComponent();
            LoadAll();
        }

        private async void LoadAll()
        {
            _loading = true;

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

        private async void UpdateStorageSize()
        {
            TxtWorldSize.Text = "Calculating...";
            long bytes = await CalculateFolderSize(ApplicationData.Current.LocalFolder);
            TxtWorldSize.Text = FormatBytes(bytes);
        }

        private async Task<long> CalculateFolderSize(StorageFolder folder)
        {
            long size = 0;
            foreach (var file in await folder.GetFilesAsync())
            {
                var props = await file.GetBasicPropertiesAsync();
                size += (long)props.Size;
            }
            foreach (var sub in await folder.GetFoldersAsync())
            {
                size += await CalculateFolderSize(sub);
            }
            return size;
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }

        private async void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                await new ContentDialog { Title = "Server Running", Content = "Please stop the server before creating a backup to ensure data integrity.", PrimaryButtonText = "OK" }.ShowAsync();
                return;
            }

            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("Zip Archive", new List<string>() { ".zip" });
            savePicker.SuggestedFileName = $"Pumpkin_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm}";

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                BtnBackup.IsEnabled = false;
                BtnBackup.Content = "Zipping...";

                try
                {
                    await CreateBackupZip(file);
                    await new ContentDialog { Title = "Success", Content = "Backup created successfully.", PrimaryButtonText = "OK" }.ShowAsync();
                }
                catch (Exception ex)
                {
                    await new ContentDialog { Title = "Error", Content = ex.Message, PrimaryButtonText = "OK" }.ShowAsync();
                }
                finally
                {
                    BtnBackup.IsEnabled = true;
                    BtnBackup.Content = "Backup to Zip";
                }
            }
        }

        private async Task CreateBackupZip(StorageFile destinationFile)
        {
            var localFolder = ApplicationData.Current.LocalFolder;

            using (var stream = await destinationFile.OpenStreamForWriteAsync())
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                await AddFolderToZip(archive, localFolder, "");
            }
        }

        private async Task AddFolderToZip(ZipArchive archive, StorageFolder folder, string entryPath)
        {
            foreach (var file in await folder.GetFilesAsync())
            {
                if (file.Name.EndsWith(".dll") || file.Name.EndsWith(".lock") || file.Name.EndsWith(".tmp")) continue;

                var entryName = Path.Combine(entryPath, file.Name);
                var entry = archive.CreateEntry(entryName);

                using (var entryStream = entry.Open())
                using (var fileStream = await file.OpenStreamForReadAsync())
                {
                    await fileStream.CopyToAsync(entryStream);
                }
            }

            foreach (var sub in await folder.GetFoldersAsync())
            {
                await AddFolderToZip(archive, sub, Path.Combine(entryPath, sub.Name));
            }
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                await new ContentDialog { Title = "Server Running", Content = "Please stop the server before restoring a backup.", PrimaryButtonText = "OK" }.ShowAsync();
                return;
            }

            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".zip");

            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var confirm = new ContentDialog
                {
                    Title = "Confirm Restore",
                    Content = "This will OVERWRITE your current world, players, and config. This cannot be undone.",
                    PrimaryButtonText = "Restore",
                    SecondaryButtonText = "Cancel"
                };

                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                {
                    BtnRestore.IsEnabled = false;
                    BtnRestore.Content = "Restoring...";

                    try
                    {
                        await ExtractBackupZip(file);

                        UpdateStorageSize();

                        await new ContentDialog { Title = "Success", Content = "Backup restored. Please restart the app to ensure configs are reloaded.", PrimaryButtonText = "OK" }.ShowAsync();
                    }
                    catch (Exception ex)
                    {
                        await new ContentDialog { Title = "Restore Failed", Content = ex.Message, PrimaryButtonText = "OK" }.ShowAsync();
                    }
                    finally
                    {
                        BtnRestore.IsEnabled = true;
                        BtnRestore.Content = "Restore Zip";
                    }
                }
            }
        }

        private async Task ExtractBackupZip(StorageFile sourceZip)
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            
            try { var w = await localFolder.GetFolderAsync("world"); await w.DeleteAsync(); } catch { }
            try { var d = await localFolder.GetFolderAsync("data"); await d.DeleteAsync(); } catch { }
            try { var conf = await localFolder.GetFileAsync("configuration.toml"); await conf.DeleteAsync(); } catch { }
            try { var feat = await localFolder.GetFileAsync("features.toml"); await feat.DeleteAsync(); } catch { }

            using (var stream = await sourceZip.OpenStreamForReadAsync())
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/")) continue;

                    string fullPath = entry.FullName.Replace("/", "\\");
                    string folderPath = Path.GetDirectoryName(fullPath);

                    StorageFolder targetFolder = localFolder;
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        string[] parts = folderPath.Split('\\');
                        foreach (var part in parts)
                        {
                            targetFolder = await targetFolder.CreateFolderAsync(part, CreationCollisionOption.OpenIfExists);
                        }
                    }

                    var file = await targetFolder.CreateFileAsync(Path.GetFileName(fullPath), CreationCollisionOption.ReplaceExisting);
                    using (var entryStream = entry.Open())
                    using (var fileStream = await file.OpenStreamForWriteAsync())
                    {
                        await entryStream.CopyToAsync(fileStream);
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task BindToggle(ToggleSwitch ts, string section, string key)
        {
            string val = await ConfigHelper.GetValueAsync(section, key);
            if (bool.TryParse(val, out bool b)) ts.IsOn = b;
        }
        private async System.Threading.Tasks.Task BindText(TextBox tb, string section, string key)
        {
            string val = await ConfigHelper.GetValueAsync(section, key);
            if (val != null) tb.Text = val;
        }
        private async System.Threading.Tasks.Task BindCombo(ComboBox cb, string section, string key)
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
        private async System.Threading.Tasks.Task BindFeature(ToggleSwitch ts)
        {
            string[] parts = ts.Tag.ToString().Split('|');
            if (parts.Length == 2)
            {
                string val = await ConfigHelper.GetValueAsync(parts[0], parts[1]);
                if (bool.TryParse(val, out bool b)) ts.IsOn = b;
            }
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

        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
#if UWP1709
            bool canRestart = true;
#else
            bool canRestart = false;
#endif

            var deleteDialog = new ContentDialog
            {
                Title = "Delete World Files",
                Content = "Are you sure you want to continue? This action is permanent.",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No"
            };

            if (await deleteDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var folder = ApplicationData.Current.LocalFolder;

                try
                {
                    try { var w = await folder.GetFolderAsync("world"); await w.DeleteAsync(); } catch { }
                    try { var l = await folder.GetFileAsync("level.dat"); await l.DeleteAsync(); } catch { }
                    try { var lb = await folder.GetFileAsync("level.dat_old"); await lb.DeleteAsync(); } catch { }
                    try { var p = await folder.GetFolderAsync("playerdata"); await p.DeleteAsync(); } catch { }

                    var deleteCompleteDialog = new ContentDialog
                    {
                        Title = "World Files Deleted",
                        Content = canRestart ? "Restart now?" : "Close app now?",
                        PrimaryButtonText = "Yes",
                        SecondaryButtonText = "No"
                    };

                    if (await deleteCompleteDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
#if UWP1709
                        await CoreApplication.RequestRestartAsync("");
#else
                        CoreApplication.Exit();
#endif
                    }

                    var btn = (Button)sender;
                    btn.Content = "Data Deleted!";
                    btn.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    var btn = (Button)sender;
                    btn.Content = "Error: " + ex.Message;
                }
            }
        }

        private async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
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
                        new Run() { Text = $"Version {GetAppVersion()} ({GetAppName()})" },
                        new LineBreak(),
                        
                        new Run() { Text = $"Pumpkin {GetPumpkinVersion()}" },
                        new LineBreak(),
                        new Run() { Text = $"(Minecraft {GetMinecraftVersion()}, Protocol {GetProtocolVersion()})" },
                        new LineBreak(),

                        new Run() { Text = "Server Commit: " },
                        new Hyperlink()
                        {
                            NavigateUri = GetPumpkinCommitUri(),
                            Inlines = { new Run() { Text = GetPumpkinCommitId() } }
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

            var dialog = new ContentDialog
            {
                Title = "About",
                Content = scrollContent,
                PrimaryButtonText = "OK"
            };

            await dialog.ShowAsync();
        }

        private async void DisclaimerButton_Click(object sender, RoutedEventArgs e)
        {
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

            textBlock.Inlines.Add(new Run { Text = "This is an unofficial, third-party server implementation. This project is " });
            textBlock.Inlines.Add(new Run { Text = "not affiliated with, endorsed, or sponsored by Mojang Studios, Microsoft, or the official Pumpkin project.", FontWeight = Windows.UI.Text.FontWeights.Bold });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run { Text = "\"Minecraft\" is a trademark of Mojang Synergies AB." });

            var dialog = new ContentDialog
            {
                Title = "Disclaimer",
                Content = new ScrollViewer { Content = textBlock },
                PrimaryButtonText = "I Understand",
            };
            await dialog.ShowAsync();
        }

        private string GetAppName()
        {
#if UWP_1507
            return "1507_UWP";
#else
            return "1709_UWP";
#endif
        }

        private string GetAppVersion()
        {
#if UWP_1507
            return "1.0.0.0";
#else
            return "2.0.0.0";
#endif
        }

        private string GetPumpkinVersion() => "0.1.0-dev+1.21.11";
        private string GetMinecraftVersion() => "1.21.11";
        private int GetProtocolVersion() => 774;
        private string GetPumpkinCommitId() =>
            "70b31323967bb99fd4feefab8e96124be369cd6f";
        private Uri GetPumpkinCommitUri() =>
            new Uri("https://github.com/Pumpkin-MC/Pumpkin/tree/" + GetPumpkinCommitId());
    }
}