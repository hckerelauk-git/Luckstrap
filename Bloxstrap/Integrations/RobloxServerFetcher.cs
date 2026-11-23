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

        // RoValra 只给英文城市名，这里翻成中文。没列到的城市直接显示英文，不会变成空白
        private static readonly Dictionary<string, string> CityNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Amsterdam"] = "阿姆斯特丹",
            ["Ashburn"] = "阿什本",
            ["Athens"] = "雅典",
            ["Atlanta"] = "亚特兰大",
            ["Boardman"] = "博德曼",
            ["Buenos Aires"] = "布宜诺斯艾利斯",
            ["Cape Town"] = "开普敦",
            ["Chicago"] = "芝加哥",
            ["Columbus"] = "哥伦布",
            ["Dallas"] = "达拉斯",
            ["Doha"] = "多哈",
            ["Dublin"] = "都柏林",
            ["Frankfurt am Main"] = "法兰克福",
            ["Istanbul"] = "伊斯坦布尔",
            ["Jakarta"] = "雅加达",
            ["Kuala Lumpur"] = "吉隆坡",
            ["Lima"] = "利马",
            ["London"] = "伦敦",
            ["Los Angeles"] = "洛杉矶",
            ["Miami"] = "迈阿密",
            ["Milan"] = "米兰",
            ["Mumbai"] = "孟买",
            ["New York City"] = "纽约",
            ["Osaka"] = "大阪",
            ["Paris"] = "巴黎",
            ["San Jose"] = "圣何塞",
            ["Santiago"] = "圣地亚哥",
            ["Santiago de Querétaro"] = "克雷塔罗",
            ["Seattle"] = "西雅图",
            ["Singapore"] = "新加坡",
            ["Sydney"] = "悉尼",
            ["São Paulo"] = "圣保罗",
            ["Tel Aviv"] = "特拉维夫",
            ["Tokyo"] = "东京",
            ["Warsaw"] = "华沙",
            ["Zürich"] = "苏黎世",
        };

        private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Argentina"] = "阿根廷",
            ["Australia"] = "澳大利亚",
            ["Brazil"] = "巴西",
            ["Chile"] = "智利",
            ["France"] = "法国",
            ["Germany"] = "德国",
            ["Greece"] = "希腊",
            ["India"] = "印度",
            ["Indonesia"] = "印尼",
            ["Ireland"] = "爱尔兰",
            ["Israel"] = "以色列",
            ["Italy"] = "意大利",
            ["Japan"] = "日本",
            ["Malaysia"] = "马来西亚",
            ["Mexico"] = "墨西哥",
            ["Netherlands"] = "荷兰",
            ["Peru"] = "秘鲁",
            ["Poland"] = "波兰",
            ["Qatar"] = "卡塔尔",
            ["Singapore"] = "新加坡",
            ["South Africa"] = "南非",
            ["Switzerland"] = "瑞士",
            ["Türkiye"] = "土耳其",
            ["United Kingdom"] = "英国",
            ["United States"] = "美国",
        };

        // 中文表只在界面语言是中文时用。其他语言直接用 RoValra 给的英文名，
        // 总比把中文城市名塞给日语用户强
        private static bool UseChineseNames =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);

        private static string BuildRegionName(string city, string country)
        {
            if (String.IsNullOrWhiteSpace(city) && String.IsNullOrWhiteSpace(country))
                return Strings.Server_RegionUnknown;

            if (!UseChineseNames)
            {
                if (String.IsNullOrWhiteSpace(country))
                    return city;

                if (String.IsNullOrWhiteSpace(city))
                    return country;

                return $"{city} · {country}";
            }

            string cityText = CityNames.TryGetValue(city, out string? cityName) ? cityName : city;
            string countryText = CountryNames.TryGetValue(country, out string? countryName) ? countryName : country;

            if (String.IsNullOrWhiteSpace(countryText))
                return cityText;

            if (String.IsNullOrWhiteSpace(cityText))
                return countryText;

            return $"{cityText} · {countryText}";
        }

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

                    // 接口里的国家名带过 \r\n（Chile 那条就是），不 Trim 的话下拉里会多一行空白，
                    // 而且跟服务器那边的字符串对不上
                    string city = entry.Location?.City?.Trim() ?? String.Empty;
                    string country = entry.Location?.CountryName?.Trim() ?? String.Empty;

                    string region = BuildRegionName(city, country);

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
            var servers = await FetchServerListAsync(placeId, sortOrder);

            if (servers.Count > 0)
                await ResolveRegionsAsync(servers);

            return servers;
        }

        /// <summary>
        /// 只拉服务器列表，不查地区。查地区是逐个发请求，很慢，
        /// 只想「找一台某地区的服」的时候不该把 100 台全查一遍
        /// </summary>
        public async Task<List<RobloxServer>> FetchServerListAsync(long placeId, int sortOrder = 2)
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
            }

            return servers;
        }

        /// <summary>
        /// 找一台指定地区的服务器。边查边判断，命中就停 —— 不像 GetServersAsync 那样
        /// 要把整页查完才返回。region 用的是 GetDatacentersAsync 返回的那套字符串
        /// </summary>
        public async Task<RobloxServer?> FindServerInRegionAsync(
            long placeId,
            string region,
            Action<int, int>? onProgress = null,
            CancellationToken token = default)
        {
            const string LOG_IDENT_FIND = $"{LOG_IDENT}::FindServerInRegion";

            var servers = await FetchServerListAsync(placeId);

            if (servers.Count == 0)
                return null;

            var datacenters = await GetDatacentersAsync();

            if (datacenters is null)
                return null;

            var (_, map) = datacenters.Value;

            string? cookie = GetAuthCookie();

            if (String.IsNullOrEmpty(cookie))
            {
                App.Logger.WriteLine(LOG_IDENT_FIND, "No .ROBLOSECURITY available, cannot resolve regions");
                return null;
            }

            using var semaphore = new SemaphoreSlim(MaxConcurrentLookups);

            var gate = new object();
            RobloxServer? match = null;
            int checkedCount = 0;

            var tasks = servers.Select(async server =>
            {
                if (token.IsCancellationRequested)
                    return;

                // 已经找到一台了，剩下的不用再打请求
                lock (gate)
                {
                    if (match is not null)
                        return;
                }

                await semaphore.WaitAsync(token);

                try
                {
                    lock (gate)
                    {
                        if (match is not null)
                            return;
                    }

                    if (_lookupCache.TryGetValue(server.JobId, out var cached))
                    {
                        server.DataCenterId = cached.DataCenterId;
                        server.Region = cached.Region;
                    }
                    else
                    {
                        int? dcId = await LookupDataCenterIdAsync(server.PlaceId, server.JobId, cookie);

                        server.DataCenterId = dcId;
                        server.Region = dcId is not null && map.TryGetValue(dcId.Value, out string? name)
                            ? name
                            : Strings.Server_RegionUnknown;

                        _lookupCache[server.JobId] = (server.DataCenterId, server.Region);

                        // 一口气打满会被 Roblox 挡
                        await Task.Delay(120, token);
                    }

                    int done;

                    lock (gate)
                    {
                        done = ++checkedCount;

                        if (server.Region == region && match is null)
                        {
                            match = server;
                            App.Logger.WriteLine(LOG_IDENT_FIND, $"Found {server.JobId} in {region}");
                        }
                    }

                    onProgress?.Invoke(done, servers.Count);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT_FIND, $"Lookup failed for {server.JobId}");
                    App.Logger.WriteException(LOG_IDENT_FIND, ex);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return match;
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
                    server.Region = Strings.Server_RegionUnknown;

                return;
            }

            var (_, map) = datacenters.Value;

            string? cookie = GetAuthCookie();

            if (String.IsNullOrEmpty(cookie))
            {
                // 没登录态就换不出数据中心，直接标未知，别让用户干等
                App.Logger.WriteLine(LOG_IDENT_REGIONS, "No .ROBLOSECURITY available, regions will be unknown");
                foreach (var server in servers)
                    server.Region = Strings.Server_RegionUnknown;

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
                        : Strings.Server_RegionUnknown;

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
                    server.Region = Strings.Server_RegionUnknown;
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
