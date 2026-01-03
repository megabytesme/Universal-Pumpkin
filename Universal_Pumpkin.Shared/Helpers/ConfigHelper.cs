using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Universal_Pumpkin
{
    public static class ConfigHelper
    {
        private static readonly List<string> ConfigFiles = new List<string> { "configuration.toml", "features.toml" };
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        private static async Task<string> ReadFileAsync(string filename)
        {
            try
            {
                StorageFolder serverRoot = await ((App)Application.Current).GetServerFolderAsync();

                var item = await serverRoot.TryGetItemAsync(filename);
                if (item is StorageFile file)
                {
                    return await FileIO.ReadTextAsync(file);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> GetValueAsync(string section, string key)
        {
            foreach (var file in ConfigFiles)
            {
                string val = await GetValueFromFileAsync(file, section, key);
                if (val != null) return val;
            }
            return null;
        }

        private static async Task<string> GetValueFromFileAsync(string filename, string section, string key)
        {
            string content = await ReadFileAsync(filename);
            if (string.IsNullOrEmpty(content)) return null;

            string blockToSearch;

            if (string.IsNullOrEmpty(section))
            {

                var firstSectionMatch = Regex.Match(content, @"^\s*\[.*\]", RegexOptions.Multiline);
                int limit = firstSectionMatch.Success ? firstSectionMatch.Index : content.Length;
                blockToSearch = content.Substring(0, limit);
            }
            else
            {

                string sectionPattern = $@"^\s*\[{Regex.Escape(section)}\]";
                var sectionMatch = Regex.Match(content, sectionPattern, RegexOptions.Multiline);

                if (!sectionMatch.Success) return null;

                int start = sectionMatch.Index + sectionMatch.Length;
                string afterHeader = content.Substring(start);

                var nextSectionMatch = Regex.Match(afterHeader, @"^\s*\[.*\]", RegexOptions.Multiline);
                int limit = nextSectionMatch.Success ? nextSectionMatch.Index : afterHeader.Length;

                blockToSearch = afterHeader.Substring(0, limit);
            }

            var match = Regex.Match(blockToSearch, $@"^\s*{Regex.Escape(key)}\s*=\s*(.*)", RegexOptions.Multiline);

            if (match.Success)
            {
                string val = match.Groups[1].Value.Trim();

                int commentIndex = val.IndexOf('#');
                if (commentIndex > -1) val = val.Substring(0, commentIndex).Trim();

                val = val.Trim('"').Trim('\'');
                return val;
            }

            return null;
        }

        public static async Task SaveValueAsync(string section, string key, string newValue)
        {
            await _fileLock.WaitAsync();
            try
            {

                newValue = newValue.Trim('"');

                if (key == "seed")
                {

                    newValue = $"\"{newValue}\"";
                }
                else if (newValue.ToLower() == "true" || newValue.ToLower() == "false")
                {
                    newValue = newValue.ToLower();
                }
                else if (long.TryParse(newValue, out _) || double.TryParse(newValue, out _))
                {

                }
                else
                {

                    newValue = $"\"{newValue}\"";
                }

                string targetFile = "configuration.toml";
                bool keyFound = false;

                foreach (var file in ConfigFiles)
                {
                    if (await GetValueFromFileAsync(file, section, key) != null)
                    {
                        targetFile = file;
                        keyFound = true;
                        break;
                    }
                }

                if (!keyFound && !string.IsNullOrEmpty(section))
                {
                    foreach (var file in ConfigFiles)
                    {
                        string content = await ReadFileAsync(file);
                        if (content != null && Regex.IsMatch(content, $@"^\s*\[{Regex.Escape(section)}\]", RegexOptions.Multiline))
                        {
                            targetFile = file;
                            break;
                        }
                    }
                }

                await WriteValueToFileAsync(targetFile, section, key, newValue);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private static async Task WriteValueToFileAsync(string filename, string section, string key, string val)
        {
            StorageFolder serverRoot = await ((App)Application.Current).GetServerFolderAsync();
            StorageFile file;

            try
            {
                file = await serverRoot.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists);
            }
            catch
            {
                return;
            }

            string content = await FileIO.ReadTextAsync(file);

            if (string.IsNullOrEmpty(section))
            {
                string pattern = $@"^(\s*{Regex.Escape(key)}\s*=\s*)(.*)";

                var firstSectionMatch = Regex.Match(content, @"^\s*\[.*\]", RegexOptions.Multiline);
                int limit = firstSectionMatch.Success ? firstSectionMatch.Index : content.Length;

                string rootPart = content.Substring(0, limit);
                string restPart = content.Substring(limit);

                if (Regex.IsMatch(rootPart, pattern, RegexOptions.Multiline))
                {
                    string newRoot = Regex.Replace(rootPart, pattern, m => m.Groups[1].Value + val, RegexOptions.Multiline);
                    await FileIO.WriteTextAsync(file, newRoot + restPart);
                }
                else
                {

                    string newContent = $"{key} = {val}\r\n" + content;
                    await FileIO.WriteTextAsync(file, newContent);
                }
            }

            else
            {
                string sectionHeader = $@"^\s*\[{Regex.Escape(section)}\]";
                var sectionMatch = Regex.Match(content, sectionHeader, RegexOptions.Multiline);

                if (sectionMatch.Success)
                {

                    int start = sectionMatch.Index + sectionMatch.Length;
                    string preSection = content.Substring(0, start);
                    string afterHeader = content.Substring(start);

                    var nextSectionMatch = Regex.Match(afterHeader, @"^\s*\[.*\]", RegexOptions.Multiline);
                    int limit = nextSectionMatch.Success ? nextSectionMatch.Index : afterHeader.Length;

                    string sectionBlock = afterHeader.Substring(0, limit);
                    string postSection = afterHeader.Substring(limit);

                    string keyPattern = $@"^(\s*{Regex.Escape(key)}\s*=\s*)(.*)";

                    if (Regex.IsMatch(sectionBlock, keyPattern, RegexOptions.Multiline))
                    {

                        string newBlock = Regex.Replace(sectionBlock, keyPattern, m => m.Groups[1].Value + val, RegexOptions.Multiline);
                        await FileIO.WriteTextAsync(file, preSection + newBlock + postSection);
                    }
                    else
                    {

                        string newBlock = $"\r\n{key} = {val}" + sectionBlock;
                        await FileIO.WriteTextAsync(file, preSection + newBlock + postSection);
                    }
                }
                else
                {

                    string newSectionBlock = $"\r\n\r\n[{section}]\r\n{key} = {val}";
                    await FileIO.WriteTextAsync(file, content + newSectionBlock);
                }
            }
        }
    }
}