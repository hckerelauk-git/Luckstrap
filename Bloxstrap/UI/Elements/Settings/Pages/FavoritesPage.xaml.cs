using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Bloxstrap.Integrations;
using Bloxstrap.Models.Entities;
using Bloxstrap.Utility;
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

            // Loaded 是 async void 的 lambda，里面抛异常没人接，会直接打到 UI 线程把程序干掉
            Loaded += async (_, _) =>
            {
                try
                {
                    RecentBox.ItemsSource = await RecentServers.LoadAsync();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("FavoritesPage", "Failed to load recent games");
                    App.Logger.WriteException("FavoritesPage", ex);
                }
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
                ShowStatus(Strings.Dlg_076, Strings.Dlg_077, InfoBarSeverity.Warning);
                return;
            }

            // 超长数字会让 long.Parse 抛 OverflowException，这一行在 try 外面，抛了就是闪退
            if (!long.TryParse(placeIdText, out long placeId) || placeId <= 0)
            {
                ShowStatus(Strings.Dlg_076, Strings.Dlg_077, InfoBarSeverity.Warning);
                return;
            }

            if (FavoriteStore.Contains(placeId))
            {
                ShowStatus(Strings.Dlg_078, String.Format(Strings.DlgF_AlreadyFavorite, placeId), InfoBarSeverity.Informational);
                return;
            }

            _busy = true;
            AddButton.IsEnabled = false;
            ShowStatus(Strings.Dlg_079, Strings.Dlg_080, InfoBarSeverity.Informational);

            try
            {
                var game = await FavoriteStore.CreateAsync(placeId);

                FavoriteStore.Add(game);
                Refresh();

                InputBox.Text = "";

                if (String.IsNullOrEmpty(game.Name))
                    ShowStatus(Strings.Dlg_081, Strings.Dlg_082, InfoBarSeverity.Warning);
                else
                    ShowStatus(Strings.Dlg_083, game.Name, InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FavoritesPage::OnAdd", "Failed to add favorite");
                App.Logger.WriteException("FavoritesPage::OnAdd", ex);
                ShowStatus(Strings.Dlg_084, ex.Message, InfoBarSeverity.Error);
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

                ShowStatus(Strings.Dlg_085, game.Name, InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FavoritesPage::OnLaunch", "Failed to launch");
                App.Logger.WriteException("FavoritesPage::OnLaunch", ex);
                ShowStatus(Strings.Dlg_086, ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void OnAddShortcut(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not FavoriteGame game)
                return;

            if (sender is not Wpf.Ui.Controls.Button button || !button.IsEnabled)
                return;

            string name = String.IsNullOrWhiteSpace(game.Name) ? $"PlaceId {game.PlaceId}" : game.Name;

            string link = String.IsNullOrEmpty(game.AccessCode)
                ? $"roblox://experiences/start?placeId={game.PlaceId}"
                : $"roblox://experiences/start?placeId={game.PlaceId}&accessCode={game.AccessCode}";

            button.IsEnabled = false;
            ShowStatus(Strings.Shortcut_Creating, name, InfoBarSeverity.Informational);

            try
            {
                string? icon = await GameShortcut.GetIconAsync(game.PlaceId, game.ThumbnailUrl);

                GameShortcut.Create(name, link, new[] { Paths.Desktop }, icon);

                ShowStatus(Strings.Dlg_092, $"{GameShortcut.SanitizeName(name)}.lnk", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FavoritesPage::OnAddShortcut", "Failed to create shortcut");
                App.Logger.WriteException("FavoritesPage::OnAddShortcut", ex);
                ShowStatus(Strings.Dlg_091, ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void OnRemove(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not FavoriteGame game)
                return;

            FavoriteStore.Remove(game.PlaceId);
            Refresh();

            ShowStatus(Strings.Dlg_087, String.IsNullOrEmpty(game.Name) ? $"PlaceId {game.PlaceId}" : game.Name, InfoBarSeverity.Informational);
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
