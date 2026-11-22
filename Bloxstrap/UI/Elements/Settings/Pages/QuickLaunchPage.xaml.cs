using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// 快速启动页：把 Place ID / 游戏链接 / 私服代码转成 roblox:// 链接，或者直接拉起客户端。
    /// </summary>
    public partial class QuickLaunchPage
    {
        private static readonly Regex GamesUrlRegex = new(@"games/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PlaceIdRegex = new(@"placeId=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DigitsRegex = new(@"^\d+$", RegexOptions.Compiled);

        private string _placeId = "";
        private string _jobId = "";

        public QuickLaunchPage()
        {
            InitializeComponent();
        }

        private void OnInputChanged(object sender, TextChangedEventArgs e)
        {
            if (ResultBox is null)
                return;

            _placeId = ExtractPlaceId(PlaceIdBox.Text);
            _jobId = (JobIdBox.Text ?? "").Trim();

            // 私服链接里带 jobId 的情况，把 ?privateServerLinkCode= 后面那串也认出来
            if (_jobId.Contains("privateServerLinkCode="))
            {
                int idx = _jobId.IndexOf("privateServerLinkCode=", StringComparison.OrdinalIgnoreCase);
                _jobId = _jobId[(idx + "privateServerLinkCode=".Length)..];
                _jobId = _jobId.Split('&')[0].Trim();
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

        private static string ExtractPlaceId(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            input = input.Trim();

            if (DigitsRegex.IsMatch(input))
                return input;

            Match m = GamesUrlRegex.Match(input);
            if (m.Success)
                return m.Groups[1].Value;

            m = PlaceIdRegex.Match(input);
            if (m.Success)
                return m.Groups[1].Value;

            return "";
        }

        private string BuildLink()
        {
            if (string.IsNullOrEmpty(_placeId))
                return "";

            if (string.IsNullOrEmpty(_jobId))
                return $"roblox://placeId={_placeId}";

            return $"roblox://placeId={_placeId}&gameInstanceId={_jobId}";
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

                ShowStatus("已拉起客户端", "正在启动，窗口可能要等几秒才出来。", Wpf.Ui.Controls.InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("QuickLaunchPage::OnLaunch", $"Failed: {ex}");
                ShowStatus("启动失败", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
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
                ShowStatus("已复制", link, Wpf.Ui.Controls.InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus("复制失败", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
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
                ShowStatus("打不开", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
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
