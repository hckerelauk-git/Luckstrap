namespace Bloxstrap.Integrations
{
    /// <summary>
    /// 收藏游戏的落盘和元数据抓取。
    ///
    /// 存在 %LOCALAPPDATA%\Luckstrap\Favorites.json，跟设置文件放一起。
    /// 添加的时候顺手把游戏名、作者、图标抓下来一起存，
    /// 这样即使 Roblox 接口抽风，收藏列表也能正常显示和启动。
    /// </summary>
    internal static class FavoriteStore
    {
        private const string LOG_IDENT = "FavoriteStore";

        public static string FileLocation => Path.Combine(Paths.Base, "Favorites.json");

        private static List<FavoriteGame>? _cache;

        public static List<FavoriteGame> Load()
        {
            if (_cache is not null)
                return _cache;

            var favorites = new List<FavoriteGame>();

            try
            {
                if (File.Exists(FileLocation))
                {
                    var entries = JsonSerializer.Deserialize<List<FavoriteGame>>(File.ReadAllText(FileLocation));

                    if (entries is not null)
                        favorites = entries;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Loaded {favorites.Count} favorites");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load favorites");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            _cache = favorites;
            return _cache;
        }

        public static void Save()
        {
            if (_cache is null)
                return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);

                File.WriteAllText(FileLocation, JsonSerializer.Serialize(_cache, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

                App.Logger.WriteLine(LOG_IDENT, $"Saved {_cache.Count} favorites");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to save favorites");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public static bool Contains(long placeId) => Load().Any(x => x.PlaceId == placeId);

        public static void Add(FavoriteGame game)
        {
            var favorites = Load();

            if (favorites.Any(x => x.PlaceId == game.PlaceId))
                return;

            favorites.Insert(0, game);
            Save();
        }

        public static void Remove(long placeId)
        {
            var favorites = Load();
            favorites.RemoveAll(x => x.PlaceId == placeId);
            Save();
        }

        /// <summary>
        /// placeId 转 universeId。收藏夹要拿游戏名和图标，两个接口都要 universeId
        /// </summary>
        public static async Task<long> ResolveUniverseId(long placeId)
        {
            var response = await Http.GetJson<UniverseIdResponse>(
                $"https://apis.roblox.com/universes/v1/places/{placeId}/universe");

            return response.UniverseId;
        }

        /// <summary>
        /// 抓游戏名 / 作者 / 图标。抓不到就返回只带 placeId 的条目，不影响收藏
        /// </summary>
        public static async Task<FavoriteGame> CreateAsync(long placeId)
        {
            var game = new FavoriteGame { PlaceId = placeId };

            try
            {
                long universeId = await ResolveUniverseId(placeId);

                if (universeId == 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Place {placeId} has no universe id");
                    return game;
                }

                game.UniverseId = universeId;

                await UniverseDetails.FetchSingle(universeId);

                var details = UniverseDetails.LoadFromCache(universeId);

                if (details is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"No details returned for universe {universeId}");
                    return game;
                }

                game.Name = details.Data.Name;
                game.CreatorName = details.Data.Creator.Name;
                game.ThumbnailUrl = details.Thumbnail.ImageUrl;
            }
            catch (Exception ex)
            {
                // 网络挂了不该让添加收藏失败，先把 placeId 存下来，名字之后再补
                App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch details for place {placeId}");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return game;
        }
    }
}
