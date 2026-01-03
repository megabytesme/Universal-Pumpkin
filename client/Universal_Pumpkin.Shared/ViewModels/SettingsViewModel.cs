using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Universal_Pumpkin.Services;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Universal_Pumpkin.ViewModels
{
    public class SettingsViewModel
    {
        public async Task<string> GetFormattedStorageSizeAsync()
        {
            StorageFolder serverRoot = await ((App)Application.Current).GetServerFolderAsync();
            long bytes = await CalculateFolderSize(serverRoot);
            return FormatBytes(bytes);
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

        public async Task CreateBackupZip(StorageFile destinationFile)
        {
            StorageFolder serverRoot = await ((App)Application.Current).GetServerFolderAsync();

            using (var stream = await destinationFile.OpenStreamForWriteAsync())
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                await AddFolderToZip(archive, serverRoot, "");
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

        public async Task RestoreBackupZip(StorageFile sourceZip)
        {
            StorageFolder serverRoot = await ((App)Application.Current).GetServerFolderAsync();

            try { var w = await serverRoot.GetFolderAsync("world"); await w.DeleteAsync(); } catch { }
            try { var d = await serverRoot.GetFolderAsync("data"); await d.DeleteAsync(); } catch { }
            try { var conf = await serverRoot.GetFileAsync("configuration.toml"); await conf.DeleteAsync(); } catch { }
            try { var feat = await serverRoot.GetFileAsync("features.toml"); await feat.DeleteAsync(); } catch { }

            using (var stream = await sourceZip.OpenStreamForReadAsync())
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/")) continue;

                    string fullPath = entry.FullName.Replace("/", "\\");
                    string folderPath = Path.GetDirectoryName(fullPath);

                    StorageFolder targetFolder = serverRoot;
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

        public async Task DeleteWorldData(StorageFolder serverRoot)
        {
            if (serverRoot == null)
                serverRoot = ApplicationData.Current.LocalFolder;

            try
            {
                var world = await serverRoot.GetFolderAsync("world");
                await world.DeleteAsync();
            }
            catch { }

            try
            {
                var level = await serverRoot.GetFileAsync("level.dat");
                await level.DeleteAsync();
            }
            catch { }

            try
            {
                var levelOld = await serverRoot.GetFileAsync("level.dat_old");
                await levelOld.DeleteAsync();
            }
            catch { }

            try
            {
                var playerData = await serverRoot.GetFolderAsync("playerdata");
                await playerData.DeleteAsync();
            }
            catch { }
        }

        public string GetAppName()
        {
#if UWP1709
            return "1709_UWP";
#else
            return "1507_UWP";
#endif
        }

        public string GetAppVersion()
        {
            var v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }

        public string GetArchitecture()
        {
            return Package.Current.Id.Architecture.ToString().ToLower();
        }

        public string GetPumpkinVersion() => "0.1.0-dev+1.21.11";
        public string GetMinecraftVersion() => "1.21.11";
        public int GetProtocolVersion() => 774;
        public string GetPumpkinCommitId() => "70b31323967bb99fd4feefab8e96124be369cd6f";
        public Uri GetPumpkinCommitUri() => new Uri("https://github.com/Pumpkin-MC/Pumpkin/tree/" + GetPumpkinCommitId());
    }
}