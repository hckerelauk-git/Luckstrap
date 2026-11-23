using System.Text.Json;

namespace Bloxstrap.Integrations
{
    /// <summary>当前登录的账号。UserId 为 0 表示没读到。</summary>
    public class AccountSnapshot
    {
        public long UserId { get; set; }
        public string Name { get; set; } = String.Empty;
        public string DisplayName { get; set; } = String.Empty;

        public string Title => String.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;

        public bool IsValid => UserId != 0 && !String.IsNullOrWhiteSpace(Name);
    }

    /// <summary>
    /// 读当前登录的是谁。
    ///
    /// 先拿当前 cookie blob 去 Luckstrap 自己存的 Accounts.json 里对号 —— 这是
    /// 唯一能反映「刚刚在设置里换过号」的来源，因为换号只改 RobloxCookies.dat，
    /// appStorage.json 要等 Roblox 下次启动才会跟着更新。
    ///
    /// 对不上再退到 Roblox 自己的 appStorage.json —— 它就在本地，登录之后 Roblox
    /// 会把 Username / UserId / DisplayName 写进去，不需要网络，代理挂了、
    /// users.roblox.com 被墙了也照样能显示。
    ///
    /// 两层都读不到才问接口。任何一层成功就够了。
    /// </summary>
    internal static class CurrentAccount
    {
        private const string LOG_IDENT = "CurrentAccount";

        private static string AppStoragePath => Path.Combine(Paths.LocalAppData, "Roblox", "LocalStorage", "appStorage.json");

        /// <summary>Roblox 客户端自己记的账号，离线可用。</summary>
        public static AccountSnapshot? ReadLocal()
        {
            try
            {
                if (!File.Exists(AppStoragePath))
                    return null;

                using var doc = JsonDocument.Parse(File.ReadAllText(AppStoragePath));
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return null;

                string name = ReadString(root, "Username");
                string displayName = ReadString(root, "DisplayName");
                string rawId = ReadString(root, "UserId");

                if (String.IsNullOrWhiteSpace(name) || !Int64.TryParse(rawId, out long userId) || userId <= 0)
                    return null;

                return new AccountSnapshot
                {
                    UserId = userId,
                    Name = name,
                    DisplayName = displayName
                };
            }
            catch (Exception ex)
            {
                // 文件正被 Roblox 写着、或者格式变了，都当读不到，走下一层
                App.Logger.WriteLine(LOG_IDENT, $"appStorage 读取失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>从 Luckstrap 存过的账号里按 cookie blob 找同一个号。</summary>
        public static AccountSnapshot? ReadSaved(string? cookieBlob)
        {
            if (String.IsNullOrEmpty(cookieBlob))
                return null;

            try
            {
                var saved = AccountManager.Load().FirstOrDefault(x => x.CookieBlob == cookieBlob && x.UserId != 0);

                if (saved is null)
                    return null;

                return new AccountSnapshot
                {
                    UserId = saved.UserId,
                    Name = saved.Name,
                    DisplayName = saved.DisplayName
                };
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Accounts.json 读取失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 当前登录态对应的账号。本地那份先用着，有 cookie 就再问一次接口，
        /// 问到了以接口为准（昵称改过、换过号都能跟上），问不到就保持本地那份。
        /// 第二个返回值表示接口这次是否成功。
        /// </summary>
        public static async Task<(AccountSnapshot? Account, bool FromNetwork)> ResolveAsync(string? cookieBlob)
        {
            AccountSnapshot? local = ReadSaved(cookieBlob) ?? ReadLocal();

            if (String.IsNullOrEmpty(cookieBlob))
                return (local, false);

            string? cookie = RobloxCookies.ReadAuthCookie(cookieBlob);

            if (String.IsNullOrEmpty(cookie))
                return (local, false);

            var user = await AccountManager.WhoAmI(cookie);

            if (user is null)
                return (local, false);

            return (new AccountSnapshot
            {
                UserId = user.Id,
                Name = user.Name,
                DisplayName = user.DisplayName
            }, true);
        }

        private static string ReadString(JsonElement root, string key)
        {
            if (!root.TryGetProperty(key, out var value))
                return String.Empty;

            // 字段可能是字符串，也可能是数字（UserId 两种都见过）
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? String.Empty,
                JsonValueKind.Number => value.ToString(),
                _ => String.Empty
            };
        }
    }
}
