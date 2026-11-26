using System.IO;
using Bloxstrap.Integrations;
using Bloxstrap.Models.Entities;

namespace Bloxstrap.Utility
{
    /// <summary>
    /// 游戏快捷方式的公共逻辑：图标、文件名、.lnk 落盘。
    /// 设置页和收藏页都用这一份，免得两边行为对不上。
    /// </summary>
    internal static class GameShortcut
    {
        private const string LOG_IDENT = "GameShortcut";

        public static string IconFolder => Path.Combine(Paths.Base, "ShortcutIcons");

        /// <summary>
        /// 文件名里不能带的字符换成下划线
        /// </summary>
        public static string SanitizeName(string name)
        {
            name = (name ?? String.Empty).Trim();

            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }

        /// <summary>
        /// 取游戏的方形 .ico。有缓存直接用，取不到返回 null，调用方退回启动器图标
        /// </summary>
        public static async Task<string?> GetIconAsync(long placeId, string? thumbnailUrl = null)
        {
            try
            {
                string path = Path.Combine(IconFolder, $"{placeId}.ico");

                if (File.Exists(path) && new FileInfo(path).Length > 0)
                    return path;

                string? url = thumbnailUrl;

                if (String.IsNullOrEmpty(url))
                {
                    long universeId = await FavoriteStore.ResolveUniverseId(placeId);

                    if (universeId == 0)
                        return null;

                    await UniverseDetails.FetchSingle(universeId);

                    url = UniverseDetails.LoadFromCache(universeId)?.Thumbnail?.ImageUrl;
                }

                if (String.IsNullOrEmpty(url))
                    return null;

                // 封面在 rbxcdn 上，那条链路要走 IPv4，用专门的客户端
                byte[] image = await App.ImageHttpClient.GetByteArrayAsync(url);

                Directory.CreateDirectory(IconFolder);
                IconWriter.WriteSquare(image, path);

                return File.Exists(path) ? path : null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to build icon for place {placeId}");
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        /// <summary>
        /// 在指定目录建 .lnk。已存在先删，不然改了服务器 ID 也不更新
        /// </summary>
        public static List<string> Create(string name, string link, IEnumerable<string> folders, string? iconPath)
        {
            string fileName = SanitizeName(name) + ".lnk";
            var created = new List<string>();

            foreach (string folder in folders)
            {
                string path = Path.Combine(folder, fileName);

                if (File.Exists(path))
                    File.Delete(path);

                ShellLink.Shortcut
                    .CreateShortcut(Paths.Application, $"\"{link}\"", iconPath ?? Paths.Application, 0)
                    .WriteToFile(path);

                created.Add(path);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Created {created.Count} shortcut(s) named {fileName}");

            return created;
        }

        public static List<string> Delete(string name, IEnumerable<string> folders)
        {
            string fileName = SanitizeName(name) + ".lnk";
            var deleted = new List<string>();

            foreach (string folder in folders)
            {
                string path = Path.Combine(folder, fileName);

                if (!File.Exists(path))
                    continue;

                File.Delete(path);
                deleted.Add(path);
            }

            return deleted;
        }
    }
}
