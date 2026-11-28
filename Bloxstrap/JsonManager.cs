using System.Runtime.CompilerServices;

namespace Bloxstrap
{
    public class JsonManager<T> where T : class, new()
    {
        public T OriginalProp { get; set; } = new();
        
        public T Prop { get; set; } = new();

        /// <summary>
        /// The file hash when last retrieved from disk
        /// </summary>
        public string? LastFileHash { get; private set; }

        public bool Loaded { get; set; } = false;

        public virtual string ClassName => typeof(T).Name;

        public virtual string FileLocation => Path.Combine(Paths.Base, $"{ClassName}.json");

        public virtual string LOG_IDENT_CLASS => $"JsonManager<{ClassName}>";

        public virtual void Load(bool alertFailure = true)
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Load";

            App.Logger.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

            try
            {
                string contents = File.ReadAllText(FileLocation);

                T? settings = JsonSerializer.Deserialize<T>(contents);

                if (settings is null)
                    throw new ArgumentNullException("Deserialization returned null");

                Prop = settings;
                Loaded = true;
                LastFileHash = MD5Hash.FromString(contents);

                App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load!");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (alertFailure)
                {
                    string message = "";

                    if (ClassName == nameof(Settings))
                        message = Strings.JsonManager_SettingsLoadFailed;
                    else if (ClassName == nameof(FastFlagManager))
                        message = Strings.JsonManager_FastFlagsLoadFailed;

                    if (!String.IsNullOrEmpty(message))
                        Frontend.ShowMessageBox($"{message}\n\n{ex.Message}", System.Windows.MessageBoxImage.Warning);
                }

                // 先把读不动的原文件留一份，再写默认值。
                // 以前是先 Save() 再备份，等于把用户原来的内容直接盖掉，出了问题也没法查
                try
                {
                    if (File.Exists(FileLocation))
                        File.Copy(FileLocation, FileLocation + ".corrupt", true);
                }
                catch (Exception copyEx)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to keep a copy of the broken file: {FileLocation}.corrupt");
                    App.Logger.WriteException(LOG_IDENT, copyEx);
                }

                Save();
            }
        }

        public virtual void Save()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Save";
            
            App.Logger.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

            Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);

            try
            {
                string contents = JsonSerializer.Serialize(Prop, new JsonSerializerOptions { WriteIndented = true });

                // 先写临时文件，再整体换过去。
                // 直接覆盖写的话，写到一半崩溃、断电或者磁盘满，会留下半截 json，
                // 下次启动读不出来就按默认值重来，用户的设置就没了
                string tempPath = FileLocation + ".tmp";

                File.WriteAllText(tempPath, contents);

                try
                {
                    if (File.Exists(FileLocation))
                    {
                        // 旧文件会被挪到 .bak，等于每次保存都留了一份上一次能用的副本
                        File.Replace(tempPath, FileLocation, FileLocation + ".bak", true);
                    }
                    else
                    {
                        File.Move(tempPath, FileLocation);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 替换失败通常是被杀软或者另一个实例占着。
                    // 退回直接覆盖，至少别把这次修改丢掉
                    App.Logger.WriteLine(LOG_IDENT, "Atomic replace failed, falling back to direct overwrite");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    File.Move(tempPath, FileLocation, true);
                }

                LastFileHash = MD5Hash.FromString(contents);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to save");
                App.Logger.WriteException(LOG_IDENT, ex);

                try
                {
                    string tempPath = FileLocation + ".tmp";

                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception cleanupEx)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to remove {FileLocation}.tmp");
                    App.Logger.WriteException(LOG_IDENT, cleanupEx);
                }

                string errorMessage = string.Format(Resources.Strings.Bootstrapper_JsonManagerSaveFailed, ClassName, ex.Message);
                Frontend.ShowMessageBox(errorMessage, System.Windows.MessageBoxImage.Warning);

                return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Save complete!");
        }

        /// <summary>
        /// Is the file on disk different to the one deserialised during this session?
        /// </summary>
        public bool HasFileOnDiskChanged()
        {
            // 文件被删掉的情况以前会在这里抛 FileNotFoundException，
            // 调用方（ApplyModifications）没有兜底，整次启动就断在这
            if (!File.Exists(FileLocation))
                return true;

            try
            {
                return LastFileHash != MD5Hash.FromFile(FileLocation);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine(LOG_IDENT_CLASS, "Could not hash the file on disk, assuming it changed");
                App.Logger.WriteException(LOG_IDENT_CLASS, ex);

                return true;
            }
        }
    }
}
