using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Universal_Pumpkin.Models;

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

        private void Player_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ShowMenu(sender, e.OriginalSource);
        }

        private void Player_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                ShowMenu(sender, e.OriginalSource);
            }
        }

        private void ShowMenu(object sender, object originalSource)
        {
            if (sender is FrameworkElement element)
            {
                _selectedPlayer = element.DataContext as PlayerData;

                if (_selectedPlayer != null)
                {
                    FlyoutBase.ShowAttachedFlyout(element);
                }
            }
        }

        private void Menu_Op_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;
            App.Server.SendCommand($"op {_selectedPlayer.Username}");
        }

        private void Menu_Deop_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;
            App.Server.SendCommand($"deop {_selectedPlayer.Username}");
        }

        private void Menu_Gm_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;
            if (sender is MenuFlyoutItem item && item.Tag != null)
            {
                string mode = item.Tag.ToString();
                App.Server.SendCommand($"gamemode {mode} {_selectedPlayer.Username}");
            }
        }

        private async void Menu_Kick_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;

            TextBox input = new TextBox { PlaceholderText = "Reason (Optional)" };
            ContentDialog dialog = new ContentDialog
            {
                Title = $"Kick {_selectedPlayer.Username}?",
                Content = input,
                PrimaryButtonText = "Kick",
                SecondaryButtonText = "Cancel"
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string reason = string.IsNullOrWhiteSpace(input.Text) ? "Kicked by operator" : input.Text;
                App.Server.SendCommand($"kick {_selectedPlayer.Username} \"{reason}\"");
            }
        }

        private async void Menu_Ban_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;

            TextBox input = new TextBox { PlaceholderText = "Reason (Optional)" };
            ContentDialog dialog = new ContentDialog
            {
                Title = $"Ban {_selectedPlayer.Username}?",
                Content = input,
                PrimaryButtonText = "Ban",
                SecondaryButtonText = "Cancel"
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string reason = string.IsNullOrWhiteSpace(input.Text) ? "Banned by operator" : input.Text;
                App.Server.SendCommand($"ban {_selectedPlayer.Username} \"{reason}\"");
            }
        }

        private async void Menu_BanIp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayer == null) return;

            ContentDialog dialog = new ContentDialog
            {
                Title = "Ban IP Address?",
                Content = $"This will ban {_selectedPlayer.IpAddress}. Are you sure?",
                PrimaryButtonText = "Ban IP",
                SecondaryButtonText = "Cancel"
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                App.Server.SendCommand($"ban-ip {_selectedPlayer.Username}");
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (App.Server.IsRunning)
            {
                _pollTimer.Start();
                PollTimer_Tick(null, null);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _pollTimer.Stop();
        }

        private void PollTimer_Tick(object sender, object e)
        {
            if (!App.Server.IsRunning)
            {
                PlayersList.Clear();
                NoPlayersText.Visibility = Visibility.Visible;
                return;
            }

            var freshData = App.Server.GetPlayers();

            if (freshData.Count == 0)
            {
                PlayersList.Clear();
                NoPlayersText.Visibility = Visibility.Visible;
                return;
            }

            NoPlayersText.Visibility = Visibility.Collapsed;

            for (int i = PlayersList.Count - 1; i >= 0; i--)
            {
                var existing = PlayersList[i];
                if (!freshData.Any(p => p.Uuid == existing.Uuid))
                {
                    PlayersList.RemoveAt(i);
                }
            }

            foreach (var fresh in freshData)
            {
                var existing = PlayersList.FirstOrDefault(p => p.Uuid == fresh.Uuid);

                if (existing == null)
                {
                    PlayersList.Add(fresh);
                }
                else
                {
                    int index = PlayersList.IndexOf(existing);
                    PlayersList[index] = fresh;
                }
            }
        }
    }
}