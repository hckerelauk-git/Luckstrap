namespace Bloxstrap.Models
{
    /// <summary>
    /// 一套 FastFlags 配置。存成单个 json 文件，切换时整体覆盖当前的 flag 表。
    /// 跟 ClientAppSettings.json 的区别是这个只是存档，不参与启动。
    /// </summary>
    public class FastFlagProfile
    {
        public string Name { get; set; } = "";
        public string Note { get; set; } = "";
        public DateTime SavedAt { get; set; } = DateTime.Now;

        // 跟 FastFlagManager 一样，值一律存字符串
        public Dictionary<string, string> Flags { get; set; } = new();
    }
}
