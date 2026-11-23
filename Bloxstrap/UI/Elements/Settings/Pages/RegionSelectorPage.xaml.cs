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
                    HintText.Text = Strings.Dlg_129;
                    ShowStatus(Strings.Dlg_130, Strings.Dlg_131, InfoBarSeverity.Warning);
                    return;
                }

                RegionBox.ItemsSource = datacenters.Value.Regions;
                RegionBox.SelectedIndex = 0;

                HintText.Text = Strings.Dlg_132;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::LoadRegions", "Failed to load regions");
                App.Logger.WriteException("RegionSelectorPage::LoadRegions", ex);

                HintText.Text = Strings.Dlg_133;
                ShowStatus(Strings.Dlg_130, ex.Message, InfoBarSeverity.Warning);
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
                ShowStatus(Strings.Dlg_076, Strings.Dlg_077, InfoBarSeverity.Warning);
                return;
            }

            if (RegionBox.SelectedItem is not string region || String.IsNullOrWhiteSpace(region))
            {
                ShowStatus(Strings.Dlg_134, Strings.Dlg_135, InfoBarSeverity.Warning);
                return;
            }

            long placeId = long.Parse(placeIdText);

            SetBusy(true);
            HintText.Visibility = Visibility.Collapsed;

            try
            {
                ShowStatus(Strings.Dlg_136, String.Format(Strings.DlgF_RegionSearching, region), InfoBarSeverity.Informational);

                RobloxServer? server = await _fetcher.FindServerInRegionAsync(placeId, region, (done, total) =>
                {
                    // 回调在后台线程，改控件要切回 UI
                    Dispatcher.BeginInvoke(new Action(() =>
                        StatusBar.Message = String.Format(Strings.DlgF_RegionProgress, region, done, total)));
                });

                if (server is null)
                {
                    ShowStatus(
                        Strings.Dlg_097,
                        String.Format(Strings.DlgF_RegionEmpty, region),
                        InfoBarSeverity.Warning);

                    HintText.Text = String.Format(Strings.DlgF_RegionNoServer, region);
                    HintText.Visibility = Visibility.Visible;
                    return;
                }

                Launch(server);

                ShowStatus(Strings.Dlg_085, $"{server.Region} · {server.PlayerCountText}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::OnJoinRegion", "Find failed");
                App.Logger.WriteException("RegionSelectorPage::OnJoinRegion", ex);
                ShowStatus(Strings.Dlg_036, ex.Message, InfoBarSeverity.Error);
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
                ShowStatus(Strings.Dlg_076, Strings.Dlg_077, InfoBarSeverity.Warning);
                return;
            }

            long placeId = long.Parse(placeIdText);

            SetBusy(true);
            ServerList.ItemsSource = null;
            HintText.Visibility = Visibility.Collapsed;

            ShowStatus(Strings.Dlg_137, Strings.Dlg_138, InfoBarSeverity.Informational);

            try
            {
                _servers = await _fetcher.GetServersAsync(placeId);

                if (_servers.Count == 0)
                {
                    ShowStatus(Strings.Dlg_139, Strings.Dlg_140, InfoBarSeverity.Warning);
                    HintText.Text = Strings.Dlg_141;
                    HintText.Visibility = Visibility.Visible;
                    return;
                }

                ServerList.ItemsSource = _servers;

                int known = _servers.Count(x => x.RegionReady && x.Region != Strings.Server_RegionUnknown);

                ShowStatus(
                    String.Format(Strings.DlgF_ServerCount, _servers.Count),
                    known > 0 ? String.Format(Strings.DlgF_RegionKnown, known) : Strings.Dlg_143,
                    known > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::OnQuery", "Query failed");
                App.Logger.WriteException("RegionSelectorPage::OnQuery", ex);
                ShowStatus(Strings.Dlg_144, ex.Message, InfoBarSeverity.Error);
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
                ShowStatus(Strings.Dlg_085, $"{server.Region} · {server.PlayerCountText}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RegionSelectorPage::OnJoin", "Launch failed");
                App.Logger.WriteException("RegionSelectorPage::OnJoin", ex);
                ShowStatus(Strings.Dlg_086, ex.Message, InfoBarSeverity.Error);
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
