using System.Text.Encodings.Web;

using Bloxstrap.Utility;

namespace Bloxstrap
{
    /// <summary>一个存下来的账号。CookieBlob 是 RobloxCookies.dat 里那段 base64，原样存，不做二次加密。</summary>
    public class SavedAccount
    {
        public string Name { get; set; } = "";
        public long UserId { get; set; }
        public string DisplayName { get; set; } = "";
        public DateTime SavedAt { get; set; } = DateTime.Now;
        public string CookieBlob { get; set; } = "";
    }

    /// <summary>
    /// 多账号切换。做法跟 Fishstrap 一样：把 Roblox 自己的登录态文件复制一份存着，切换时写回去。
    /// 不存密码、不碰客户端进程、不多开游戏，一次只有一份登录态，跟手动在网页上退出再登录是一个效果。
    /// 注意 DPAPI 是绑当前 Windows 用户的，换电脑或换 Windows 账号这些存档都用不了。
    /// </summary>
    internal static class AccountManager
    {
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string FilePath => Path.Combine(Paths.Base, "Accounts.json");

        public static List<SavedAccount> Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new List<SavedAccount>();

                var accounts = JsonSerializer.Deserialize<List<SavedAccount>>(File.ReadAllText(FilePath));

                return accounts ?? new List<SavedAccount>();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountManager::Load", ex.ToString());
                return new List<SavedAccount>();
            }
        }

        public static void Save(List<SavedAccount> accounts)
        {
            Directory.CreateDirectory(Paths.Base);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(accounts, WriteOptions));
        }

        /// <summary>Roblox 开着的时候不要写登录态文件，不然会被它自己覆盖回去。</summary>
        public static bool IsRobloxRunning()
        {
            string[] names = { "RobloxPlayerBeta", "Windows10Universal" };

            foreach (string name in names)
            {
                try
                {
                    if (Process.GetProcessesByName(name).Length > 0)
                        return true;
                }
                catch
                {
                    // 拿不到进程列表就当没开，后面写文件失败会自己报错
                }
            }

            return false;
        }

        /// <summary>拿 cookie 问 Roblox 这是谁。失败返回 null，不影响账号本身能用。</summary>
        public static async Task<AuthenticatedUser?> WhoAmI(string authCookie)
        {
            const string LOG_IDENT = "AccountManager::WhoAmI";

            // 用 App.HttpClient，别自己 new 一个：那个走的是系统代理 + 固定的 TLS 配置，
            // 跟查游戏、查服务器用的是同一条链路。单独 new 出来的在部分网络下会握手失败。
            // 日志处理器只记方法和 URL，不记请求头，cookie 不会落到日志里
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
                request.Headers.TryAddWithoutValidation("User-Agent", "Roblox/WinInet");
                request.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={authCookie}");

                using var response = await App.HttpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"HTTP {(int)response.StatusCode}，cookie 可能过期了");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<AuthenticatedUser>(json);

                if (user is null || user.Id == 0)
                    return null;

                return user;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, ex.Message);
                return null;
            }
        }
    }

    public class AuthenticatedUser
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";
    }
}
