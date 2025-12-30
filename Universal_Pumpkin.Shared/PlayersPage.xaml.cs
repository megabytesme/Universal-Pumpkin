using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Universal_Pumpkin.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace Universal_Pumpkin
{
    public sealed partial class PlayersPage : Page
    {
        private DispatcherTimer _pollTimer;
        public ObservableCollection<PlayerData> PlayersList { get; } = new ObservableCollection<PlayerData>();
        private PlayerData _selectedPlayer;

        public PlayersPage()
        {
            this.InitializeComponent();
            this.DataContext = this;

            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromSeconds(1);
            _pollTimer.Tick += PollTimer_Tick;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HandlePivotState();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _pollTimer.Stop();
        }

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandlePivotState();
        }

        private void HandlePivotState()
        {
            if (MainPivot.SelectedIndex == 0)
            {
                if (App.Server.IsRunning) _pollTimer.Start();
                PollTimer_Tick(null, null);
            }
            else
            {
                _pollTimer.Stop();
                RefreshManagementData();
            }
        }

        private async void RefreshManagementData()
        {
            try
            {
                if (MainPivot.SelectedIndex == 1)
                {
                    var ops = await ManagementHelper.LoadOps();
                    foreach (var op in ops)
                    {
                        if (App.Server.IsDeopQueued(op.Name))
                            op.IsPendingRemoval = true;
                    }
                    ListOps.ItemsSource = ops;
                }
                else if (MainPivot.SelectedIndex == 2) ListBans.ItemsSource = await ManagementHelper.LoadBans();
                else if (MainPivot.SelectedIndex == 3) ListIpBans.ItemsSource = await ManagementHelper.LoadIpBans();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading management data: {ex.Message}");
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => PollTimer_Tick(null, null);
        private void BtnRefreshManagement_Click(object sender, RoutedEventArgs e) => RefreshManagementData();

        private async void BtnAddOp_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                var online = App.Server.GetPlayers();

                var candidates = online.Where(p => p.PermissionLevel == 0).ToList();

                if (candidates.Count == 0)
                {
                    await ShowAlert("No eligible players are online to OP.");
                    return;
                }

                var selected = await PromptPlayerSelection(candidates, "Select Player to OP");

                if (selected != null)
                {
                    App.Server.SendCommand($"op {selected.Username}");
                    await Task.Delay(500);
                    RefreshManagementData();
                }
            }
            else
            {
                await ShowAlert("To add an OP offline, you must manually edit ops.json. Please start the server to use this feature.");
            }
        }

        private async void BtnRemoveOp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OpEntry op)
            {
                if (op.IsPendingRemoval) return;

                if (App.Server.IsRunning)
                {
                    var online = App.Server.GetPlayers();
                    bool isOnline = online.Any(p => p.Username.Equals(op.Name, StringComparison.OrdinalIgnoreCase));

                    if (isOnline)
                    {
                        App.Server.SendCommand($"deop {op.Name}");
                        await Task.Delay(500);
                        RefreshManagementData();
                    }
                    else
                    {
                        App.Server.QueueOfflineDeop(op.Name);

                        op.IsPendingRemoval = true;

                        btn.Content = "Queued";
                        btn.IsEnabled = false;

                        await ShowAlert($"{op.Name} is offline. They have been queued for de-op and will be removed when the server stops.");
                    }
                }
                else
                {
                    var list = await ManagementHelper.LoadOps();
                    list.RemoveAll(x => x.Name == op.Name || x.Uuid == op.Uuid);
                    await ManagementHelper.SaveOps(list);
                    RefreshManagementData();
                }
            }
        }

        private async Task<PlayerData> PromptPlayerSelection(List<PlayerData> candidates, string title)
        {
            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                ItemsSource = candidates,
                Height = 300,
                ItemTemplate = this.Resources["PlayerPickerTemplate"] as DataTemplate
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = listView,
                PrimaryButtonText = "Select",
                SecondaryButtonText = "Cancel"
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                return listView.SelectedItem as PlayerData;
            }
            return null;
        }

        private async Task ShowBanDialog(string username)
        {
            var reasonBox = new TextBox
            {
                PlaceholderText = "Reason (Optional)",
                Margin = new Thickness(0, 0, 0, 10)
            };

            var ipCheck = new CheckBox
            {
                Content = "Also ban IP Address"
            };

            var panel = new StackPanel();
            panel.Children.Add(reasonBox);
            panel.Children.Add(ipCheck);

            var dialog = new ContentDialog
            {
                Title = $"Ban {username}?",
                Content = panel,
                PrimaryButtonText = "Ban",
                SecondaryButtonText = "Cancel"
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string reason = string.IsNullOrWhiteSpace(reasonBox.Text) ? "Banned by operator" : reasonBox.Text;

                App.Server.SendCommand($"ban {username} \"{reason}\"");

                if (ipCheck.IsChecked == true)
                {
                    App.Server.SendCommand($"ban-ip {username} \"{reason}\"");
                }

                await Task.Delay(500);
                RefreshManagementData();
            }
        }

        private async void BtnRemoveEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                if (btn.Tag is OpEntry op)
                {
                    if (string.IsNullOrWhiteSpace(op.Name))
                    {
                        await ShowAlert("Cannot deop: Player Name is missing.");
                        return;
                    }

                    if (App.Server.IsRunning)
                    {
                        Debug.WriteLine($"[Client] Sending: deop {op.Name}");
                        App.Server.SendCommand($"deop {op.Name}");
                        await Task.Delay(500);
                    }
                    else
                    {
                        var list = await ManagementHelper.LoadOps();
                        list.RemoveAll(x => x.Name == op.Name || x.Uuid == op.Uuid);
                        await ManagementHelper.SaveOps(list);
                    }
                }
                else if (btn.Tag is BanEntry ban)
                {
                    if (string.IsNullOrWhiteSpace(ban.Name))
                    {
                        await ShowAlert("Cannot pardon: Player Name is missing.");
                        return;
                    }

                    if (App.Server.IsRunning)
                    {
                        Debug.WriteLine($"[Client] Sending: pardon {ban.Name}");
                        App.Server.SendCommand($"pardon {ban.Name}");
                        await Task.Delay(500);
                    }
                    else
                    {
                        var list = await ManagementHelper.LoadBans();
                        list.RemoveAll(x => x.Name == ban.Name);
                        await ManagementHelper.SaveBans(list);
                    }
                }
                else if (btn.Tag is IpBanEntry ipBan)
                {
                    if (App.Server.IsRunning)
                    {
                        App.Server.SendCommand($"pardon-ip {ipBan.Ip}");
                        await Task.Delay(500);
                    }
                    else
                    {
                        var list = await ManagementHelper.LoadIpBans();
                        list.RemoveAll(x => x.Ip == ipBan.Ip);
                        await ManagementHelper.SaveIpBans(list);
                    }
                }

                RefreshManagementData();
            }
        }

        private async void BtnAddBan_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                var online = App.Server.GetPlayers();

                if (online.Count == 0)
                {
                    await ShowAlert("No players are currently online to ban via selector. To ban an offline player by name, please use the Console page.");
                    return;
                }

                var selected = await PromptPlayerSelection(online, "Select Player to Ban");

                if (selected != null)
                {
                    await ShowBanDialog(selected.Username);
                }
            }
            else
            {
                await ShowAlert("Please start the server to ban players (UUID lookup required).");
            }
        }

        private async void BtnAddIpBan_Click(object sender, RoutedEventArgs e)
        {
            string ip = await PromptString("Enter IP to Ban:");
            if (string.IsNullOrWhiteSpace(ip)) return;

            if (!IPAddress.TryParse(ip, out _))
            {
                await ShowAlert("Invalid IP Address format.");
                return;
            }

            var reasonBox = new TextBox
            {
                PlaceholderText = "Reason (Optional)",
                Margin = new Thickness(0, 0, 0, 10)
            };

            var playerBanCheck = new CheckBox
            {
                Content = "Also ban players with this IP",
                Visibility = Visibility.Collapsed,
                IsChecked = true
            };

            var panel = new StackPanel();
            panel.Children.Add(reasonBox);
            panel.Children.Add(playerBanCheck);

            List<string> matchingPlayers = new List<string>();

            if (App.Server.IsRunning)
            {
                var online = App.Server.GetPlayers();
                foreach (var p in online)
                {
                    string pIp = p.IpAddress.Contains(":") ? p.IpAddress.Split(':')[0] : p.IpAddress;

                    if (pIp == ip)
                    {
                        matchingPlayers.Add(p.Username);
                    }
                }

                if (matchingPlayers.Count > 0)
                {
                    playerBanCheck.Visibility = Visibility.Visible;
                    playerBanCheck.Content = $"Also ban {matchingPlayers.Count} player(s) on this IP";
                }
            }

            var confirmDialog = new ContentDialog
            {
                Title = $"Ban IP {ip}?",
                Content = panel,
                PrimaryButtonText = "Ban IP",
                SecondaryButtonText = "Cancel"
            };

            if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string reason = string.IsNullOrWhiteSpace(reasonBox.Text) ? "Banned by operator" : reasonBox.Text;

                if (App.Server.IsRunning)
                {
                    App.Server.SendCommand($"ban-ip {ip} \"{reason}\"");

                    if (playerBanCheck.Visibility == Visibility.Visible && playerBanCheck.IsChecked == true)
                    {
                        foreach (var name in matchingPlayers)
                        {
                            App.Server.SendCommand($"ban {name} \"{reason}\"");
                        }
                    }

                    await Task.Delay(500);
                    RefreshManagementData();
                }
                else
                {
                    var list = await ManagementHelper.LoadIpBans();
                    list.Add(new IpBanEntry
                    {
                        Ip = ip,
                        Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:sszzz"),
                        Source = "Console",
                        Expires = "forever",
                        Reason = reason
                    });
                    await ManagementHelper.SaveIpBans(list);
                    RefreshManagementData();
                }
            }
        }

        private async void BtnRemoveIpBan_Click(object sender, RoutedEventArgs e)
        {
            BtnRemoveEntry_Click(sender, e);
        }

        private async void BtnRemoveBan_Click(object sender, RoutedEventArgs e) => BtnRemoveEntry_Click(sender, e);

        private async Task<string> PromptString(string title)
        {
            var box = new TextBox();
            var dialog = new ContentDialog { Title = title, Content = box, PrimaryButtonText = "OK", SecondaryButtonText = "Cancel" };
            return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
        }

        private async Task ShowAlert(string msg)
        {
            await new ContentDialog { Title = "Info", Content = msg, PrimaryButtonText = "OK" }.ShowAsync();
        }

        private void Player_RightTapped(object sender, RightTappedRoutedEventArgs e) => ShowMenu(sender, e.OriginalSource);
        private void Player_Holding(object sender, HoldingRoutedEventArgs e) { if (e.HoldingState == Windows.UI.Input.HoldingState.Started) ShowMenu(sender, e.OriginalSource); }

        private void ShowMenu(object sender, object originalSource)
        {
            if (sender is FrameworkElement element)
            {
                _selectedPlayer = element.DataContext as PlayerData;
                if (_selectedPlayer != null) FlyoutBase.ShowAttachedFlyout(element);
            }
        }

        private void Menu_Op_Click(object sender, RoutedEventArgs e) { if (_selectedPlayer != null) App.Server.SendCommand($"op {_selectedPlayer.Username}"); }
        private void Menu_Deop_Click(object sender, RoutedEventArgs e) { if (_selectedPlayer != null) App.Server.SendCommand($"deop {_selectedPlayer.Username}"); }

        private void Menu_Gm_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer != null && sender is MenuFlyoutItem item)
                App.Server.SendCommand($"gamemode {item.Tag} {_selectedPlayer.Username}");
        }

        private async void Menu_Kick_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;
            string reason = await PromptString($"Kick {_selectedPlayer.Username}? (Reason)") ?? "Kicked by operator";
            App.Server.SendCommand($"kick {_selectedPlayer.Username} \"{reason}\"");
        }

        private async void Menu_Ban_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;
            await ShowBanDialog(_selectedPlayer.Username);
        }

        private async void Menu_BanIp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer != null) App.Server.SendCommand($"ban-ip {_selectedPlayer.Username}");
        }

        private void PollTimer_Tick(object sender, object e)
        {
            if (!App.Server.IsRunning) { PlayersList.Clear(); NoPlayersText.Visibility = Visibility.Visible; return; }
            var freshData = App.Server.GetPlayers();
            if (freshData.Count == 0) { PlayersList.Clear(); NoPlayersText.Visibility = Visibility.Visible; return; }
            NoPlayersText.Visibility = Visibility.Collapsed;

            for (int i = PlayersList.Count - 1; i >= 0; i--)
            {
                var existing = PlayersList[i];
                if (!freshData.Any(p => p.Uuid == existing.Uuid)) PlayersList.RemoveAt(i);
            }

            foreach (var fresh in freshData)
            {
                var existing = PlayersList.FirstOrDefault(p => p.Uuid == fresh.Uuid);
                if (existing == null) PlayersList.Add(fresh);
                else { int index = PlayersList.IndexOf(existing); PlayersList[index] = fresh; }
            }
        }
    }
}