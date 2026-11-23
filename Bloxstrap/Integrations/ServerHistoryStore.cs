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

        // 日志只需要补一次，进程内重复扫没有意义
        private static bool _logImported;

        public static string FileLocation => Path.Combine(Paths.Base, "ServerHistory.json");

        // 记录清空时间，日志补录的时候拿它当分界线
        private static string ClearMarkerLocation => Path.Combine(Paths.Base, "ServerHistory.cleared");

        private static DateTime ReadClearMarker()
        {
            try
            {
                if (File.Exists(ClearMarkerLocation)
                    && DateTime.TryParse(File.ReadAllText(ClearMarkerLocation), null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime clearedAt))
                    return clearedAt;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to read clear marker");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return DateTime.MinValue;
        }

        /// <summary>
        /// 清空历史。除了删文件，还要留个时间戳 —— 不然下次启动从 Roblox 日志补录，
        /// 刚清掉的记录又全回来了
        /// </summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(FileLocation))
                    File.Delete(FileLocation);

                Directory.CreateDirectory(Paths.Base);
                File.WriteAllText(ClearMarkerLocation, DateTime.Now.ToString("O"));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to clear server history");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

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

            return MergeTeleports(history);
        }

        /// <summary>
        /// 老版本把游戏里传送也算成一局，同一个 universe 连着记好几条。
        /// 读的时候顺手合并，保证界面上一个游戏的一局只出现一次
        /// </summary>
        private static List<ActivityData> MergeTeleports(List<ActivityData> history)
        {
            if (history.Count < 2)
                return history;

            var ordered = history.OrderBy(x => x.TimeJoined).ToList();
            var merged = new List<ActivityData>();

            foreach (var entry in ordered)
            {
                ActivityData? last = merged.Count > 0 ? merged[^1] : null;

                bool sameSession = last is not null
                    && last.UniverseId != 0
                    && last.UniverseId == entry.UniverseId
                    && entry.TimeJoined - (last.TimeLeft ?? last.TimeJoined) < TimeSpan.FromMinutes(2);

                if (sameSession)
                {
                    last!.TimeLeft = entry.TimeLeft ?? last.TimeLeft;
                    continue;
                }

                merged.Add(entry);
            }

            merged.Reverse();
            return merged;
        }

        /// <summary>
        /// 把 Roblox 日志里的记录并进来。跟已有的按 jobId + 进入时间去重，
        /// 已经记过的不会重复；返回真正新增的条数。
        /// 每次打开页面都扫一遍日志太浪费，一个进程里只扫第一次
        /// </summary>
        public static int ImportFromRobloxLogs(DateTime since)
        {
            if (_logImported)
                return 0;

            _logImported = true;

            var imported = RobloxLogHistory.Import(since);

            // 用户点过「清空记录」的话，清空之前的那几局不要再从日志里翻出来
            DateTime clearedAt = ReadClearMarker();

            if (clearedAt != DateTime.MinValue)
                imported = imported.Where(x => x.TimeJoined > clearedAt).ToList();

            if (imported.Count == 0)
                return 0;

            var existing = Load();
            var seen = new HashSet<string>(existing.Select(x => $"{x.JobId}|{x.TimeJoined.Ticks}"));
            int added = 0;

            foreach (var entry in imported)
            {
                if (!seen.Add($"{entry.JobId}|{entry.TimeJoined.Ticks}"))
                    continue;

                existing.Add(entry);
                added++;
            }

            if (added > 0)
            {
                existing.Sort((a, b) => b.TimeJoined.CompareTo(a.TimeJoined));
                Save(existing);
                App.Logger.WriteLine(LOG_IDENT, $"Imported {added} entries from Roblox logs");
            }

            return added;
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
