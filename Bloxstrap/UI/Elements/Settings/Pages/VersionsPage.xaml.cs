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
            SummaryText.Text = Strings.Dlg_145;

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
                            subtitle = Strings.Dlg_146;
                        else if (isPlayer)
                            subtitle = Strings.Dlg_147;
                        else if (isStudio)
                            subtitle = Strings.Dlg_148;
                        else
                            subtitle = String.Format(Strings.DlgF_LastModified, dir.LastWriteTime);

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
                ? Strings.Dlg_149
                : String.Format(Strings.DlgF_VersionSummary, _entries.Count, FormatSize(total), FormatSize(deletable));
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
                StatusBar.Title = Strings.Dlg_150;
                StatusBar.Message = Strings.Dlg_151;
                StatusBar.Severity = Wpf.Ui.Controls.InfoBarSeverity.Warning;
                StatusBar.IsOpen = true;
                return;
            }

            long totalBytes = targets.Sum(t => t.SizeBytes);
            string names = string.Join("\n", targets.Select(t => "· " + t.Name));

            var confirm = MessageBox.Show(
                String.Format(Strings.DlgF_DeleteVersionsConfirm, targets.Count, FormatSize(totalBytes), names),
                Strings.Dlg_152,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            StatusBar.Title = Strings.Dlg_153;
            StatusBar.Message = Strings.Dlg_154;
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

            StatusBar.Title = failed == 0 ? Strings.Dlg_155 : Strings.Dlg_156;
            StatusBar.Message = failed == 0
                ? String.Format(Strings.DlgF_DeletedSummary, ok, FormatSize(totalBytes))
                : String.Format(Strings.DlgF_DeletePartial, ok, failed);
            StatusBar.Severity = failed == 0
                ? Wpf.Ui.Controls.InfoBarSeverity.Success
                : Wpf.Ui.Controls.InfoBarSeverity.Warning;
            StatusBar.IsOpen = true;
        }
    }
}
