using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Bloxstrap.Integrations;
using Bloxstrap.Models.Entities;
using Wpf.Ui.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// 地区选择器页：列服务器、标地区、挑一个进。
    /// </summary>
    public partial class RegionSelectorPage
    {
        private const string AllRegions = "全部地区";

        private readonly RobloxServerFetcher _fetcher = new();

        private List<RobloxServer> _servers = new();

        private bool _busy = false;

        public RegionSelectorPage()
        {
            InitializeComponent();

            RegionBox.ItemsSource = new List<string> { AllRegions };
            RegionBox.SelectedIndex = 0;
        }

        private async void OnQuery(object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;

            string placeIdText = Utilities.ExtractPlaceId(PlaceIdBox.Text);

            if (String.IsNullOrEmpty(placeIdText))
            {
                ShowStatus("认不出这个输入", "要 Place ID，或者带 /games/数字 的链接。", InfoBarSeverity.Warning);
                return;
            }

            long placeId = long.Parse(placeIdText);

            _busy = true;
            QueryButton.IsEnabled = false;
            ServerList.ItemsSource = null;
            HintText.Visibility = Visibility.Collapsed;

            ShowStatus("正在查询", "先拉服务器列表，再逐个查地区，100 台大概要十几秒。", InfoBarSeverity.Informational);

            try
            {
                _servers = await _fetcher.GetServersAsync(placeId);

                if (_servers.Count == 0)
                {
                    ShowStatus("没查到服务器", "这个 Place 可能没有公开服务器，或者接口暂时不可用。", InfoBarSeverity.Warning);
                    HintText.Text = "没查到服务器。";
                    HintText.Visibility = Visibility.Visible;
                    return;
                }

                FillRegionFilter();
                ApplyFilter();

                int known = _servers.Count(x => x.RegionReady && x.Region != "未知");

                ShowStatus(
                    $"查到 {_servers.Count} 台服务器",
                    known > 0 ? $"{known} 台拿到了地区，选一个进。" : "地区全没拿到，可能是没登录或者接口挂了，服务器本身还能进。",
                    known > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::OnQuery", "Query failed");
                App.Logger.WriteException("RegionSelectorPage::OnQuery", ex);
                ShowStatus("查询失败", ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                _busy = false;
                QueryButton.IsEnabled = true;
            }
        }

        private void FillRegionFilter()
        {
            var regions = new List<string> { AllRegions };

            regions.AddRange(_servers
                .Select(x => x.Region)
                .Where(x => !String.IsNullOrEmpty(x))
                .Distinct()
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            RegionBox.ItemsSource = regions;
            RegionBox.SelectedIndex = 0;
        }

        private void OnRegionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ServerList is not null)
                ApplyFilter();
        }

        private void ApplyFilter()
        {
            string? selected = RegionBox.SelectedItem as string;

            if (String.IsNullOrEmpty(selected) || selected == AllRegions)
            {
                ServerList.ItemsSource = null;
                ServerList.ItemsSource = _servers;
            }
            else
            {
                var filtered = _servers.Where(x => x.Region == selected).ToList();
                ServerList.ItemsSource = null;
                ServerList.ItemsSource = filtered;
            }

            HintText.Visibility = _servers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnJoin(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not RobloxServer server)
                return;

            try
            {
                App.Logger.WriteLine("RegionSelectorPage::OnJoin", $"Joining {server.JoinLink}");

                // 拉新进程，别在设置窗口里直接 App.Terminate
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = $"\"{server.JoinLink}\"",
                    UseShellExecute = true
                });

                ShowStatus("已拉起客户端", $"{server.Region} · {server.PlayerCountText}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::OnJoin", "Launch failed");
                App.Logger.WriteException("RegionSelectorPage::OnJoin", ex);
                ShowStatus("启动失败", ex.Message, InfoBarSeverity.Error);
            }
        }

        private void ShowStatus(string title, string message, InfoBarSeverity severity)
        {
            StatusBar.Title = title;
            StatusBar.Message = message;
            StatusBar.Severity = severity;
            StatusBar.IsOpen = true;
        }
    }
}
