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

            // 只有 placeId 的老记录先换成 universeId，不然下面拿不到名字
            await RobloxPlaceResolver.ResolveAsync(latest);

            var missing = latest
                .Where(x => x.UniverseId != 0)
                .Select(x => x.UniverseId)
                .Distinct()
                .Where(x => UniverseDetails.LoadFromCache(x) is null)
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

            return latest.Select(x =>
            {
                // FetchBulk 只写静态缓存，ActivityData 上的 UniverseDetails 还是空的，
                // 得回缓存里取一次 —— 之前直接读 x.UniverseDetails，列表里全是 PlaceId
                UniverseDetails? details = x.UniverseDetails ?? UniverseDetails.LoadFromCache(x.UniverseId);

                return new RecentGame
                {
                    PlaceId = x.PlaceId,
                    UniverseId = x.UniverseId,
                    JobId = x.JobId,
                    AccessCode = x.AccessCode,
                    Name = details?.Data?.Name ?? $"PlaceId {x.PlaceId}",
                    LastPlayed = x.TimeJoined,
                    IsPrivate = x.ServerType == Enums.ServerType.Private || !String.IsNullOrEmpty(x.AccessCode)
                };
            }).ToList();
        }

        /// <summary>
        /// 收藏 + 最近玩过合成一份，收藏排前面，重复的游戏只留一条
        /// </summary>
        public static async Task<List<GamePickItem>> LoadPicksAsync()
        {
            var items = new List<GamePickItem>();

            foreach (var favorite in FavoriteStore.Load())
            {
                if (favorite.PlaceId == 0)
                    continue;

                items.Add(new GamePickItem
                {
                    PlaceId = favorite.PlaceId,
                    Name = favorite.Name,
                    DisplayText = $"收藏 · {favorite.Name}"
                });
            }

            foreach (var recent in await LoadAsync())
            {
                if (items.Any(x => x.PlaceId == recent.PlaceId))
                    continue;

                items.Add(new GamePickItem
                {
                    PlaceId = recent.PlaceId,
                    Name = recent.Name,
                    DisplayText = $"最近 · {recent.DisplayText}",
                    JobId = recent.JobId,
                    AccessCode = recent.AccessCode
                });
            }

            return items;
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

    /// <summary>收藏和最近玩过合起来的候选列表，给区服选择页挑游戏用</summary>
    public class GamePickItem
    {
        public long PlaceId { get; set; }
        public string Name { get; set; } = String.Empty;
        public string DisplayText { get; set; } = String.Empty;

        /// <summary>最近玩过的那条才有的服务器信息，收藏项是空的</summary>
        public string JobId { get; set; } = String.Empty;
        public string AccessCode { get; set; } = String.Empty;
    }
}
