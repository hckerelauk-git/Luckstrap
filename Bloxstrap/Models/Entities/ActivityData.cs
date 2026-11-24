using System.Web;
using System.Windows;
using System.Windows.Input;
using Bloxstrap.AppData;
using Bloxstrap.Models.APIs;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.Models.Entities
{
    public class ActivityData
    {
        private long _universeId = 0;

        /// <summary>
        /// If the current activity stems from an in-universe teleport, then this will be
        /// set to the activity that corresponds to the initial game join
        /// </summary>
        public ActivityData? RootActivity;

        public long UniverseId
        {
            get => _universeId;
            set
            {
                _universeId = value;
                UniverseDetails.LoadFromCache(value);
            }
        }

        public long PlaceId { get; set; } = 0;

        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// This will be empty unless the server joined is a private server
        /// </summary>
        public string AccessCode { get; set; } = string.Empty;
        
        public long UserId { get; set; } = 0;

        public string MachineAddress { get; set; } = string.Empty;

        public bool MachineAddressValid => !string.IsNullOrEmpty(MachineAddress) && !MachineAddress.StartsWith("10.");

        public bool IsTeleport { get; set; } = false;

        public ServerType ServerType { get; set; } = ServerType.Public;

        public DateTime TimeJoined { get; set; }

        public DateTime? TimeLeft { get; set; }

        // everything below here is optional strictly for bloxstraprpc, discord rich presence, or game history

        /// <summary>
        /// This is intended only for other people to use, i.e. context menu invite link, rich presence joining
        /// </summary>
        public string RPCLaunchData { get; set; } = string.Empty;

        public UniverseDetails? UniverseDetails { get; set; }

        public string GameHistoryDescription
        {
            get
            {
                string desc = string.Format(
                    "{0} • {1} {2} {3}", 
                    UniverseDetails?.Data.Creator.Name,
                    TimeJoined.ToString("t"), 
                    Locale.CurrentCulture.Name.StartsWith("ja") ? '~' : '-',
                    TimeLeft?.ToString("t")
                );

                if (ServerType != ServerType.Public)
                    desc += " • " + ServerType.ToTranslatedString();

                return desc;
            }
        }

        public ICommand RejoinServerCommand => new RelayCommand(RejoinServer);

        private SemaphoreSlim serverQuerySemaphore = new(1, 1);

        public string GetInviteDeeplink(bool launchData = true)
        {
            string deeplink = $"roblox://experiences/start?placeId={PlaceId}";

            if (ServerType == ServerType.Private)
                deeplink += "&accessCode=" + AccessCode;
            else
                deeplink += "&gameInstanceId=" + JobId;

            if (launchData && !string.IsNullOrEmpty(RPCLaunchData))
                deeplink += "&launchData=" + HttpUtility.UrlEncode(RPCLaunchData);

            return deeplink;
        }

        public async Task<string?> QueryServerLocation()
        {
            const string LOG_IDENT = "ActivityData::QueryServerLocation";

            // 这里以前是直接 throw。但调用方全是 async void 的事件处理器（进游戏提示、服务器信息窗口），
            // 抛出去会冒到 UI 线程的全局异常处理器，弹个框然后把整个程序退掉。
            // 地址拿不到就是查不了位置，返回 null 让调用方显示「不可用」就行。
            if (!MachineAddressValid)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Skipping location query, machine address is invalid ({MachineAddress})");
                return null;
            }

            await serverQuerySemaphore.WaitAsync();

            try
            {
                if (GlobalCache.ServerLocation.TryGetValue(MachineAddress, out string? cached))
                    return cached;

                string? location = await LookupServerLocationAsync(MachineAddress);

                if (location is null)
                    App.Logger.WriteLine(LOG_IDENT, $"No provider could place {MachineAddress}");
                else
                    App.Logger.WriteLine(LOG_IDENT, $"Server location for {MachineAddress} is {location}");

                GlobalCache.ServerLocation[MachineAddress] = location;

                return location;
            }
            catch (Exception ex)
            {
                // 查不到只记日志，不要弹窗，也不要重试 —— 结果一起缓存，下次进同一个 IP 不再打请求
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get server location for {MachineAddress}");
                App.Logger.WriteException(LOG_IDENT, ex);

                GlobalCache.ServerLocation[MachineAddress] = null;

                return null;
            }
            finally
            {
                serverQuerySemaphore.Release();
            }
        }

        /// <summary>
        /// 机房位置。ipwho.is 和 ipinfo.io 同时查，谁先给出结果用谁的。
        /// 这两家挂了才用 ip-api.com 兜底 —— 它把 Roblox 整段 IP 都算成美国俄勒冈，
        /// 抢在前面返回的话，东京的服务器会显示成波特兰。
        /// 同一个 IP 只查一次，结果（包括失败）都进 GlobalCache
        /// </summary>
        private static async Task<string?> LookupServerLocationAsync(string address)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

            var providers = new Func<CancellationToken, Task<string?>>[]
            {
                token => QueryLocationAsync($"https://ipwho.is/{address}", "city", "region", "country", token),
                token => QueryLocationAsync($"https://ipinfo.io/{address}/json", "city", "region", "country", token)
            };

            string? location = await FirstResultAsync(providers, cts);

            if (!String.IsNullOrEmpty(location))
                return location;

            // 兜底。ip-api 的结果粒度差，但总比「未知」强
            try
            {
                using var fallbackCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return await QueryLocationAsync($"http://ip-api.com/json/{address}?lang=zh-CN", "city", "regionName", "country", fallbackCts.Token);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<string?> FirstResultAsync(IEnumerable<Func<CancellationToken, Task<string?>>> providers, CancellationTokenSource cts)
        {
            var running = providers.Select(provider => provider(cts.Token)).ToList();

            while (running.Count > 0)
            {
                Task<string?> finished = await Task.WhenAny(running);
                running.Remove(finished);

                string? location = null;

                try
                {
                    location = await finished;
                }
                catch (Exception)
                {
                    // 单家失败不影响其他家，继续等下一家
                }

                if (!String.IsNullOrEmpty(location))
                {
                    // 拿到结果就把剩下的请求掐掉，顺手等它们收尾，
                    // 不然 CTS 先 dispose 掉，还在跑的请求会摸到已释放的 token
                    cts.Cancel();

                    try
                    {
                        await Task.WhenAll(running);
                    }
                    catch (Exception)
                    {
                    }

                    return location;
                }
            }

            return null;
        }

        private static async Task<string?> QueryLocationAsync(string url, string cityKey, string regionKey, string countryKey, CancellationToken token)
        {
            using var response = await App.HttpClient.GetAsync(url, token);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(token);

            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            // ipwho.is 查不到时是 HTTP 200 + {"success": false}
            if (root.TryGetProperty("success", out JsonElement success) && success.ValueKind == JsonValueKind.False)
                return null;

            string? city = ReadLocationField(root, cityKey);
            string? region = ReadLocationField(root, regionKey);
            string? country = ReadLocationField(root, countryKey);

            if (String.IsNullOrEmpty(city))
                return null;

            // 城市名跟省/州重名时只留一个，不然会显示成「Tokyo, Tokyo, JP」
            if (city == region || String.IsNullOrEmpty(region))
                return String.IsNullOrEmpty(country) ? city : $"{city}, {country}";

            return String.IsNullOrEmpty(country) ? $"{city}, {region}" : $"{city}, {region}, {country}";
        }

        private static string? ReadLocationField(JsonElement root, string key)
        {
            if (!root.TryGetProperty(key, out JsonElement value) || value.ValueKind != JsonValueKind.String)
                return null;

            string? text = value.GetString();

            return String.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        public override string ToString() => $"{PlaceId}/{JobId}";

        private void RejoinServer()
        {
            string playerPath = new RobloxPlayerData().ExecutablePath;

            Process.Start(playerPath, GetInviteDeeplink(false));
        }
    }
}
