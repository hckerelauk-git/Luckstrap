using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bloxstrap.Models.Entities
{
    /// <summary>
    /// 网络诊断里的一行。测试结果直接反映到界面上
    /// </summary>
    public class NetworkProbeResult : INotifyPropertyChanged
    {
        public string Name { get; set; } = String.Empty;

        public string Url { get; set; } = String.Empty;

        private string _status = "未测试";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOk)); OnPropertyChanged(nameof(IsFailed)); }
        }

        private string _detail = String.Empty;
        public string Detail
        {
            get => _detail;
            set { _detail = value; OnPropertyChanged(); }
        }

        private bool? _ok;
        public bool? Ok
        {
            get => _ok;
            set { _ok = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOk)); OnPropertyChanged(nameof(IsFailed)); }
        }

        public bool IsOk => _ok == true;

        public bool IsFailed => _ok == false;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
