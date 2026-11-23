using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Bloxstrap.Integrations;
using Bloxstrap.Models.Entities;
using Wpf.Ui.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// 收藏夹页：存常用游戏，一键拉起客户端。
    /// </summary>
    public partial class FavoritesPage
    {
        private bool _busy = false;

        public FavoritesPage()
        {
            InitializeComponent();
            Refresh();

            Loaded += async (_, _) =>
            {
                RecentBox.ItemsSource = await RecentServers.LoadAsync();
            };
        }

        private void OnRecentSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (RecentBox.SelectedItem is not RecentGame game)
                return;

            InputBox.Text = game.PlaceId.ToString();
        }

        private void Refresh()
        {
            var favorites = FavoriteStore.Load();

            FavoritesList.ItemsSource = null;
            FavoritesList.ItemsSource = favorites;

            EmptyHint.Visibility = favorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                OnAdd(sender, e);
        }

        private async void OnAdd(object sender, RoutedEventArgs e)
        {
            if (_busy)
                return;

            string placeIdText = Utilities.ExtractPlaceId(InputBox.Text);

            if (String.IsNullOrEmpty(placeIdText))
            {
                ShowStatus("认不出这个输入", "要 Place ID，或者带 /games/数字 的链接。", InfoBarSeverity.Warning);
                return;
            }

            long placeId = long.Parse(placeIdText);

            if (FavoriteStore.Contains(placeId))
            {
                ShowStatus("已经收藏过了", $"PlaceId {placeId} 已经在列表里。", InfoBarSeverity.Informational);
                return;
            }

            _busy = true;
            AddButton.IsEnabled = false;
            ShowStatus("正在获取游戏信息", "第一次添加要拉一次名字和图标，稍等。", InfoBarSeverity.Informational);

            try
            {
                var game = await FavoriteStore.CreateAsync(placeId);

                FavoriteStore.Add(game);
                Refresh();

                InputBox.Text = "";

                if (String.IsNullOrEmpty(game.Name))
                    ShowStatus("加进去了，但没拿到游戏信息", "可能是网络问题或者这个 Place 不存在。启动不受影响。", InfoBarSeverity.Warning);
                else
                    ShowStatus("已收藏", game.Name, InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FavoritesPage::OnAdd", "Failed to add favorite");
                App.Logger.WriteException("FavoritesPage::OnAdd", ex);
                ShowStatus("添加失败", ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                _busy = false;
                AddButton.IsEnabled = true;
            }
        }

        private void OnLaunch(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not FavoriteGame game)
                return;

            try
            {
                App.Logger.WriteLine("FavoritesPage::OnLaunch", $"Launching {game.LaunchLink}");

                // 跟快速启动页一样，拉一个新进程，别在设置窗口里直接 App.Terminate
                var psi = new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = $"\"{game.LaunchLink}\"",
                    UseShellExecute = true
                };

                Process.Start(psi);

                ShowStatus("已拉起客户端", game.Name, InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FavoritesPage::OnLaunch", "Failed to launch");
                App.Logger.WriteException("FavoritesPage::OnLaunch", ex);
                ShowStatus("启动失败", ex.Message, InfoBarSeverity.Error);
            }
        }

        private void OnRemove(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not FavoriteGame game)
                return;

            FavoriteStore.Remove(game.PlaceId);
            Refresh();

            ShowStatus("已移除", String.IsNullOrEmpty(game.Name) ? $"PlaceId {game.PlaceId}" : game.Name, InfoBarSeverity.Informational);
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
