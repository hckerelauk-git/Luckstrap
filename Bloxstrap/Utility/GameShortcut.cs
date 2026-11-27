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

            name = name.Trim();

            return String.IsNullOrEmpty(name) ? "shortcut" : name;
        }

        /// <summary>
        /// 取游戏的方形 .ico。有缓存直接用，取不到返回 null，调用方退回启动器图标
        /// </summary>
        public static async Task<string?> GetIconAsync(long placeId, string? thumbnailUrl = null)
        {
            // 查 universeId、拉详情、下图，三步各有自己的超时，串起来最坏能到一百多秒。
            // 这里给个总上限，超时就直接用启动器图标，不能让按钮一直转
            Task<string?> work = BuildIconAsync(placeId, thumbnailUrl);

            Task finished = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(20)));

            if (finished != work)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Building icon for place {placeId} timed out");
                return null;
            }

            return await work;
        }

        private static async Task<string?> BuildIconAsync(long placeId, string? thumbnailUrl)
        {
            try
            {
                string path = Path.Combine(IconFolder, $"{placeId}.ico");

                if (File.Exists(path) && new FileInfo(path).Length > 0)
                    return path;

                string? url = thumbnailUrl;

                // 老收藏里存的是 128×128 的缩略图，做成图标会糊，遇到这种就当没传
                if (!String.IsNullOrEmpty(url) && (url.Contains("/128/128/") || url.Contains("size=128x128")))
                    url = null;

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

                // 先写临时文件再换过去，中途失败不会留下半截 .ico 被当成缓存
                string temp = path + ".tmp";

                try
                {
                    IconWriter.WriteSquare(image, temp);
                    File.Move(temp, path, true);
                }
                finally
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }

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

            // 用当前正在运行的 exe，不是安装目录拼出来的路径。
            // 便携运行时 Paths.Application 可能指向一个不存在的文件，双击快捷方式会没反应
            string target = Paths.Process;

            if (String.IsNullOrEmpty(target) || !File.Exists(target))
                target = Paths.Application;

            foreach (string folder in folders)
            {
                string path = Path.Combine(folder, fileName);

                if (File.Exists(path))
                    File.Delete(path);

                ShellLink.Shortcut
                    .CreateShortcut(target, $"\"{link}\"", iconPath ?? target, 0)
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
