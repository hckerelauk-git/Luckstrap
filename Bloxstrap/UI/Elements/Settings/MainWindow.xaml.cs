using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

using Bloxstrap.UI.Elements.Settings.Pages;
using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            var viewModel = new MainWindowViewModel();

            viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedSnackbar.Show();
            viewModel.RequestCloseWindowEvent += (_, _) => Close();

            DataContext = viewModel;
            
            InitializeComponent();

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

#if DEBUG // easier access
            EditorWarningNavItem.Visibility = Visibility.Visible;
#endif

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningSnackbar();

            LoadState();

            // 左下角那块要读登录态再打网络请求，异步做，别卡窗口
            Loaded += (_, _) => LoadCurrentAccount();
        }

        // ── 左下角账号 ────────────────────────────────────────────────

        private void AccountCard_Click(object sender, MouseButtonEventArgs e)
            => RootNavigation.Navigate(typeof(AccountsPage));

        private async void LoadCurrentAccount()
        {
            const string LOG_IDENT = "MainWindow::LoadCurrentAccount";

            try
            {
                string? blob = RobloxCookies.ReadBlob();
                string? cookie = String.IsNullOrEmpty(blob) ? null : RobloxCookies.ReadAuthCookie(blob!);

                if (String.IsNullOrEmpty(cookie))
                {
                    AccountDisplayName.Text = "未登录";
                    AccountUserName.Text = "点这里打开账号切换";
                    return;
                }

                AuthenticatedUser? user = await AccountManager.WhoAmI(cookie!);

                if (user is null)
                {
                    AccountDisplayName.Text = "读不到登录态";
                    AccountUserName.Text = "点这里打开账号切换";
                    return;
                }

                AccountDisplayName.Text = String.IsNullOrWhiteSpace(user.DisplayName) ? user.Name : user.DisplayName;
                AccountUserName.Text = "@" + user.Name;

                UserDetails details = await UserDetails.Fetch(user.Id);
                string? url = details.Thumbnail?.ImageUrl;

                if (!String.IsNullOrEmpty(url))
                    AccountAvatar.Source = await LoadAvatarAsync(url!);
            }
            catch (Exception ex)
            {
                // 没登录、没网、接口挂了都走这儿。左下角显示成「读不到账号」就行，不要弹窗
                App.Logger.WriteLine(LOG_IDENT, "Failed to load current account");
                App.Logger.WriteException(LOG_IDENT, ex);

                AccountDisplayName.Text = "读不到账号";
                AccountUserName.Text = "点这里打开账号切换";
            }
        }

        /// <summary>
        /// 头像走 HttpClient 自己下，不用 BitmapImage 的 UriSource。
        /// 后者用的是 WebRequest，代理和 TLS 那套设置跟 App.HttpClient 不一致，容易下不来
        /// </summary>
        private static async Task<BitmapImage?> LoadAvatarAsync(string url)
        {
            byte[] bytes = await App.HttpClient.GetByteArrayAsync(url);

            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            return image;
        }

        public void LoadState()
        {
            if (_state.Left > SystemParameters.VirtualScreenWidth)
                _state.Left = 0;

            if (_state.Top > SystemParameters.VirtualScreenHeight)
                _state.Top = 0;

            if (_state.Width > 0)
                this.Width = _state.Width;

            if (_state.Height > 0)
                this.Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _state.Left;
                this.Top = _state.Top;
            }
        }

        private async void ShowAlreadyRunningSnackbar()
        {
            await Task.Delay(500); // wait for everything to finish loading
            AlreadyRunningSnackbar.Show();
        }

        #region INavigationWindow methods

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.FastFlags.Changed || App.PendingSettingTasks.Any())
            {
                var result = Frontend.ShowMessageBox(Strings.Menu_UnsavedChanges, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }
            
            _state.Width = this.Width;
            _state.Height = this.Height;

            _state.Top = this.Top;
            _state.Left = this.Left;

            App.State.Save();
        }

        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }
    }
}
