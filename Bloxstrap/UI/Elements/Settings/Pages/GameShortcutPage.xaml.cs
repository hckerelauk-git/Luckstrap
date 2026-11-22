using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Bloxstrap.Integrations;
using Bloxstrap.Models.Entities;
using Bloxstrap.Utility;
using Wpf.Ui.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// 游戏快捷方式页：把「某个游戏 / 某个服务器」做成桌面或开始菜单的 .lnk，
    /// 双击之后走 Luckstrap 正常启动流程，FastFlags、模组、RPC 都还在。
    /// </summary>
    public partial class GameShortcutPage
    {
        private const string ShortcutPrefix = "Luckstrap - ";

        private List<FavoriteGame> _favorites = new();

        public GameShortcutPage()
        {
            InitializeComponent();
            LoadFavorites();
        }

        private void LoadFavorites()
        {
            _favorites = FavoriteStore.Load();

            var items = new List<FavoriteGame>(_favorites);

            // 第一项是「手动输入」，选中它就清空输入框让用户自己填
            items.Insert(0, new FavoriteGame { PlaceId = 0, Name = "手动输入" });

            FavoriteBox.ItemsSource = items;
            FavoriteBox.DisplayMemberPath = "Name";
            FavoriteBox.SelectedIndex = 0;
        }

        private void OnFavoriteSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (FavoriteBox.SelectedItem is not FavoriteGame game || game.PlaceId == 0)
                return;

            PlaceIdBox.Text = game.PlaceId.ToString();

            if (String.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = game.Name;
        }

        private string GetPlaceId()
        {
            return Utilities.ExtractPlaceId(PlaceIdBox.Text);
        }

        /// <summary>
        /// 拼 roblox 深链。私服走 accessCode，公开服用 gameInstanceId
        /// </summary>
        private string BuildLink(string placeId)
        {
            string jobId = (JobIdBox.Text ?? "").Trim();
            string accessCode = (AccessCodeBox.Text ?? "").Trim();

            if (jobId.Contains("privateServerLinkCode="))
            {
                int idx = jobId.IndexOf("privateServerLinkCode=", StringComparison.OrdinalIgnoreCase);
                jobId = jobId[(idx + "privateServerLinkCode=".Length)..].Split('&')[0].Trim();
            }

            string link = $"roblox://experiences/start?placeId={placeId}";

            if (!String.IsNullOrEmpty(accessCode))
                link += $"&accessCode={accessCode}";
            else if (!String.IsNullOrEmpty(jobId))
                link += $"&gameInstanceId={jobId}";

            return link;
        }

        private string GetShortcutName(string placeId)
        {
            string name = (NameBox.Text ?? "").Trim();

            if (String.IsNullOrEmpty(name))
            {
                var favorite = _favorites.FirstOrDefault(x => x.PlaceId.ToString() == placeId);
                name = favorite is not null && !String.IsNullOrEmpty(favorite.Name)
                    ? favorite.Name
                    : placeId;
            }

            // 文件名里不能有这些字符，替换掉免得创建失败
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return ShortcutPrefix + name;
        }

        private List<string> GetShortcutPaths(string name)
        {
            var paths = new List<string>();

            if (DesktopCheck.IsChecked == true)
                paths.Add(Path.Combine(Paths.Desktop, name + ".lnk"));

            if (StartMenuCheck.IsChecked == true)
                paths.Add(Path.Combine(Paths.WindowsStartMenu, name + ".lnk"));

            return paths;
        }

        private bool TryCreateShortcuts(out string link, out string name)
        {
            link = "";
            name = "";

            string placeId = GetPlaceId();

            if (String.IsNullOrEmpty(placeId))
            {
                ShowStatus("认不出这个输入", "要 Place ID，或者带 /games/数字 的链接。", InfoBarSeverity.Warning);
                return false;
            }

            if (DesktopCheck.IsChecked != true && StartMenuCheck.IsChecked != true)
            {
                ShowStatus("没选位置", "桌面和开始菜单至少勾一个。", InfoBarSeverity.Warning);
                return false;
            }

            link = BuildLink(placeId);
            name = GetShortcutName(placeId);

            var paths = GetShortcutPaths(name);

            try
            {
                foreach (string path in paths)
                {
                    // 已经存在就先删，不然 Shortcut.Create 会直接跳过，改了服务器 ID 也不更新
                    if (File.Exists(path))
                        File.Delete(path);

                    Shortcut.Create(Paths.Application, $"\"{link}\"", path);
                }

                App.Logger.WriteLine("GameShortcutPage::TryCreateShortcuts", $"Created {paths.Count} shortcut(s) for {link}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GameShortcutPage::TryCreateShortcuts", "Failed to create shortcut");
                App.Logger.WriteException("GameShortcutPage::TryCreateShortcuts", ex);

                ShowStatus("创建失败", ex.Message, InfoBarSeverity.Error);
                return false;
            }
        }

        private void OnCreateOnly(object sender, RoutedEventArgs e)
        {
            if (!TryCreateShortcuts(out string link, out string name))
                return;

            ShowStatus("快捷方式已创建", $"{name}.lnk\n{link}", InfoBarSeverity.Success);
        }

        private void OnCreateAndLaunch(object sender, RoutedEventArgs e)
        {
            if (!TryCreateShortcuts(out string link, out string name))
                return;

            try
            {
                // 拉一个新进程，别在设置窗口里直接 App.Terminate
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = $"\"{link}\"",
                    UseShellExecute = true
                });

                ShowStatus("已创建并启动", $"{name}.lnk", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GameShortcutPage::OnCreateAndLaunch", "Failed to launch");
                App.Logger.WriteException("GameShortcutPage::OnCreateAndLaunch", ex);

                ShowStatus("快捷方式建好了，但启动失败", ex.Message, InfoBarSeverity.Warning);
            }
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            string placeId = GetPlaceId();

            if (String.IsNullOrEmpty(placeId))
            {
                ShowStatus("先填游戏", "要删哪个游戏的快捷方式，得先知道是哪个游戏。", InfoBarSeverity.Warning);
                return;
            }

            string name = GetShortcutName(placeId);
            int deleted = 0;

            foreach (string path in GetShortcutPaths(name))
            {
                try
                {
                    if (!File.Exists(path))
                        continue;

                    File.Delete(path);
                    deleted++;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("GameShortcutPage::OnDelete", ex);
                }
            }

            if (deleted == 0)
                ShowStatus("没找到", $"桌面和开始菜单里都没有 {name}.lnk", InfoBarSeverity.Informational);
            else
                ShowStatus("已删除", $"删掉了 {deleted} 个 {name}.lnk", InfoBarSeverity.Success);
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
