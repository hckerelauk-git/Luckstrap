using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Bloxstrap.Integrations;
using Bloxstrap.Models.Entities;
using Wpf.Ui.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// 区服选择页。两种用法：
    ///   选一个地区，直接进那个地区的服（边查边找，命中就停）
    ///   或者列全部服务器，自己从里面挑
    /// </summary>
    public partial class RegionSelectorPage
    {
        private readonly RobloxServerFetcher _fetcher = new();

        private List<RobloxServer> _servers = new();

        private bool _busy = false;

        public RegionSelectorPage()
        {
            InitializeComponent();

            Loaded += async (_, _) =>
            {
                await LoadRegionsAsync();
                await LoadGamePicksAsync();
            };
        }

        private async Task LoadGamePicksAsync()
        {
            if (GameBox.Items.Count > 0)
                return;

            try
            {
                GameBox.ItemsSource = await RecentServers.LoadPicksAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::LoadGamePicks", "Failed to load game picks");
                App.Logger.WriteException("RegionSelectorPage::LoadGamePicks", ex);
            }
        }

        private void OnGameSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (GameBox.SelectedItem is not GamePickItem game)
                return;

            PlaceIdBox.Text = game.PlaceId.ToString();
        }

        // ── 地区下拉 ──────────────────────────────────────────────────

        private async Task LoadRegionsAsync()
        {
            if (RegionBox.Items.Count > 0)
                return;

            try
            {
                var datacenters = await _fetcher.GetDatacentersAsync();

                if (datacenters is null)
                {
                    HintText.Text = "地区列表没拉到。点「看全部服务器」还能用，那边查到的地区一样能挑。";
                    ShowStatus("地区列表没拉到", "机房对照表取不到，多半是网络问题。直接点「看全部服务器」手动挑。", InfoBarSeverity.Warning);
                    return;
                }

                RegionBox.ItemsSource = datacenters.Value.Regions;
                RegionBox.SelectedIndex = 0;

                HintText.Text = "填 Place ID，选个地区，点「进这个地区的服」。";
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::LoadRegions", "Failed to load regions");
                App.Logger.WriteException("RegionSelectorPage::LoadRegions", ex);

                HintText.Text = "地区列表没拉到。点「看全部服务器」还能用。";
                ShowStatus("地区列表没拉到", ex.Message, InfoBarSeverity.Warning);
            }
        }

        // ── 按地区进服 ────────────────────────────────────────────────

        private async void OnJoinRegion(object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;

            string placeIdText = Utilities.ExtractPlaceId(PlaceIdBox.Text);

            if (String.IsNullOrEmpty(placeIdText))
            {
                ShowStatus("认不出这个输入", "要 Place ID，或者带 /games/数字 的链接。", InfoBarSeverity.Warning);
                return;
            }

            if (RegionBox.SelectedItem is not string region || String.IsNullOrWhiteSpace(region))
            {
                ShowStatus("先选个地区", "上面那个下拉框里挑一个。", InfoBarSeverity.Warning);
                return;
            }

            long placeId = long.Parse(placeIdText);

            SetBusy(true);
            HintText.Visibility = Visibility.Collapsed;

            try
            {
                ShowStatus("正在找", $"{region} · 先拉服务器列表，再逐个查地区，找到一台就进。", InfoBarSeverity.Informational);

                RobloxServer? server = await _fetcher.FindServerInRegionAsync(placeId, region, (done, total) =>
                {
                    // 回调在后台线程，改控件要切回 UI
                    Dispatcher.BeginInvoke(new Action(() =>
                        StatusBar.Message = $"{region} · 已查 {done}/{total} 台"));
                });

                if (server is null)
                {
                    ShowStatus(
                        "没找到",
                        $"这个游戏现在没有 {region} 的公开服务器。换个地区，或者点「看全部服务器」自己挑一台。",
                        InfoBarSeverity.Warning);

                    HintText.Text = $"{region} 没有可进的服务器。";
                    HintText.Visibility = Visibility.Visible;
                    return;
                }

                Launch(server);

                ShowStatus("已拉起客户端", $"{server.Region} · {server.PlayerCountText}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::OnJoinRegion", "Find failed");
                App.Logger.WriteException("RegionSelectorPage::OnJoinRegion", ex);
                ShowStatus("出错了", ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ── 全部服务器 ────────────────────────────────────────────────

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

            SetBusy(true);
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

                ServerList.ItemsSource = _servers;

                int known = _servers.Count(x => x.RegionReady && x.Region != "未知");

                ShowStatus(
                    $"查到 {_servers.Count} 台服务器",
                    known > 0 ? $"{known} 台拿到了地区，挑一台进。" : "地区全没拿到，可能是没登录或者接口挂了，服务器本身还能进。",
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
                SetBusy(false);
            }
        }

        private void OnJoin(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not RobloxServer server)
                return;

            try
            {
                Launch(server);
                ShowStatus("已拉起客户端", $"{server.Region} · {server.PlayerCountText}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::OnJoin", "Launch failed");
                App.Logger.WriteException("RegionSelectorPage::OnJoin", ex);
                ShowStatus("启动失败", ex.Message, InfoBarSeverity.Error);
            }
        }

        // ── 杂项 ──────────────────────────────────────────────────────

        private static void Launch(RobloxServer server)
        {
            App.Logger.WriteLine("RegionSelectorPage::Launch", $"Joining {server.JoinLink}");

            // 拉新进程，别在设置窗口里直接 App.Terminate
            Process.Start(new ProcessStartInfo
            {
                FileName = Paths.Process,
                Arguments = $"\"{server.JoinLink}\"",
                UseShellExecute = true
            });
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            JoinButton.IsEnabled = !busy;
            ListButton.IsEnabled = !busy;
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
