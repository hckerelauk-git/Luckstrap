namespace Bloxstrap.Models.APIs.RoValra
{
    /// <summary>
    /// RoValra 的数据中心列表条目。用它把 Roblox 的 DataCenterId 翻译成城市名。
    /// 字段名按实际返回的来（dataCenterIds 是驼峰，不是 data_center_ids）
    /// </summary>
    public class DatacenterEntry
    {
        [JsonPropertyName("dataCenterIds")]
        public List<int> DataCenterIds { get; set; } = new();

        [JsonPropertyName("location")]
        public DatacenterLocation? Location { get; set; }

        [JsonPropertyName("inactive")]
        public bool Inactive { get; set; }
    }

    public class DatacenterLocation
    {
        [JsonPropertyName("city")]
        public string City { get; set; } = String.Empty;

        [JsonPropertyName("region")]
        public string Region { get; set; } = String.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = String.Empty;

        [JsonPropertyName("country_name")]
        public string CountryName { get; set; } = String.Empty;
    }
}
