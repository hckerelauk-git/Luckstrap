using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Bloxstrap.Utility
{
    /// <summary>
    /// Roblox 的登录态存在 %LOCALAPPDATA%\Roblox\LocalStorage\RobloxCookies.dat。
    /// 文件本身是个 json：{ "CookiesVersion": "1", "CookiesData": "&lt;base64&gt;" }，
    /// base64 解开是 DPAPI（当前用户）加密过的一坨 Netscape 格式 cookie 文本，
    /// 里面 \t.ROBLOSECURITY\t 后面那串就是登录凭据。
    ///
    /// 注意字段名：实际文件用的是 CookiesVersion / CookiesData，
    /// Fishstrap 源码里那份 RobloxCookies 模型写的是 Version / Cookies，已经对不上了，别照抄。
    ///
    /// 这里只做三件事：读出那段 base64、原样写回去、解开看里面是谁。
    /// 写回的时候只改 CookiesData 一个字段，别的字段留着不动。
    /// </summary>
    internal static class RobloxCookies
    {
        private const string AuthCookieName = ".ROBLOSECURITY";
        private const string SupportedVersion = "1";

        // Roblox 那边的格式是 \t.ROBLOSECURITY\t<值>;
        private const string AuthPattern = @"\t\.ROBLOSECURITY\t(.+?)(;|$)";

        private const string VersionKey = "CookiesVersion";
        private const string DataKey = "CookiesData";

        public static string FilePath => Path.Combine(Paths.LocalAppData, "Roblox", "LocalStorage", "RobloxCookies.dat");

        public static bool Exists => File.Exists(FilePath);

        /// <summary>读出文件里那段 base64。没有文件、格式不对都返回 null。</summary>
        public static string? ReadBlob()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                var node = JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject;

                if (node is null || !node.TryGetPropertyValue(DataKey, out var data) || data is null)
                    return null;

                return data.GetValue<string>();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RobloxCookies::ReadBlob", ex.Message);
                return null;
            }
        }

        /// <summary>把 base64 写回去。只动 CookiesData 字段，其他字段原样保留。</summary>
        public static void WriteBlob(string base64)
        {
            JsonObject node;

            try
            {
                node = (File.Exists(FilePath) ? JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject : null) ?? new JsonObject();
            }
            catch (Exception ex)
            {
                // 原文件坏了就重开一个，反正里面只有 CookiesData 有用
                App.Logger.WriteLine("RobloxCookies::WriteBlob", $"原文件解析失败，重建：{ex.Message}");
                node = new JsonObject();
            }

            if (!node.ContainsKey(VersionKey))
                node[VersionKey] = SupportedVersion;

            node[DataKey] = base64;

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            // 原文件是紧凑的单行 json，这里也写成紧凑的，尽量少改它的样子
            string contents = node.ToJsonString();

            // 先写临时文件再换。这个文件是 Roblox 的登录态，
            // 写到一半被打断就等于把人家账号踢下线，还得重新登
            string temp = FilePath + ".tmp";

            File.WriteAllText(temp, contents);

            try
            {
                if (File.Exists(FilePath))
                    File.Replace(temp, FilePath, null, true);
                else
                    File.Move(temp, FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 被 Roblox 占着的时候替换会失败，退回直接覆盖
                App.Logger.WriteLine("RobloxCookies::WriteBlob", $"原子替换失败，改为直接覆盖：{ex.Message}");

                File.Move(temp, FilePath, true);
            }
        }

        /// <summary>解开 base64 取出 .ROBLOSECURITY 的值。解不开或者没找到都返回 null。</summary>
        public static string? ReadAuthCookie(string base64)
        {
            try
            {
                byte[] encrypted = Convert.FromBase64String(base64);
                byte[] raw = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);

                string text = Encoding.UTF8.GetString(raw);
                Match match = Regex.Match(text, AuthPattern, RegexOptions.Singleline);

                return match.Success ? match.Groups[1].Value : null;
            }
            catch (Exception ex)
            {
                // 常见原因：换了 Windows 用户、换了电脑、Roblox 改了格式
                App.Logger.WriteLine("RobloxCookies::ReadAuthCookie", ex.Message);
                return null;
            }
        }

        /// <summary>写之前先留一份原始文件，第一次切换时生成，之后不再覆盖。</summary>
        public static void BackupOnce()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return;

                string backup = FilePath + ".luckstrap-bak";

                if (!File.Exists(backup))
                    File.Copy(FilePath, backup);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RobloxCookies::BackupOnce", ex.Message);
            }
        }
    }
}
