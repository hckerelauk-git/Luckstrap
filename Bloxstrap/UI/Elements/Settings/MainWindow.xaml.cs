using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

using Bloxstrap.Integrations;
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

            // 右上角账号卡要读登录态再打网络请求，异步做，别卡窗口
            Loaded += (_, _) => LoadCurrentAccount();

            // 换完号回到窗口要能立刻变过来，不然还显示上一个号
            Activated += (_, _) =>
            {
                string? blob = SafeReadBlob();

                if (blob != _shownBlob)
                    LoadCurrentAccount();
            };
        }

        // ── 右上角账号卡 ──────────────────────────────────────────────

        private string? _shownBlob;
        private long _avatarUserId;
        private bool _loadingAccount;
        private bool _reloadPending;

        private void AccountCard_Click(object sender, MouseButtonEventArgs e)
            => RootNavigation.Navigate(typeof(AccountsPage));

        /// <summary>换完号让账号卡立刻重读，不用等窗口重新激活</summary>
        public void RefreshAccount()
        {
            _shownBlob = null;
            _avatarUserId = 0;

            // 旧号的头像先清掉，不然换号之后还挂着上一个号的脸
            AccountAvatarBrush.ImageSource = null;

            LoadCurrentAccount();
        }

        private static string? SafeReadBlob()
        {
            try
            {
                return RobloxCookies.ReadBlob();
            }
            catch
            {
                return null;
            }
        }

        private async void LoadCurrentAccount()
        {
            const string LOG_IDENT = "MainWindow::LoadCurrentAccount";

            // 正在读的时候又要重读，记一笔，等这一轮完事再补一次。
            // 直接 return 会把「换完号刷新」这个请求吞掉，卡片就停在旧号上
            if (_loadingAccount)
            {
                _reloadPending = true;
                return;
            }

            _loadingAccount = true;

            try
            {
                string? blob = SafeReadBlob();
                _shownBlob = blob;

                // 先拿本地那份顶着。按当前 cookie 对号优先，换号之后 appStorage.json
                // 还是旧的，只有 Accounts.json 能立刻反映过来
                AccountSnapshot? account = CurrentAccount.ReadSaved(blob) ?? CurrentAccount.ReadLocal();

                if (account is not null)
                    ShowAccount(account);

                var (resolved, fromNetwork) = await CurrentAccount.ResolveAsync(blob);

                if (resolved is not null && (fromNetwork || account is null))
                {
                    account = resolved;
                    ShowAccount(account);
                }
                else if (account is null)
                {
                    // 登录态文件都没有，那就是真的没登录
                    SetAccountText(Strings.Dlg_001, Strings.Dlg_002);
                    AccountAvatarBrush.ImageSource = null;
                }

                if (account is null)
                    return;

                await LoadAvatarAsync(account.UserId, account.Title);
            }
            catch (Exception ex)
            {
                // 走到这儿说明连本地文件都读挂了。卡片保持原样，不要弹窗
                App.Logger.WriteLine(LOG_IDENT, "Failed to load current account");
                App.Logger.WriteException(LOG_IDENT, ex);

                SetAccountText(Strings.Dlg_003, Strings.Dlg_002);
            }
            finally
            {
                _loadingAccount = false;

                if (_reloadPending)
                {
                    _reloadPending = false;
                    LoadCurrentAccount();
                }
            }
        }

        private void ShowAccount(AccountSnapshot account)
        {
            SetAccountText(account.Title, "@" + account.Name);
            AccountCard.ToolTip = String.Format(Strings.Account_Tooltip, account.Title, account.Name);

            // 头像还没下来的这段时间显示首字母，不要留一块空白。
            // 已经有头像就别盖上去，圆形 PNG 的四个角是透明的，会露出来
            if (AccountAvatarBrush.ImageSource is null)
            {
                AccountInitial.Text = String.IsNullOrWhiteSpace(account.Title)
                    ? "?"
                    : account.Title.Substring(0, 1).ToUpperInvariant();
                AccountInitial.Visibility = Visibility.Visible;
            }
        }

        private void SetAccountText(string title, string subtitle)
        {
            AccountDisplayName.Text = title;
            AccountUserName.Text = subtitle;
        }

        /// <summary>
        /// 头像单独一条路，失败不影响名字。取不到就画个首字母，
        /// 至少不会是一块空白
        /// </summary>
        private async Task LoadAvatarAsync(long userId, string displayName)
        {
            if (userId == 0 || userId == _avatarUserId)
                return;

            try
            {
                string url = $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=true";

                // 新号第一次取头像时接口会先返回 Pending，过一会儿才有图。给它一次重试
                string? imageUrl = null;

                for (int attempt = 0; attempt < 2 && String.IsNullOrEmpty(imageUrl); attempt++)
                {
                    if (attempt > 0)
                        await Task.Delay(700);

                    var response = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(url);
                    imageUrl = response?.Data?.FirstOrDefault()?.ImageUrl;
                }

                if (String.IsNullOrEmpty(imageUrl))
                {
                    App.Logger.WriteLine("MainWindow::LoadAvatar", $"接口没给图，userId={userId}");
                    return;
                }

                BitmapImage? image = await LoadAvatarAsync(imageUrl!);

                if (image is null)
                    return;

                AccountAvatarBrush.ImageSource = image;
                AccountInitial.Visibility = Visibility.Collapsed;
                _avatarUserId = userId;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("MainWindow::LoadAvatar", $"头像下载失败，改用首字母：{ex.Message}");

                // 没头像就画个圈里带首字母的，别留白
                AccountAvatarBrush.ImageSource = null;
                AccountInitial.Text = String.IsNullOrWhiteSpace(displayName)
                    ? "?"
                    : displayName.Substring(0, 1).ToUpperInvariant();
                AccountInitial.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 头像走 HttpClient 自己下，不用 BitmapImage 的 UriSource。
        /// 后者用的是 WebRequest，代理和 TLS 那套设置跟 App.HttpClient 不一致，容易下不来。
        /// 图片在 rbxcdn 上，那条链路要强制 IPv4，所以用 App.ImageHttpClient
        /// </summary>
        private static async Task<BitmapImage?> LoadAvatarAsync(string url)
        {
            try
            {
                byte[] bytes = await App.ImageHttpClient.GetByteArrayAsync(url);

                using var stream = new MemoryStream(bytes);

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();

                return image;
            }
            catch (Exception ex)
            {
                // 头像下不下来不是大事，外面会退回首字母
                App.Logger.WriteLine("MainWindow::LoadAvatar", ex.Message);
                return null;
            }
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
