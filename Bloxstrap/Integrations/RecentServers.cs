namespace Bloxstrap.Integrations
{
    /// <summary>
    /// 给「快速启动 / 收藏 / 快捷方式」三个页面共用的最近玩过列表。
    /// 数据来自 ServerHistoryStore（包含从 Roblox 日志补进来的记录），同一个游戏只留最近一次
    /// </summary>
    internal static class RecentServers
    {
        public static async Task<List<RecentGame>> LoadAsync(int max = 12)
        {
            // 顺带补一遍日志，用户可能从没打开过「最近玩过」页
            await Task.Run(() => ServerHistoryStore.ImportFromRobloxLogs(DateTime.Now.AddDays(-30)));

            var history = ServerHistoryStore.Load();

            var latest = history
                .Where(x => x.PlaceId != 0)
                .GroupBy(x => x.PlaceId)
                .Select(x => x.OrderByDescending(y => y.TimeJoined).First())
                .OrderByDescending(x => x.TimeJoined)
                .Take(max)
                .ToList();

            var missing = latest
                .Where(x => x.UniverseId != 0 && x.UniverseDetails is null)
                .Select(x => x.UniverseId)
                .Distinct()
                .ToList();

            if (missing.Count > 0)
            {
                try
                {
                    await UniverseDetails.FetchBulk(String.Join(',', missing));
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("RecentServers::Load", "Failed to fetch universe details");
                    App.Logger.WriteException("RecentServers::Load", ex);
                }
            }

            return latest.Select(x => new RecentGame
            {
                PlaceId = x.PlaceId,
                UniverseId = x.UniverseId,
                JobId = x.JobId,
                AccessCode = x.AccessCode,
                Name = x.UniverseDetails?.Data?.Name ?? $"PlaceId {x.PlaceId}",
                LastPlayed = x.TimeJoined,
                IsPrivate = x.ServerType == Enums.ServerType.Private || !String.IsNullOrEmpty(x.AccessCode)
            }).ToList();
        }
    }

    public class RecentGame
    {
        public long PlaceId { get; set; }
        public long UniverseId { get; set; }
        public string JobId { get; set; } = String.Empty;
        public string AccessCode { get; set; } = String.Empty;
        public string Name { get; set; } = String.Empty;
        public DateTime LastPlayed { get; set; }
        public bool IsPrivate { get; set; }

        public string DisplayText => IsPrivate
            ? $"{Name}（私服 · {LastPlayed:MM-dd HH:mm}）"
            : $"{Name}（{LastPlayed:MM-dd HH:mm}）";
    }
}
