using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Bloxstrap.Models.Entities
{
    /// <summary>
    /// 收藏的游戏。只存展示和启动需要的字段，
    /// 名字和缩略图在添加时抓一次，之后就算接口挂了也能正常显示。
    /// </summary>
    public class FavoriteGame : INotifyPropertyChanged
    {
        public long PlaceId { get; set; }

        public long UniverseId { get; set; }

        private string _name = String.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _creatorName = String.Empty;
        public string CreatorName
        {
            get => _creatorName;
            set { _creatorName = value; OnPropertyChanged(); }
        }

        // 留 null 而不是空字符串。WPF 的 ImageSourceConverter 遇到 "" 会抛转换异常刷 binding 日志
        private string? _thumbnailUrl;
        public string? ThumbnailUrl
        {
            get => _thumbnailUrl;
            set { _thumbnailUrl = value; OnPropertyChanged(); }
        }

        public DateTime TimeAdded { get; set; } = DateTime.Now;

        /// <summary>
        /// 私服代码，可选。有的话启动时带上 gameInstanceId
        /// </summary>
        public string AccessCode { get; set; } = String.Empty;

        [JsonIgnore]
        public string LaunchLink => String.IsNullOrEmpty(AccessCode)
            ? $"roblox://experiences/start?placeId={PlaceId}"
            : $"roblox://experiences/start?placeId={PlaceId}&accessCode={AccessCode}";

        [JsonIgnore]
        public string Subtitle => String.IsNullOrEmpty(CreatorName)
            ? $"PlaceId {PlaceId}"
            : $"by {CreatorName}";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
