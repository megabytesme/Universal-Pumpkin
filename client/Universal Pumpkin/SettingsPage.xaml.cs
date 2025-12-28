using System;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

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

            _loading = false;
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
                    cb.SelectedItem = item;
                    break;
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
            if (parts.Length == 2)
            {
                await ConfigHelper.SaveValueAsync(parts[0], parts[1], c.IsOn.ToString().ToLower());
            }
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
            if (c.SelectedItem is ComboBoxItem item)
            {
                await ConfigHelper.SaveValueAsync("", c.Tag.ToString(), item.Content.ToString());
            }
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
            var folder = ApplicationData.Current.LocalFolder;
            try
            {
                try { var w = await folder.GetFolderAsync("world"); await w.DeleteAsync(); } catch { }
                try { var l = await folder.GetFileAsync("level.dat"); await l.DeleteAsync(); } catch { }
                try { var lb = await folder.GetFileAsync("level.dat_old"); await lb.DeleteAsync(); } catch { }
                try { var p = await folder.GetFolderAsync("playerdata"); await p.DeleteAsync(); } catch { }

                var btn = (Button)sender;
                btn.Content = "Data Deleted! Restart App.";
                btn.IsEnabled = false;
            }
            catch (Exception ex)
            {
                var btn = (Button)sender;
                btn.Content = "Error: " + ex.Message;
            }
        }

        private async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
        }
    }
}