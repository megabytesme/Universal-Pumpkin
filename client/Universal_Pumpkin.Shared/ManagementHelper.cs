using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using Universal_Pumpkin.Models;

namespace Universal_Pumpkin
{
    public static class ManagementHelper
    {
        private const string DataFolder = "data";

        private static async Task<T> LoadListAsync<T>(string filename) where T : class, new()
        {
            try
            {
                var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(DataFolder);
                var file = await folder.GetFileAsync(filename);
                string json = await FileIO.ReadTextAsync(file);
                return JsonConvert.DeserializeObject<T>(json) ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        private static async Task SaveListAsync<T>(string filename, T data)
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var dataFolder = await localFolder.CreateFolderAsync(DataFolder, CreationCollisionOption.OpenIfExists);
                var file = await dataFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists);

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                await FileIO.WriteTextAsync(file, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving {filename}: {ex.Message}");
            }
        }

        public static Task<List<OpEntry>> LoadOps() => LoadListAsync<List<OpEntry>>("ops.json");
        public static Task SaveOps(List<OpEntry> list) => SaveListAsync("ops.json", list);

        public static Task<List<BanEntry>> LoadBans() => LoadListAsync<List<BanEntry>>("banned-players.json");
        public static Task SaveBans(List<BanEntry> list) => SaveListAsync("banned-players.json", list);

        public static Task<List<IpBanEntry>> LoadIpBans() => LoadListAsync<List<IpBanEntry>>("banned-ips.json");
        public static Task SaveIpBans(List<IpBanEntry> list) => SaveListAsync("banned-ips.json", list);
    }
}