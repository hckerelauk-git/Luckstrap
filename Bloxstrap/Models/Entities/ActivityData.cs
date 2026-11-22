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

            if (GlobalCache.ServerLocation.TryGetValue(MachineAddress, out string? location))
            {
                serverQuerySemaphore.Release();
                return location;
            }

            try
            {
                var ipInfo = await Http.GetJson<IPInfoResponse>($"https://ipinfo.io/{MachineAddress}/json");

                if (string.IsNullOrEmpty(ipInfo.City))
                    throw new InvalidHTTPResponseException("Reported city was blank");

                if (ipInfo.City == ipInfo.Region)
                    location = $"{ipInfo.Region}, {ipInfo.Country}";
                else
                    location = $"{ipInfo.City}, {ipInfo.Region}, {ipInfo.Country}";

                GlobalCache.ServerLocation[MachineAddress] = location;
            }
            catch (Exception ex)
            {
                // ipinfo.io 在部分地区连不上，每次进游戏都弹警告框太吵，只记日志。
                // 顺便把失败结果也缓存起来，别每次进游戏都重试一遍
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get server location for {MachineAddress}");
                App.Logger.WriteException(LOG_IDENT, ex);

                GlobalCache.ServerLocation[MachineAddress] = null;
            }
            finally
            {
                serverQuerySemaphore.Release();
            }

            return location;
        }

        public override string ToString() => $"{PlaceId}/{JobId}";

        private void RejoinServer()
        {
            string playerPath = new RobloxPlayerData().ExecutablePath;

            Process.Start(playerPath, GetInviteDeeplink(false));
        }
    }
}
