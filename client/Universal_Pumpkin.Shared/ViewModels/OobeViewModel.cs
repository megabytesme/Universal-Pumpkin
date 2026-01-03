using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Storage.Pickers;
using Universal_Pumpkin.Helpers;
using Universal_Pumpkin.Models;
using Windows.UI.Xaml;

namespace Universal_Pumpkin.ViewModels
{
    public class OobeViewModel
    {
        public event EventHandler<string> StatusMessage;
        public event EventHandler RestoreCompleted;

        public async Task<OobePermissionStatus> RequestBackgroundPermission()
        {
            bool allowed = await BackgroundKeeper.RequestKeepAlive();
            if (allowed)
                return OobePermissionStatus.Allowed;

            var status = await BackgroundExecutionManager.RequestAccessAsync();

#if UWP1709
            if (status == BackgroundAccessStatus.Denied ||
                status == BackgroundAccessStatus.DeniedByUser)
                return OobePermissionStatus.Denied;
#else
            if (status == BackgroundAccessStatus.Denied)
                return OobePermissionStatus.Denied;
#endif

            return OobePermissionStatus.Restricted;
        }

        public async Task RestoreBackup()
        {
            var openPicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.Desktop
            };
            openPicker.FileTypeFilter.Add(".zip");

            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    StatusMessage?.Invoke(this, "Restoring data...");
                    await ExtractBackupZip(file);
                    StatusMessage?.Invoke(this, "Restore complete! You can now finish setup.");
                    RestoreCompleted?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    StatusMessage?.Invoke(this, $"Error: {ex.Message}");
                }
            }
        }

        private async Task ExtractBackupZip(StorageFile sourceZip)
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

                    var file = await targetFolder.CreateFileAsync(
                        Path.GetFileName(fullPath),
                        CreationCollisionOption.ReplaceExisting);

                    using (var entryStream = entry.Open())
                    using (var fileStream = await file.OpenStreamForWriteAsync())
                    {
                        await entryStream.CopyToAsync(fileStream);
                    }
                }
            }
        }

        public void CompleteOobe()
        {
            Services.FirstRunService.MarkAsCompleted();
        }
    }
}
