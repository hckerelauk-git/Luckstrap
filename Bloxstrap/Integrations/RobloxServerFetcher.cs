using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Bloxstrap.Models.APIs.RoValra;
using Bloxstrap.Models.Entities;

namespace Bloxstrap.Integrations
{
    /// <summary>
    /// 拉某个游戏的服务器列表，并查出每台服务器在哪个地区。
    ///
    /// Roblox 官方的服务器列表接口（games.roblox.com/.../servers/Public）只给 JobId 和人数，
    /// 不告诉你机房在哪。数据中心 ID 藏在加入请求的返回脚本里，所以要逐个发 join 请求去换。
    /// 换到的结果按 JobId 缓存在内存里，同一个服务器不用查第二次。
    ///
    /// 地区对照表用的是 RoValra 的公开接口，跟 Froststrap 用的是同一份数据。
    /// </summary>
    internal class RobloxServerFetcher
    {
        private const string LOG_IDENT = "RobloxServerFetcher";

        private const string DatacenterUrl = "https://apis.rovalra.com/v1/datacenters/list";

        // 官方接口只认这几个值，写别的会直接 400
        private const int PageSize = 100;

        // 同时最多查这么多台，太多会被 Roblox 限流
        private const int MaxConcurrentLookups = 6;

        private static Dictionary<int, string>? _datacenterMap;
        private static List<string>? _regionList;

        private readonly ConcurrentDictionary<string, (int? DataCenterId, string Region)> _lookupCache = new();

        /// <summary>
        /// 拉数据中心对照表。返回排序后的地区名列表和 id → 地区 的映射
        /// </summary>
        public async Task<(List<string> Regions, Dictionary<int, string> Map)?> GetDatacentersAsync()
        {
            if (_datacenterMap is not null && _regionList is not null)
                return (_regionList, _datacenterMap);

            try
            {
                string json = await App.HttpClient.GetStringAsync(DatacenterUrl);

                var entries = JsonSerializer.Deserialize<List<DatacenterEntry>>(json);

                if (entries is null || entries.Count == 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Datacenter list came back empty");
                    return null;
                }

                var map = new Dictionary<int, string>();
                var regions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in entries)
                {
                    if (entry.Inactive)
                        continue;

                    string city = entry.Location?.City ?? String.Empty;
                    string country = entry.Location?.CountryName ?? String.Empty;

                    string region = String.IsNullOrWhiteSpace(city) && String.IsNullOrWhiteSpace(country)
                        ? "未知"
                        : $"{city}, {country}".Trim().Trim(',', ' ');

                    regions.Add(region);

                    foreach (int id in entry.DataCenterIds)
                        map[id] = region;
                }

                _datacenterMap = map;
                _regionList = regions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

                App.Logger.WriteLine(LOG_IDENT, $"Loaded {map.Count} datacenter ids across {_regionList.Count} regions");

                return (_regionList, _datacenterMap);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load datacenter list");
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        /// <summary>
        /// 拉一页服务器，然后并发补地区。cookie 拿不到的话地区会全部显示未知，列表本身还能用
        /// </summary>
        public async Task<List<RobloxServer>> GetServersAsync(long placeId, int sortOrder = 2)
        {
            const string LOG_IDENT_SERVERS = $"{LOG_IDENT}::GetServers";

            var servers = new List<RobloxServer>();

            try
            {
                string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public" +
                             $"?sortOrder={sortOrder}&excludeFullGames=true&limit={PageSize}";

                string json = await App.HttpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                {
                    App.Logger.WriteLine(LOG_IDENT_SERVERS, "Server list had no data array");
                    return servers;
                }

                foreach (var item in data.EnumerateArray())
                {
                    servers.Add(new RobloxServer
                    {
                        PlaceId = placeId,
                        JobId = item.TryGetProperty("id", out var id) ? id.GetString() ?? String.Empty : String.Empty,
                        Playing = item.TryGetProperty("playing", out var playing) ? playing.GetInt32() : 0,
                        MaxPlayers = item.TryGetProperty("maxPlayers", out var max) ? max.GetInt32() : 0,
                        Ping = item.TryGetProperty("ping", out var ping) ? (int)ping.GetDouble() : 0
                    });
                }

                App.Logger.WriteLine(LOG_IDENT_SERVERS, $"Got {servers.Count} servers for place {placeId}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT_SERVERS, "Failed to fetch server list");
                App.Logger.WriteException(LOG_IDENT_SERVERS, ex);
                return servers;
            }

            await ResolveRegionsAsync(servers);

            return servers;
        }

        /// <summary>
        /// 并发查地区。查不到的留「未知」，不阻塞其它条目
        /// </summary>
        private async Task ResolveRegionsAsync(List<RobloxServer> servers)
        {
            const string LOG_IDENT_REGIONS = $"{LOG_IDENT}::ResolveRegions";

            var datacenters = await GetDatacentersAsync();

            if (datacenters is null)
            {
                foreach (var server in servers)
                    server.Region = "未知";

                return;
            }

            var (_, map) = datacenters.Value;

            string? cookie = GetAuthCookie();

            if (String.IsNullOrEmpty(cookie))
            {
                // 没登录态就换不出数据中心，直接标未知，别让用户干等
                App.Logger.WriteLine(LOG_IDENT_REGIONS, "No .ROBLOSECURITY available, regions will be unknown");
                foreach (var server in servers)
                    server.Region = "未知";

                return;
            }

            using var semaphore = new SemaphoreSlim(MaxConcurrentLookups);

            var tasks = servers.Select(async server =>
            {
                // 缓存里有就直接用，省一次请求
                if (_lookupCache.TryGetValue(server.JobId, out var cached))
                {
                    server.DataCenterId = cached.DataCenterId;
                    server.Region = cached.Region;
                    return;
                }

                await semaphore.WaitAsync();

                try
                {
                    int? dcId = await LookupDataCenterIdAsync(server.PlaceId, server.JobId, cookie);

                    string region = dcId is not null && map.TryGetValue(dcId.Value, out string? name)
                        ? name
                        : "未知";

                    server.DataCenterId = dcId;
                    server.Region = region;

                    _lookupCache[server.JobId] = (dcId, region);

                    // 每个请求之间歇一下，一口气打满会被 Roblox 挡
                    await Task.Delay(120);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_REGIONS, $"Lookup failed for {server.JobId}");
                    App.Logger.WriteException(LOG_IDENT_REGIONS, ex);
                    server.Region = "未知";
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 发一次加入请求，从返回的脚本里把 DataCenterId 抠出来。只读不写，不会真的把游戏拉起来
        /// </summary>
        private static async Task<int?> LookupDataCenterIdAsync(long placeId, string jobId, string cookie)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://gamejoin.roblox.com/v1/join-game-instance");

            request.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
            request.Headers.TryAddWithoutValidation("User-Agent", "Roblox/Luckstrap");
            request.Headers.TryAddWithoutValidation("Referer", $"https://www.roblox.com/games/{placeId}");
            request.Headers.TryAddWithoutValidation("Origin", "https://www.roblox.com");

            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    placeId,
                    isTeleport = false,
                    gameId = jobId,
                    gameJoinAttemptId = jobId
                }),
                Encoding.UTF8,
                "application/json");

            using var response = await App.HttpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Join request for {jobId} returned {(int)response.StatusCode}");
                return null;
            }

            string body = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);

            return FindDataCenterId(doc.RootElement, 0);
        }

        /// <summary>
        /// 递归找 DataCenterId。join 脚本里嵌了好几层，位置不固定，
        /// 直接按字段名找比按路径取稳
        /// </summary>
        private static int? FindDataCenterId(JsonElement element, int depth)
        {
            // 脚本嵌套不会太深，超过就当没找到，防止恶意/异常响应把栈打爆
            if (depth > 8)
                return null;

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("DataCenterId") && property.Value.ValueKind == JsonValueKind.Number
                        && property.Value.TryGetInt32(out int id))
                        return id;

                    int? nested = FindDataCenterId(property.Value, depth + 1);

                    if (nested is not null)
                        return nested;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    int? nested = FindDataCenterId(item, depth + 1);

                    if (nested is not null)
                        return nested;
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                // joinScript 本身是个 JSON 字符串，要再解一层
                string? text = element.GetString();

                if (!String.IsNullOrWhiteSpace(text) && (text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('[')))
                {
                    try
                    {
                        using var inner = JsonDocument.Parse(text);
                        return FindDataCenterId(inner.RootElement, depth + 1);
                    }
                    catch (JsonException)
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 从 Roblox 本地登录态里取 .ROBLOSECURITY。取不到返回 null
        /// </summary>
        private static string? GetAuthCookie()
        {
            try
            {
                string? blob = RobloxCookies.ReadBlob();

                if (String.IsNullOrEmpty(blob))
                    return null;

                return RobloxCookies.ReadAuthCookie(blob);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to read auth cookie");
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }
    }
}
