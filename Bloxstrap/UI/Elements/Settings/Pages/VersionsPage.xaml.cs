using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public class VersionEntry : INotifyPropertyChanged
    {
        private bool _selected;

        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string SizeText { get; set; } = "";
        public long SizeBytes { get; set; }
        public bool CanDelete { get; set; } = true;

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// 旧版本清理页：扫描 Versions 目录，列出每个版本包占多少空间，勾选删掉。
    /// 正在使用的 Player / Studio 版本不允许删。
    /// </summary>
    public partial class VersionsPage
    {
        private readonly ObservableCollection<VersionEntry> _entries = new();

        public VersionsPage()
        {
            InitializeComponent();
            VersionList.ItemsSource = _entries;
            Loaded += async (_, _) => await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            _entries.Clear();
            SummaryText.Text = "扫描中…";

            string currentPlayer = App.RobloxState.Prop.Player.VersionGuid ?? "";
            string currentStudio = App.RobloxState.Prop.Studio.VersionGuid ?? "";

            var list = await Task.Run(() =>
            {
                var result = new System.Collections.Generic.List<VersionEntry>();

                try
                {
                    if (!Directory.Exists(Paths.Versions))
                        return result;

                    foreach (var dir in new DirectoryInfo(Paths.Versions).GetDirectories())
                    {
                        bool isPlayer = !string.IsNullOrEmpty(currentPlayer) && dir.Name.Equals(currentPlayer, StringComparison.OrdinalIgnoreCase);
                        bool isStudio = !string.IsNullOrEmpty(currentStudio) && dir.Name.Equals(currentStudio, StringComparison.OrdinalIgnoreCase);

                        long size = GetDirectorySize(dir.FullName);

                        string subtitle;
                        if (isPlayer && isStudio)
                            subtitle = "正在使用（Player + Studio）";
                        else if (isPlayer)
                            subtitle = "正在使用（Player）";
                        else if (isStudio)
                            subtitle = "正在使用（Studio）";
                        else
                            subtitle = $"最后修改 {dir.LastWriteTime:yyyy-MM-dd HH:mm}";

                        result.Add(new VersionEntry
                        {
                            Name = dir.Name,
                            FullPath = dir.FullName,
                            Subtitle = subtitle,
                            SizeBytes = size,
                            SizeText = FormatSize(size),
                            CanDelete = !(isPlayer || isStudio)
                        });
                    }

                    result.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("VersionsPage::Refresh", ex.ToString());
                }

                return result;
            });

            foreach (var e in list)
                _entries.Add(e);

            UpdateSummary();
        }

        private static long GetDirectorySize(string path)
        {
            try
            {
                return new DirectoryInfo(path)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024 / 1024:F2} GB";
            if (bytes >= 1024L * 1024)
                return $"{bytes / 1024.0 / 1024:F1} MB";
            return $"{bytes / 1024.0:F0} KB";
        }

        private void UpdateSummary()
        {
            long deletable = _entries.Where(e => e.CanDelete).Sum(e => e.SizeBytes);
            long total = _entries.Sum(e => e.SizeBytes);

            SummaryText.Text = _entries.Count == 0
                ? "没有找到版本包"
                : $"共 {_entries.Count} 个版本，占用 {FormatSize(total)}，其中可删 {FormatSize(deletable)}";
        }

        private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync();

        private void OnSelectAll(object sender, RoutedEventArgs e)
        {
            // 循环变量不能叫 e，会跟事件参数 e 撞（CS0136）
            foreach (var entry in _entries.Where(x => x.CanDelete))
                entry.Selected = true;
        }

        private async void OnDelete(object sender, RoutedEventArgs e)
        {
            var targets = _entries.Where(x => x.Selected && x.CanDelete).ToList();

            if (targets.Count == 0)
            {
                StatusBar.Title = "没选东西";
                StatusBar.Message = "先勾选要删的版本。正在使用的版本删不了。";
                StatusBar.Severity = Wpf.Ui.Controls.InfoBarSeverity.Warning;
                StatusBar.IsOpen = true;
                return;
            }

            long totalBytes = targets.Sum(t => t.SizeBytes);
            string names = string.Join("\n", targets.Select(t => "· " + t.Name));

            var confirm = MessageBox.Show(
                $"要删掉这 {targets.Count} 个版本包吗？能腾出 {FormatSize(totalBytes)}。\n\n{names}\n\n删了之后如果游戏要用这个版本，客户端会重新下载。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            StatusBar.Title = "正在删";
            StatusBar.Message = "别关窗口，删完会刷新列表。";
            StatusBar.Severity = Wpf.Ui.Controls.InfoBarSeverity.Informational;
            StatusBar.IsOpen = true;

            int ok = 0, failed = 0;

            await Task.Run(() =>
            {
                foreach (var t in targets)
                {
                    try
                    {
                        Directory.Delete(t.FullPath, true);
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        App.Logger.WriteLine("VersionsPage::Delete", $"{t.FullPath}: {ex.Message}");
                    }
                }
            });

            await RefreshAsync();

            StatusBar.Title = failed == 0 ? "删完了" : "删了一部分";
            StatusBar.Message = failed == 0
                ? $"清掉 {ok} 个版本，腾出 {FormatSize(totalBytes)}。"
                : $"{ok} 个成功，{failed} 个失败（可能被占用）。详情看日志。";
            StatusBar.Severity = failed == 0
                ? Wpf.Ui.Controls.InfoBarSeverity.Success
                : Wpf.Ui.Controls.InfoBarSeverity.Warning;
            StatusBar.IsOpen = true;
        }
    }
}
