using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using Universal_Pumpkin.Models;
using Windows.UI.Xaml;

namespace Universal_Pumpkin
{
    public static class ManagementHelper
    {
        private const string DataFolder = "data";

        private static async Task<StorageFolder> GetDataFolderAsync()
        {
            StorageFolder serverRoot = await ((App)Application.Current).GetServerFolderAsync();
            return await serverRoot.CreateFolderAsync(DataFolder, CreationCollisionOption.OpenIfExists);
        }

        private static async Task<T> LoadListAsync<T>(string filename) where T : class, new()
        {
            try
            {
                StorageFolder dataFolder = await GetDataFolderAsync();
                StorageFile file = await dataFolder.GetFileAsync(filename);

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
                StorageFolder dataFolder = await GetDataFolderAsync();
                StorageFile file = await dataFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists);

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
