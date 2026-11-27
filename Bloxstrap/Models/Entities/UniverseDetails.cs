namespace Bloxstrap.Models.Entities
{
    /// <summary>
    /// Explicit loading. Load from cache before and after a fetch.
    /// </summary>
    public class UniverseDetails
    {
        private static List<UniverseDetails> _cache { get; set; } = new();

        public GameDetailResponse Data { get; set; } = null!;

        /// <summary>
        /// Returns data for a 420x420 icon
        /// </summary>
        public ThumbnailResponse Thumbnail { get; set; } = null!;

        public static UniverseDetails? LoadFromCache(long id)
        {
            var cacheQuery = _cache.Where(x => x.Data?.Id == id);

            if (cacheQuery.Any())
                return cacheQuery.First();

            return null;
        }

        public static Task FetchSingle(long id) => FetchBulk(id.ToString());

        public static async Task FetchBulk(string ids)
        {
            var gameDetailResponse = await Http.GetJson<ApiArrayResponse<GameDetailResponse>>($"https://games.roblox.com/v1/games?universeIds={ids}");

            if (!gameDetailResponse.Data.Any())
                throw new InvalidHTTPResponseException("Roblox API for Game Details returned invalid data");

            // 420 而不是 128。封面在卡片里要铺 200 多像素宽，高 DPI 屏还要再乘 1.5，
            // 128 的图拉上去就是糊的
            var universeThumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>($"https://thumbnails.roblox.com/v1/games/icons?universeIds={ids}&returnPolicy=PlaceHolder&size=420x420&format=Png&isCircular=false");

            if (!universeThumbnailResponse.Data.Any())
                throw new InvalidHTTPResponseException("Roblox API for Game Thumbnails returned invalid data");

            foreach (string strId in ids.Split(','))
            {
                long id = long.Parse(strId);

                // 历史里可能有已经被删掉或者封掉的游戏，First() 会直接抛，
                // 一条坏的就把整个历史窗口带崩，所以这里跳过
                var data = gameDetailResponse.Data.FirstOrDefault(x => x.Id == id);
                var thumbnail = universeThumbnailResponse.Data.FirstOrDefault(x => x.TargetId == id);

                if (data is null || thumbnail is null)
                {
                    App.Logger.WriteLine("UniverseDetails::FetchBulk", $"Universe {id} is missing from the API response, skipping");
                    continue;
                }

                // 同一个 universe 可能被不同页面各拉一次，缓存里只留一份
                if (_cache.Any(x => x.Data?.Id == id))
                    continue;

                _cache.Add(new UniverseDetails
                {
                    Data = data,
                    Thumbnail = thumbnail,
                });
            }
        }
    }
}
