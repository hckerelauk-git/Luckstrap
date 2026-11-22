using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Bloxstrap.Models.Entities;
using Wpf.Ui.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// 网络诊断页。进不去游戏的时候先跑一遍，看是哪个域名断了。
    /// </summary>
    public partial class NetworkTestPage
    {
        // 这几条都是客户端实际会打到的接口。顺序按「进游戏」的流程排
        private static readonly (string Name, string Url)[] Targets = new[]
        {
            ("Roblox 官网", "https://www.roblox.com/"),
            ("客户端部署（主）", "https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer"),
            ("客户端部署（备）", "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer"),
            ("游戏信息", "https://games.roblox.com/v1/games?universeIds=1"),
            ("游戏图标", "https://thumbnails.roblox.com/v1/games/icons?universeIds=1&returnPolicy=PlaceHolder&size=128x128&format=Png&isCircular=false"),
            ("PlaceId 转换", "https://apis.roblox.com/universes/v1/places/1818/universe"),
            ("用户信息", "https://users.roblox.com/v1/users/1"),
            ("服务器位置", "https://ipinfo.io/1.1.1.1/json")
        };

        private bool _running = false;

        public NetworkTestPage()
        {
            InitializeComponent();
            Reset();
        }

        private void Reset()
        {
            var results = new List<NetworkProbeResult>();

            foreach (var (name, url) in Targets)
                results.Add(new NetworkProbeResult { Name = name, Url = url, Status = "·" });

            ResultList.ItemsSource = results;
        }

        private async void OnRun(object sender, RoutedEventArgs e)
        {
            if (_running)
                return;

            _running = true;
            RunButton.IsEnabled = false;

            Reset();

            var results = (List<NetworkProbeResult>)ResultList.ItemsSource;
            int passed = 0;

            ShowStatus("正在测试", $"一共 {results.Count} 项，一项一项来。", InfoBarSeverity.Informational);

            foreach (var result in results)
            {
                result.Status = "…";
                result.Detail = "连接中";

                var sw = Stopwatch.StartNew();

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    using var response = await App.HttpClient.GetAsync(result.Url, cts.Token);

                    sw.Stop();

                    bool ok = response.IsSuccessStatusCode;

                    result.Ok = ok;
                    result.Status = ok ? "✓" : "✗";
                    result.Detail = $"HTTP {(int)response.StatusCode} · {sw.ElapsedMilliseconds} ms";

                    if (ok)
                        passed++;
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    result.Ok = false;
                    result.Status = "✗";
                    result.Detail = $"超时（>{sw.ElapsedMilliseconds} ms 没有响应）";
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    result.Ok = false;
                    result.Status = "✗";

                    // 把内层异常也带上，SSL 问题经常藏在 InnerException 里
                    string message = ex.Message;

                    if (ex.InnerException is not null)
                        message += $" / {ex.InnerException.Message}";

                    result.Detail = $"{ex.GetType().Name}: {message}";

                    App.Logger.WriteLine("NetworkTestPage::OnRun", $"{result.Url} failed");
                    App.Logger.WriteException("NetworkTestPage::OnRun", ex);
                }
            }

            _running = false;
            RunButton.IsEnabled = true;

            int failed = results.Count - passed;

            if (failed == 0)
                ShowStatus("全部通过", $"{passed} 项都通了，网络没问题。", InfoBarSeverity.Success);
            else
                ShowStatus($"{failed} 项失败", $"通了 {passed} 项，挂了 {failed} 项。看下面标 ✗ 的。", InfoBarSeverity.Warning);
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            if (ResultList.ItemsSource is not List<NetworkProbeResult> results)
                return;

            var sb = new StringBuilder();
            sb.AppendLine($"Luckstrap 网络诊断 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            foreach (var r in results)
                sb.AppendLine($"{r.Status} {r.Name} - {r.Detail}");

            try
            {
                Clipboard.SetText(sb.ToString());
                ShowStatus("已复制", "直接粘给别人看就行。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus("复制失败", ex.Message, InfoBarSeverity.Error);
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
