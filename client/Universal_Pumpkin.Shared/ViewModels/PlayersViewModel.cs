using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Universal_Pumpkin.Models;
using Windows.UI.Xaml;

namespace Universal_Pumpkin.ViewModels
{
    public class PlayersViewModel
    {
        private DispatcherTimer _pollTimer;
        public ObservableCollection<PlayerData> PlayersList { get; } = new ObservableCollection<PlayerData>();
        public ObservableCollection<OpEntry> OpsList { get; } = new ObservableCollection<OpEntry>();
        public ObservableCollection<BanEntry> BansList { get; } = new ObservableCollection<BanEntry>();
        public ObservableCollection<IpBanEntry> IpBansList { get; } = new ObservableCollection<IpBanEntry>();

        public bool HasPlayers => PlayersList.Count > 0;

        public event EventHandler PlayersUpdated;

        public PlayersViewModel()
        {
            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromSeconds(1);
            _pollTimer.Tick += PollTimer_Tick;
        }

        public void StartPolling()
        {
            if (App.Server.IsRunning) _pollTimer.Start();
            PollTimer_Tick(null, null);
        }

        public void StopPolling() => _pollTimer.Stop();

        private void PollTimer_Tick(object sender, object e)
        {
            if (!App.Server.IsRunning)
            {
                PlayersList.Clear();
                PlayersUpdated?.Invoke(this, EventArgs.Empty);
                return;
            }

            var freshData = App.Server.GetPlayers();
            if (freshData.Count == 0)
            {
                PlayersList.Clear();
                PlayersUpdated?.Invoke(this, EventArgs.Empty);
                return;
            }

            for (int i = PlayersList.Count - 1; i >= 0; i--)
            {
                var existing = PlayersList[i];
                if (!freshData.Any(p => p.Uuid == existing.Uuid)) PlayersList.RemoveAt(i);
            }

            foreach (var fresh in freshData)
            {
                var existing = PlayersList.FirstOrDefault(p => p.Uuid == fresh.Uuid);
                if (existing == null) PlayersList.Add(fresh);
                else
                {
                    int index = PlayersList.IndexOf(existing);
                    PlayersList[index] = fresh;
                }
            }
            PlayersUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task LoadOps()
        {
            try
            {
                var ops = await ManagementHelper.LoadOps();
                foreach (var op in ops)
                {
                    if (App.Server.IsDeopQueued(op.Name)) op.IsPendingRemoval = true;
                }
                OpsList.Clear();
                foreach (var o in ops) OpsList.Add(o);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public async Task LoadBans()
        {
            try
            {
                var bans = await ManagementHelper.LoadBans();
                BansList.Clear();
                foreach (var b in bans) BansList.Add(b);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public async Task LoadIpBans()
        {
            try
            {
                var bans = await ManagementHelper.LoadIpBans();
                IpBansList.Clear();
                foreach (var b in bans) IpBansList.Add(b);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public void SendCommand(string cmd) => App.Server.SendCommand(cmd);

        public async Task AddOp(string name)
        {
            string uuid = await MojangApiHelper.GetUuidFromUsernameAsync(name);
            if (uuid == null) throw new Exception("UUID not found");

            var list = await ManagementHelper.LoadOps();
            if (!list.Any(x => x.Uuid == uuid))
            {
                list.Add(new OpEntry { Name = name, Uuid = uuid, Level = 4, BypassesPlayerLimit = false });
                await ManagementHelper.SaveOps(list);
            }
        }

        public async Task RemoveOp(OpEntry op)
        {
            var list = await ManagementHelper.LoadOps();
            list.RemoveAll(x => x.Name == op.Name || x.Uuid == op.Uuid);
            await ManagementHelper.SaveOps(list);
        }

        public async Task AddBan(string name, string reason)
        {
            string uuid = await MojangApiHelper.GetUuidFromUsernameAsync(name);
            if (uuid == null) throw new Exception("UUID not found");

            var list = await ManagementHelper.LoadBans();
            list.Add(new BanEntry
            {
                Name = name,
                Uuid = uuid,
                Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:sszzz"),
                Source = "Console",
                Expires = "forever",
                Reason = reason
            });
            await ManagementHelper.SaveBans(list);
        }

        public async Task RemoveBan(BanEntry ban)
        {
            var list = await ManagementHelper.LoadBans();
            list.RemoveAll(x => x.Name == ban.Name);
            await ManagementHelper.SaveBans(list);
        }

        public async Task AddIpBan(string ip, string reason)
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
        }

        public async Task RemoveIpBan(IpBanEntry ipBan)
        {
            var list = await ManagementHelper.LoadIpBans();
            list.RemoveAll(x => x.Ip == ipBan.Ip);
            await ManagementHelper.SaveIpBans(list);
        }
    }
}