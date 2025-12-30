using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Universal_Pumpkin.Models;

namespace Universal_Pumpkin
{
    public sealed partial class PlayersPage : Page
    {
        private DispatcherTimer _pollTimer;
        public ObservableCollection<PlayerData> PlayersList { get; } = new ObservableCollection<PlayerData>();

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