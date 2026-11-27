using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Bloxstrap.Integrations;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// 快速启动页：把 Place ID / 游戏链接 / 私服代码转成 roblox:// 链接，或者直接拉起客户端。
    /// </summary>
    public partial class QuickLaunchPage
    {
        private string _placeId = "";
        private string _jobId = "";
        private string _accessCode = "";

        public QuickLaunchPage()
        {
            InitializeComponent();

            // Loaded 是 async void 的 lambda，里面抛异常没人接，会直接打到 UI 线程把程序干掉
            Loaded += async (_, _) =>
            {
                try
                {
                    RecentBox.ItemsSource = await RecentServers.LoadAsync();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("QuickLaunchPage", "Failed to load recent games");
                    App.Logger.WriteException("QuickLaunchPage", ex);
                }
            };
        }

        private void OnRecentSelected(object sender, SelectionChangedEventArgs e)
        {
            if (RecentBox.SelectedItem is not RecentGame game)
                return;

            PlaceIdBox.Text = game.PlaceId.ToString();

            // 私服优先填私服码，公开服填 jobId，不然选中一条记录只能进游戏、回不到那个服
            if (!String.IsNullOrEmpty(game.AccessCode))
                JobIdBox.Text = game.AccessCode;
            else if (!String.IsNullOrEmpty(game.JobId))
                JobIdBox.Text = game.JobId;
        }

        private void OnInputChanged(object sender, TextChangedEventArgs e)
        {
            if (ResultBox is null)
                return;

            _placeId = Utilities.ExtractPlaceId(PlaceIdBox.Text);

            string input = (JobIdBox.Text ?? "").Trim();

            _jobId = "";
            _accessCode = "";

            if (input.Contains("privateServerLinkCode="))
            {
                // 私服分享链接里的那串是私服码，只能走 accessCode。
                // 以前一律当 jobId 拼成 gameInstanceId，私服根本进不去
                int idx = input.IndexOf("privateServerLinkCode=", StringComparison.OrdinalIgnoreCase);
                _accessCode = input[(idx + "privateServerLinkCode=".Length)..].Split('&')[0].Trim();
            }
            else if (Guid.TryParse(input, out _))
            {
                // jobId 是 36 位 UUID
                _jobId = input;
            }
            else if (!String.IsNullOrEmpty(input))
            {
                // 既不是 UUID 也不是分享链接，按私服码处理
                _accessCode = input;
            }

            string link = BuildLink();

            if (string.IsNullOrEmpty(link))
            {
                ResultBox.Text = "";
                LaunchButton.IsEnabled = false;
                return;
            }

            ResultBox.Text = link;
            LaunchButton.IsEnabled = true;
        }

        private string BuildLink()
        {
            if (string.IsNullOrEmpty(_placeId))
                return "";

            string link = $"roblox://experiences/start?placeId={_placeId}";

            if (!String.IsNullOrEmpty(_accessCode))
                link += $"&accessCode={_accessCode}";
            else if (!String.IsNullOrEmpty(_jobId))
                link += $"&gameInstanceId={_jobId}";

            return link;
        }

        private void OnLaunch(object sender, RoutedEventArgs e)
        {
            string link = BuildLink();
            if (string.IsNullOrEmpty(link))
                return;

            try
            {
                App.Logger.WriteLine("QuickLaunchPage::OnLaunch", $"Launching {link}");

                // 不走 LaunchHandler.LaunchRoblox，那个会 App.Terminate() 把设置窗口一起关掉。
                // 这里拉一个新的自身进程带 roblox:// 参数，跟从浏览器点 Play 效果一样。
                var psi = new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = $"\"{link}\"",
                    UseShellExecute = true
                };

                Process.Start(psi);

                ShowStatus(Strings.Dlg_085, Strings.Dlg_122, Wpf.Ui.Controls.InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("QuickLaunchPage::OnLaunch", $"Failed: {ex}");
                ShowStatus(Strings.Dlg_086, ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
            }
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            string link = BuildLink();
            if (string.IsNullOrEmpty(link))
                return;

            try
            {
                Clipboard.SetText(link);
                ShowStatus(Strings.Dlg_119, link, Wpf.Ui.Controls.InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus(Strings.Dlg_121, ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
            }
        }

        private void OnOpenWeb(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_placeId))
                return;

            string url = $"https://www.roblox.com/games/{_placeId}";

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowStatus(Strings.Dlg_123, ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
            }
        }

        private void ShowStatus(string title, string message, Wpf.Ui.Controls.InfoBarSeverity severity)
        {
            StatusBar.Title = title;
            StatusBar.Message = message;
            StatusBar.Severity = severity;
            StatusBar.IsOpen = true;
        }
    }
}
