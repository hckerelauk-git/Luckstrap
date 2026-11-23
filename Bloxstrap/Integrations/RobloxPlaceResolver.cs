using System.Text.Json;
using Bloxstrap.Models.Entities;

namespace Bloxstrap.Integrations
{
    /// <summary>
    /// placeId 换 universeId。历史记录里只有 placeId 的时候，光靠它拿不到游戏名和封面，
    /// 得先问一下 Roblox 这个 place 属于哪个 universe。
    /// </summary>
    internal static class RobloxPlaceResolver
    {
        private const string LOG_IDENT = "RobloxPlaceResolver";

        private static readonly Dictionary<long, long> _cache = new();

        /// <summary>
        /// 把一批记录里缺 universeId 的补上。查不到的留着 0，调用方用 PlaceId 兜底显示
        /// </summary>
        public static async Task ResolveAsync(IEnumerable<ActivityData> entries)
        {
            var missing = entries
                .Where(x => x.UniverseId == 0 && x.PlaceId != 0)
                .Select(x => x.PlaceId)
                .Distinct()
                .ToList();

            foreach (long placeId in missing)
            {
                long universeId = await GetUniverseIdAsync(placeId);

                if (universeId == 0)
                    continue;

                foreach (var entry in entries.Where(x => x.PlaceId == placeId && x.UniverseId == 0))
                    entry.UniverseId = universeId;
            }
        }

        private static async Task<long> GetUniverseIdAsync(long placeId)
        {
            if (_cache.TryGetValue(placeId, out long cached))
                return cached;

            try
            {
                string json = await App.HttpClient.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{placeId}/universe");

                using var document = JsonDocument.Parse(json);

                if (document.RootElement.TryGetProperty("universeId", out JsonElement value) && value.TryGetInt64(out long universeId))
                {
                    _cache[placeId] = universeId;
                    return universeId;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Could not resolve universe for place {placeId}");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            _cache[placeId] = 0;
            return 0;
        }
    }
}
