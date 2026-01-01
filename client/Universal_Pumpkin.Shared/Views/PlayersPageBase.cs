using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Universal_Pumpkin.Models;
using Universal_Pumpkin.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Universal_Pumpkin.Shared.Views
{
    public abstract class PlayersPageBase : Page
    {
        protected readonly PlayersViewModel _vm;
        protected PlayerData _selectedPlayer;

        protected PlayersPageBase()
        {
            _vm = new PlayersViewModel();
            this.DataContext = this;

            _vm.PlayersUpdated += (s, e) =>
            {
                var noPlayers = FindName("NoPlayersText") as FrameworkElement;
                if (noPlayers != null)
                {
                    noPlayers.Visibility = _vm.HasPlayers ? Visibility.Collapsed : Visibility.Visible;
                }
            };
        }

        public System.Collections.ObjectModel.ObservableCollection<PlayerData> PlayersList => _vm.PlayersList;

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.StopPolling();
        }

        // --------- Section helpers (called from Win10/Win11 shells) ---------

        protected void EnterOnlineSection()
        {
            _vm.StartPolling();
        }

        protected void EnterOpsSection()
        {
            _vm.StopPolling();
            RefreshManagementData(1);
        }

        protected void EnterBansSection()
        {
            _vm.StopPolling();
            RefreshManagementData(2);
        }

        protected void EnterIpBansSection()
        {
            _vm.StopPolling();
            RefreshManagementData(3);
        }

        protected async void RefreshManagementData(int mode)
        {
            try
            {
                switch (mode)
                {
                    case 1:
                        await _vm.LoadOps();
                        {
                            var listOps = FindName("ListOps") as ListView;
                            if (listOps != null) listOps.ItemsSource = _vm.OpsList;
                        }
                        break;

                    case 2:
                        await _vm.LoadBans();
                        {
                            var listBans = FindName("ListBans") as ListView;
                            if (listBans != null) listBans.ItemsSource = _vm.BansList;
                        }
                        break;

                    case 3:
                        await _vm.LoadIpBans();
                        {
                            var listIpBans = FindName("ListIpBans") as ListView;
                            if (listIpBans != null) listIpBans.ItemsSource = _vm.IpBansList;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading management data: {ex.Message}");
            }
        }

        // --------- Dialog factory (Win11 overrides to set XamlRoot) ---------

        protected virtual ContentDialog CreateDialog()
        {
            return new ContentDialog();
        }

        // --------- Toolbar buttons ---------

        protected void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _vm.StartPolling();
        }

        protected void BtnRefreshManagement_Click(object sender, RoutedEventArgs e)
        {
            // We infer which management view is active by visibility.
            var listOps = FindName("ListOps") as ListView;
            var listBans = FindName("ListBans") as ListView;
            var listIpBans = FindName("ListIpBans") as ListView;

            if (listOps != null && listOps.Visibility == Visibility.Visible)
            {
                RefreshManagementData(1);
            }
            else if (listBans != null && listBans.Visibility == Visibility.Visible)
            {
                RefreshManagementData(2);
            }
            else if (listIpBans != null && listIpBans.Visibility == Visibility.Visible)
            {
                RefreshManagementData(3);
            }
        }

        protected async void BtnAddOp_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                var candidates = App.Server.GetPlayers().Where(p => p.PermissionLevel == 0).ToList();
                if (candidates.Count == 0)
                {
                    await ShowAlert("No eligible players online.");
                    return;
                }

                var selected = await PromptPlayerSelection(candidates, "Select Player to OP");
                if (selected != null)
                {
                    _vm.SendCommand($"op {selected.Username}");
                    await Task.Delay(500);
                    RefreshManagementData(1);
                }
            }
            else
            {
                string name = await PromptString("Enter Mojang Username to OP:");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    try
                    {
                        await _vm.AddOp(name);
                        RefreshManagementData(1);
                        await ShowAlert($"Added {name} as OP.");
                    }
                    catch
                    {
                        await ShowAlert("Could not resolve UUID. Check spelling.");
                    }
                }
            }
        }

        protected async void BtnRemoveOp_Click(object sender, RoutedEventArgs e)
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
                        _vm.SendCommand($"deop {op.Name}");
                        await Task.Delay(500);
                        RefreshManagementData(1);
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
                    await _vm.RemoveOp(op);
                    RefreshManagementData(1);
                }
            }
        }

        protected async void BtnAddBan_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                var online = App.Server.GetPlayers();
                if (online.Count == 0)
                {
                    await ShowAlert("No players online.");
                    return;
                }

                var selected = await PromptPlayerSelection(online, "Select Player to Ban");
                if (selected != null)
                    await ShowBanDialog(selected.Username);
            }
            else
            {
                string name = await PromptString("Enter Username to Ban:");
                if (string.IsNullOrWhiteSpace(name)) return;

                try
                {
                    await _vm.AddBan(name, "Banned via Offline Console");
                    RefreshManagementData(2);
                    await ShowAlert($"Successfully banned {name}.");
                }
                catch
                {
                    await ShowAlert($"Could not resolve UUID for '{name}'.");
                }
            }
        }

        protected async void BtnRemoveBan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BanEntry ban)
            {
                if (App.Server.IsRunning)
                {
                    _vm.SendCommand($"pardon {ban.Name}");
                    await Task.Delay(500);
                }
                else
                {
                    await _vm.RemoveBan(ban);
                }
                RefreshManagementData(2);
            }
        }

        protected async void BtnAddIpBan_Click(object sender, RoutedEventArgs e)
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

            var confirmDialog = CreateDialog();
            confirmDialog.Title = $"Ban IP {ip}?";
            confirmDialog.Content = panel;
            confirmDialog.PrimaryButtonText = "Ban IP";
            confirmDialog.SecondaryButtonText = "Cancel";

            if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string reason = string.IsNullOrWhiteSpace(reasonBox.Text) ? "Banned by operator" : reasonBox.Text;

                if (App.Server.IsRunning)
                {
                    _vm.SendCommand($"ban-ip {ip} \"{reason}\"");

                    if (playerBanCheck.Visibility == Visibility.Visible && playerBanCheck.IsChecked == true)
                    {
                        foreach (var name in matchingPlayers)
                            _vm.SendCommand($"ban {name} \"{reason}\"");
                    }

                    await Task.Delay(500);
                }
                else
                {
                    await _vm.AddIpBan(ip, reason);
                }

                RefreshManagementData(3);
            }
        }

        protected async void BtnRemoveIpBan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is IpBanEntry ipBan)
            {
                if (App.Server.IsRunning)
                {
                    _vm.SendCommand($"pardon-ip {ipBan.Ip}");
                    await Task.Delay(500);
                }
                else
                {
                    await _vm.RemoveIpBan(ipBan);
                }
                RefreshManagementData(3);
            }
        }

        // --------- Management list generic remove (Win10) ---------

        protected void BtnRemoveEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                if (btn.Tag is OpEntry) BtnRemoveOp_Click(sender, e);
                else if (btn.Tag is BanEntry) BtnRemoveBan_Click(sender, e);
                else if (btn.Tag is IpBanEntry) BtnRemoveIpBan_Click(sender, e);
            }
        }

        // --------- Dialog helpers ---------

        protected async Task<PlayerData> PromptPlayerSelection(List<PlayerData> candidates, string title)
        {
            var template = this.Resources["PlayerPickerTemplate"] as DataTemplate;

            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                ItemsSource = candidates,
                Height = 300,
                ItemTemplate = template
            };

            var dialog = CreateDialog();
            dialog.Title = title;
            dialog.Content = listView;
            dialog.PrimaryButtonText = "Select";
            dialog.SecondaryButtonText = "Cancel";

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                return listView.SelectedItem as PlayerData;
            }
            return null;
        }

        protected async Task ShowBanDialog(string username)
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

            var dialog = CreateDialog();
            dialog.Title = $"Ban {username}?";
            dialog.Content = panel;
            dialog.PrimaryButtonText = "Ban";
            dialog.SecondaryButtonText = "Cancel";

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string reason = string.IsNullOrWhiteSpace(reasonBox.Text) ? "Banned by operator" : reasonBox.Text;
                _vm.SendCommand($"ban {username} \"{reason}\"");

                if (ipCheck.IsChecked == true)
                    _vm.SendCommand($"ban-ip {username} \"{reason}\"");

                await Task.Delay(500);
                RefreshManagementData(2);
            }
        }

        protected async Task<string> PromptString(string title)
        {
            var box = new TextBox();

            var dialog = CreateDialog();
            dialog.Title = title;
            dialog.Content = box;
            dialog.PrimaryButtonText = "OK";
            dialog.SecondaryButtonText = "Cancel";

            return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
        }

        protected async Task ShowAlert(string msg)
        {
            var dialog = CreateDialog();
            dialog.Title = "Info";
            dialog.Content = msg;
            dialog.PrimaryButtonText = "OK";
            await dialog.ShowAsync();
        }

        // --------- Context menu handlers ---------

        protected void Player_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ShowMenu(sender);
        }

        protected void Player_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
                ShowMenu(sender);
        }

        private void ShowMenu(object sender)
        {
            if (sender is FrameworkElement element)
            {
                _selectedPlayer = element.DataContext as PlayerData;
                if (_selectedPlayer != null)
                    FlyoutBase.ShowAttachedFlyout(element);
            }
        }

        protected void Menu_Op_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer != null)
                _vm.SendCommand($"op {_selectedPlayer.Username}");
        }

        protected void Menu_Deop_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer != null)
                _vm.SendCommand($"deop {_selectedPlayer.Username}");
        }

        protected void Menu_Gm_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer != null && sender is MenuFlyoutItem item)
                _vm.SendCommand($"gamemode {item.Tag} {_selectedPlayer.Username}");
        }

        protected async void Menu_Kick_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;
            string reason = await PromptString($"Kick {_selectedPlayer.Username}? (Reason)") ?? "Kicked by operator";
            _vm.SendCommand($"kick {_selectedPlayer.Username} \"{reason}\"");
        }

        protected async void Menu_Ban_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;
            await ShowBanDialog(_selectedPlayer.Username);
        }

        protected void Menu_BanIp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer != null)
                _vm.SendCommand($"ban-ip {_selectedPlayer.Username}");
        }
    }
}