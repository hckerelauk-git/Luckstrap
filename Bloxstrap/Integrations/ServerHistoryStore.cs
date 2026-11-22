using Bloxstrap.Models.Entities;

namespace Bloxstrap.Integrations
{
    /// <summary>
    /// 服务器历史落盘。
    ///
    /// 原来 ActivityWatcher.History 只在内存里，托盘退出或者重启就全没了，
    /// 每次想回上一个服务器都得重新找。
    ///
    /// 只存重连要用的字段。UniverseDetails 不存 —— 那个有自己的磁盘缓存，
    /// 显示的时候 ServerHistoryViewModel 会调 FetchBulk 补齐。
    /// </summary>
    internal static class ServerHistoryStore
    {
        private const string LOG_IDENT = "ServerHistoryStore";

        // 留太多会让历史窗口一次性拉几十个 universe 的详情，卡
        private const int MaxEntries = 50;

        public static string FileLocation => Path.Combine(Paths.Base, "ServerHistory.json");

        private class Entry
        {
            public long UniverseId { get; set; }
            public long PlaceId { get; set; }
            public string JobId { get; set; } = String.Empty;
            public string AccessCode { get; set; } = String.Empty;
            public long UserId { get; set; }
            public string MachineAddress { get; set; } = String.Empty;
            public bool IsTeleport { get; set; }
            public ServerType ServerType { get; set; }
            public DateTime TimeJoined { get; set; }
            public DateTime? TimeLeft { get; set; }
            public string RPCLaunchData { get; set; } = String.Empty;
        }

        public static List<ActivityData> Load()
        {
            var history = new List<ActivityData>();

            try
            {
                if (!File.Exists(FileLocation))
                    return history;

                var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(FileLocation));

                if (entries is null)
                    return history;

                foreach (var entry in entries)
                {
                    // UniverseId 的 setter 会顺手查一次 UniverseDetails 缓存，
                    // 缓存命中的话游戏名直接就出来了，不用等网络
                    history.Add(new ActivityData
                    {
                        UniverseId = entry.UniverseId,
                        PlaceId = entry.PlaceId,
                        JobId = entry.JobId,
                        AccessCode = entry.AccessCode,
                        UserId = entry.UserId,
                        MachineAddress = entry.MachineAddress,
                        IsTeleport = entry.IsTeleport,
                        ServerType = entry.ServerType,
                        TimeJoined = entry.TimeJoined,
                        TimeLeft = entry.TimeLeft,
                        RPCLaunchData = entry.RPCLaunchData,
                    });
                }

                App.Logger.WriteLine(LOG_IDENT, $"Loaded {history.Count} entries from {FileLocation}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load server history");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return history;
        }

        public static void Save(IEnumerable<ActivityData> history)
        {
            try
            {
                var entries = history
                    .Where(x => x.PlaceId != 0)
                    .Take(MaxEntries)
                    .Select(x => new Entry
                    {
                        UniverseId = x.UniverseId,
                        PlaceId = x.PlaceId,
                        JobId = x.JobId,
                        AccessCode = x.AccessCode,
                        UserId = x.UserId,
                        MachineAddress = x.MachineAddress,
                        IsTeleport = x.IsTeleport,
                        ServerType = x.ServerType,
                        TimeJoined = x.TimeJoined,
                        TimeLeft = x.TimeLeft,
                        RPCLaunchData = x.RPCLaunchData,
                    })
                    .ToList();

                Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);

                File.WriteAllText(FileLocation, JsonSerializer.Serialize(entries, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

                App.Logger.WriteLine(LOG_IDENT, $"Saved {entries.Count} entries");
            }
            catch (Exception ex)
            {
                // 存不下来不该影响玩游戏，记一条日志就够
                App.Logger.WriteLine(LOG_IDENT, "Failed to save server history");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }
}
