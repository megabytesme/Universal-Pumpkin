using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Universal_Pumpkin.Models;
using Universal_Pumpkin.ViewModels;
using System.Net;
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using NavigationView = Microsoft.UI.Xaml.Controls.NavigationView;
using NavigationViewSelectionChangedEventArgs = Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;

namespace Universal_Pumpkin.Views.Win11
{
    public sealed partial class PlayersPage_Win11 : Page
    {
        private readonly PlayersViewModel _vm;
        private PlayerData _selectedPlayer;

        public PlayersPage_Win11()
        {
            this.InitializeComponent();
            _vm = new PlayersViewModel();
            this.DataContext = this;

            _vm.PlayersUpdated += (s, e) =>
            {
                if (NoPlayersText != null)
                {
                    NoPlayersText.Visibility = _vm.HasPlayers ? Visibility.Collapsed : Visibility.Visible;
                }
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            TopNav.SelectedItem = TopNav.FooterMenuItems[0];
            HandleNavigation("Online");
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.StopPolling();
        }

        private void TopNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                HandleNavigation(item.Tag.ToString());
            }
        }

        private void HandleNavigation(string tag)
        {
            ViewOnline.Visibility = Visibility.Collapsed;
            ViewOps.Visibility = Visibility.Collapsed;
            ViewBans.Visibility = Visibility.Collapsed;
            ViewIpBans.Visibility = Visibility.Collapsed;

            switch (tag)
            {
                case "Online":
                    ViewOnline.Visibility = Visibility.Visible;
                    _vm.StartPolling();
                    break;

                case "Ops":
                    ViewOps.Visibility = Visibility.Visible;
                    _vm.StopPolling();
                    RefreshManagementData(1);
                    break;

                case "Bans":
                    ViewBans.Visibility = Visibility.Visible;
                    _vm.StopPolling();
                    RefreshManagementData(2);
                    break;

                case "IpBans":
                    ViewIpBans.Visibility = Visibility.Visible;
                    _vm.StopPolling();
                    RefreshManagementData(3);
                    break;
            }
        }

        private async void RefreshManagementData(int mode = 0)
        {
            try
            {
                if (mode == 0) return;

                switch (mode)
                {
                    case 1:
                        await _vm.LoadOps();
                        ListOps.ItemsSource = _vm.OpsList;
                        break;

                    case 2:
                        await _vm.LoadBans();
                        ListBans.ItemsSource = _vm.BansList;
                        break;

                    case 3:
                        await _vm.LoadIpBans();
                        ListIpBans.ItemsSource = _vm.IpBansList;
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading management data: {ex.Message}");
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _vm.StartPolling();
        private void BtnRefreshManagement_Click(object sender, RoutedEventArgs e)
        {
            if (ViewOps.Visibility == Visibility.Visible) RefreshManagementData(1);
            else if (ViewBans.Visibility == Visibility.Visible) RefreshManagementData(2);
            else if (ViewIpBans.Visibility == Visibility.Visible) RefreshManagementData(3);
        }

        private async void BtnAddOp_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                var candidates = App.Server.GetPlayers().Where(p => p.PermissionLevel == 0).ToList();
                if (candidates.Count == 0) { await ShowAlert("No eligible players online."); return; }

                var selected = await PromptPlayerSelection(candidates, "Select Player to OP");
                if (selected != null)
                {
                    _vm.SendCommand($"op {selected.Username}");
                    await Task.Delay(500);
                    RefreshManagementData();
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
                        RefreshManagementData();
                        await ShowAlert($"Added {name} as OP.");
                    }
                    catch { await ShowAlert("Could not resolve UUID. Check spelling."); }
                }
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
                        _vm.SendCommand($"deop {op.Name}");
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
                    await _vm.RemoveOp(op);
                    RefreshManagementData();
                }
            }
        }
        
        private async void BtnAddBan_Click(object sender, RoutedEventArgs e)
        {
            if (App.Server.IsRunning)
            {
                var online = App.Server.GetPlayers();
                if (online.Count == 0) { await ShowAlert("No players online."); return; }
                var selected = await PromptPlayerSelection(online, "Select Player to Ban");
                if (selected != null) await ShowBanDialog(selected.Username);
            }
            else
            {
                string name = await PromptString("Enter Username to Ban:");
                if (string.IsNullOrWhiteSpace(name)) return;

                try
                {
                    await _vm.AddBan(name, "Banned via Offline Console");
                    RefreshManagementData();
                    await ShowAlert($"Successfully banned {name}.");
                }
                catch
                {
                    await ShowAlert($"Could not resolve UUID for '{name}'.");
                }
            }
        }

        private async void BtnRemoveBan_Click(object sender, RoutedEventArgs e)
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
                RefreshManagementData();
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
                    _vm.SendCommand($"ban-ip {ip} \"{reason}\"");

                    if (playerBanCheck.Visibility == Visibility.Visible && playerBanCheck.IsChecked == true)
                    {
                        foreach (var name in matchingPlayers) _vm.SendCommand($"ban {name} \"{reason}\"");
                    }
                    
                    await Task.Delay(500);
                }
                else
                {
                    await _vm.AddIpBan(ip, reason);
                }
                RefreshManagementData();
            }
        }

        private async void BtnRemoveIpBan_Click(object sender, RoutedEventArgs e)
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
                RefreshManagementData();
            }
        }

        private async Task<PlayerData> PromptPlayerSelection(List<PlayerData> candidates, string title)
        {
            var lv = new ListView { SelectionMode = ListViewSelectionMode.Single, ItemsSource = candidates, Height = 300, ItemTemplate = (DataTemplate)this.Resources["PlayerPickerTemplate"] };
            var dialog = new ContentDialog { Title = title, Content = lv, PrimaryButtonText = "Select", SecondaryButtonText = "Cancel", XamlRoot = this.XamlRoot };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary) return lv.SelectedItem as PlayerData;
            return null;
        }

        private async Task ShowBanDialog(string username)
        {
            var reasonBox = new TextBox { PlaceholderText = "Reason (Optional)", Margin = new Thickness(0, 0, 0, 10) };
            var ipCheck = new CheckBox { Content = "Also ban IP Address" };

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
                _vm.SendCommand($"ban {username} \"{reason}\"");

                if (ipCheck.IsChecked == true) _vm.SendCommand($"ban-ip {username} \"{reason}\"");

                await Task.Delay(500);
                RefreshManagementData();
            }
        }

        private async Task<string> PromptString(string title)
        {
            var box = new TextBox();
            var dialog = new ContentDialog { Title = title, Content = box, PrimaryButtonText = "OK", SecondaryButtonText = "Cancel", XamlRoot = this.XamlRoot };
            return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
        }

        private async Task ShowAlert(string msg)
        {
            await new ContentDialog { Title = "Info", Content = msg, PrimaryButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();
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

        private void Menu_Op_Click(object sender, RoutedEventArgs e) { if (_selectedPlayer != null) _vm.SendCommand($"op {_selectedPlayer.Username}"); }
        private void Menu_Deop_Click(object sender, RoutedEventArgs e) { if (_selectedPlayer != null) _vm.SendCommand($"deop {_selectedPlayer.Username}"); }
        private void Menu_Gm_Click(object sender, RoutedEventArgs e) { if (_selectedPlayer != null && sender is MenuFlyoutItem item) _vm.SendCommand($"gamemode {item.Tag} {_selectedPlayer.Username}"); }
        private async void Menu_Kick_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;
            string reason = await PromptString($"Kick {_selectedPlayer.Username}? (Reason)") ?? "Kicked by operator";
            _vm.SendCommand($"kick {_selectedPlayer.Username} \"{reason}\"");
        }

        private async void Menu_Ban_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;
            await ShowBanDialog(_selectedPlayer.Username);
        }

        private void Menu_BanIp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer != null) _vm.SendCommand($"ban-ip {_selectedPlayer.Username}");
        }
    }
}