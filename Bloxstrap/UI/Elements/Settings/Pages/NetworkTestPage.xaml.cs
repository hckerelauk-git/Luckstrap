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
            (Strings.Dlg_108, "https://www.roblox.com/"),
            (Strings.Dlg_109, "https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer"),
            (Strings.Dlg_110, "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer"),
            (Strings.Dlg_111, "https://games.roblox.com/v1/games?universeIds=1"),
            (Strings.Dlg_112, "https://thumbnails.roblox.com/v1/games/icons?universeIds=1&returnPolicy=PlaceHolder&size=128x128&format=Png&isCircular=false"),
            (Strings.Dlg_113, "https://apis.roblox.com/universes/v1/places/1818/universe"),
            (Strings.Dlg_114, "https://users.roblox.com/v1/users/1"),
            (Strings.Dlg_115, "https://ipinfo.io/1.1.1.1/json")
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

            ShowStatus(Strings.Dlg_116, String.Format(Strings.DlgF_TestingCount, results.Count), InfoBarSeverity.Informational);

            foreach (var result in results)
            {
                result.Status = "…";
                result.Detail = Strings.Dlg_117;

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
                    result.Detail = String.Format(Strings.DlgF_Timeout, sw.ElapsedMilliseconds);
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
                ShowStatus(Strings.Dlg_118, String.Format(Strings.DlgF_AllPassed, passed), InfoBarSeverity.Success);
            else
                ShowStatus(String.Format(Strings.DlgF_SomeFailed, failed), String.Format(Strings.DlgF_FailedDetail, passed, failed), InfoBarSeverity.Warning);
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            if (ResultList.ItemsSource is not List<NetworkProbeResult> results)
                return;

            var sb = new StringBuilder();
            sb.AppendLine(String.Format(Strings.DlgF_ReportTitle, DateTime.Now));
            sb.AppendLine();

            foreach (var r in results)
                sb.AppendLine($"{r.Status} {r.Name} - {r.Detail}");

            try
            {
                Clipboard.SetText(sb.ToString());
                ShowStatus(Strings.Dlg_119, Strings.Dlg_120, InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus(Strings.Dlg_121, ex.Message, InfoBarSeverity.Error);
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
