using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bloxstrap.Models.Entities
{
    /// <summary>
    /// 服务器列表里的一条。地区是后补的 —— 官方服务器列表接口不返回数据中心，
    /// 得逐个发 join 请求换出来，所以做成可通知的，查到哪个更新哪个
    /// </summary>
    public class RobloxServer : INotifyPropertyChanged
    {
        public string JobId { get; set; } = String.Empty;

        public int Playing { get; set; }

        public int MaxPlayers { get; set; }

        public int Ping { get; set; }

        public int? DataCenterId { get; set; }

        private string _region = "查询中";
        public string Region
        {
            get => _region;
            set { _region = value; OnPropertyChanged(); OnPropertyChanged(nameof(RegionReady)); }
        }

        public bool RegionReady => _region != "查询中";

        public string PlayerCountText => $"{Playing}/{MaxPlayers}";

        public string PingText => Ping > 0 ? $"{Ping} ms" : "-";

        public string JoinLink => $"roblox://experiences/start?placeId={PlaceId}&gameInstanceId={JobId}";

        public long PlaceId { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
