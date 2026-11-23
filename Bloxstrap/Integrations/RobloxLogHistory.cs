using System.Globalization;
using System.Text.RegularExpressions;
using Bloxstrap.Models.Entities;

namespace Bloxstrap.Integrations
{
    /// <summary>
    /// 从 Roblox 自己的日志里翻最近进过的服务器。
    ///
    /// ActivityWatcher 只能记它开着的那些局 —— 装 Luckstrap 之前的、或者 Luckstrap 没开时
    /// 从网页直接点 Play 的，全都漏掉。Roblox 每次启动都会在 %LOCALAPPDATA%\Roblox\logs
    /// 留一份日志，里面本来就有 jobId、placeId、机房 IP 和断连时间，直接读那个就行。
    /// </summary>
    internal static class RobloxLogHistory
    {
        private const string LOG_IDENT = "RobloxLogHistory";

        private const string JoinPattern = @"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)";
        private const string UniversePattern = @"placeid:([0-9]+),.*universeid:([0-9]+)";
        private const string UdmuxPattern = @"UDMUX Address = ([0-9\.]+), Port";
        private const string DisconnectEntry = "Time to disconnect replication data:";
        private const string TimestampPattern = @"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})";

        // 日志一份能到几十兆，太老的文件没必要读
        private const int MaxFiles = 40;

        public static string LogDirectory => Path.Combine(Paths.LocalAppData, "Roblox", "logs");

        /// <summary>
        /// 扫一遍日志，返回 since 之后进过的服务器。按时间正序解析，方便把进房和断连配成一对
        /// </summary>
        public static List<ActivityData> Import(DateTime since)
        {
            var result = new List<ActivityData>();

            try
            {
                var directory = new DirectoryInfo(LogDirectory);

                if (!directory.Exists)
                    return result;

                var files = directory.GetFiles("*.log")
                    .Where(x => x.LastWriteTime >= since)
                    .OrderByDescending(x => x.LastWriteTime)
                    .Take(MaxFiles)
                    .OrderBy(x => x.LastWriteTime)
                    .ToList();

                foreach (var file in files)
                    ParseFile(file, result);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to read Roblox logs");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return result;
        }

        private static void ParseFile(FileInfo file, List<ActivityData> result)
        {
            ActivityData? pending = null;
            DateTime lastTimestamp = file.LastWriteTime;

            try
            {
                foreach (string line in File.ReadLines(file.FullName))
                {
                    if (line.Length < 24)
                        continue;

                    DateTime timestamp = ParseTimestamp(line, lastTimestamp);
                    lastTimestamp = timestamp;

                    if (line.Contains("! Joining game"))
                    {
                        Match match = Regex.Match(line, JoinPattern);

                        if (!match.Success)
                            continue;

                        pending = new ActivityData
                        {
                            JobId = match.Groups[1].Value,
                            PlaceId = Int64.Parse(match.Groups[2].Value),
                            MachineAddress = match.Groups[3].Value,
                            TimeJoined = timestamp
                        };

                        continue;
                    }

                    if (pending is null)
                        continue;

                    if (line.Contains("game_join_loadtime"))
                    {
                        Match match = Regex.Match(line, UniversePattern);

                        if (match.Success)
                            pending.UniverseId = Int64.Parse(match.Groups[2].Value);
                    }
                    else if (line.Contains("UDMUX Address = "))
                    {
                        Match match = Regex.Match(line, UdmuxPattern);

                        // 进房那行给的是 10.x 内网地址，机房位置要用 UDMUX 这个公网地址
                        if (match.Success)
                            pending.MachineAddress = match.Groups[1].Value;
                    }
                    else if (line.Contains(DisconnectEntry))
                    {
                        pending.TimeLeft = timestamp;
                        result.Add(pending);
                        pending = null;
                    }
                }

                // 日志结束还没断连的，说明是直接关窗口退的，拿最后一行的时间凑一个
                if (pending is not null)
                {
                    pending.TimeLeft = lastTimestamp;
                    result.Add(pending);
                }
            }
            catch (Exception ex)
            {
                // 日志正被 Roblox 写着的时候读会抛，跳过这份就行
                App.Logger.WriteLine(LOG_IDENT, $"Skipped {file.Name}: {ex.Message}");
            }
        }

        private static DateTime ParseTimestamp(string line, DateTime fallback)
        {
            Match match = Regex.Match(line, TimestampPattern);

            if (!match.Success)
                return fallback;

            if (DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime utc))
                return utc.ToLocalTime();

            return fallback;
        }
    }
}
