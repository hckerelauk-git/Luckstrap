using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

// 同 FastFlagProfilesPage：不能 using Wpf.Ui.Controls，那边有个 MessageBox 会撞名
using InfoBarSeverity = Wpf.Ui.Controls.InfoBarSeverity;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>列表里的一行。IsCurrent 用来给「当前」那个小标签控制显示。</summary>
    public class AccountItem
    {
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public bool IsCurrent { get; set; }
        public SavedAccount Account { get; set; } = new();
    }

    /// <summary>
    /// 账号切换页。做法是复制 / 覆盖 Roblox 自己的登录态文件，一次只有一个账号登录。
    /// 不存密码、不多开游戏。
    /// </summary>
    public partial class AccountsPage
    {
        private readonly ObservableCollection<AccountItem> _items = new();

        private List<SavedAccount> _accounts = new();

        public AccountsPage()
        {
            InitializeComponent();
            AccountList.ItemsSource = _items;
            Loaded += (_, _) => Reload();
        }

        // ── 列表 ──────────────────────────────────────────────────────

        private void Reload(string? selectBlob = null)
        {
            string? currentBlob = RobloxCookies.ReadBlob();

            if (selectBlob is null && AccountList.SelectedItem is AccountItem previous)
                selectBlob = previous.Account.CookieBlob;

            _accounts = AccountManager.Load();
            _items.Clear();

            foreach (var account in _accounts)
            {
                _items.Add(new AccountItem
                {
                    Title = string.IsNullOrWhiteSpace(account.DisplayName) ? account.Name : account.DisplayName,
                    Subtitle = BuildSubtitle(account),
                    IsCurrent = currentBlob is not null && currentBlob == account.CookieBlob,
                    Account = account
                });
            }

            ListHeader.Text = currentBlob is null
                ? $"已存 {_accounts.Count} 个 · 读不到当前登录态"
                : $"已存 {_accounts.Count} 个";

            if (_items.Count > 0)
                AccountList.SelectedItem = _items.FirstOrDefault(i => i.Account.CookieBlob == selectBlob) ?? _items[0];
        }

        private static string BuildSubtitle(SavedAccount account)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(account.DisplayName) && !string.IsNullOrWhiteSpace(account.Name))
                parts.Add("@" + account.Name);

            if (account.UserId > 0)
                parts.Add("ID " + account.UserId);

            parts.Add("存于 " + account.SavedAt.ToString("MM-dd HH:mm"));

            return string.Join(" · ", parts);
        }

        private void OnAccountSelected(object sender, SelectionChangedEventArgs e)
        {
            if (AccountList.SelectedItem is AccountItem item)
                NameBox.Text = item.Account.Name;
        }

        // ── 操作 ──────────────────────────────────────────────────────

        private async void OnAddCurrent(object sender, RoutedEventArgs e)
        {
            string? blob = RobloxCookies.ReadBlob();

            if (blob is null)
            {
                Warn("读不到登录态", $"没找到 {RobloxCookies.FilePath}。先在游戏里正常登录一次，再回来点这个。");
                return;
            }

            if (_accounts.Any(a => a.CookieBlob == blob))
            {
                Warn("已经存过了", "当前登录的这个账号已经在列表里。");
                return;
            }

            Busy("正在问 Roblox 这是哪个号…");

            AuthenticatedUser? user = null;
            string? cookie = RobloxCookies.ReadAuthCookie(blob);

            if (cookie is not null)
                user = await AccountManager.WhoAmI(cookie);

            var account = new SavedAccount
            {
                CookieBlob = blob,
                SavedAt = DateTime.Now,
                Name = user?.Name ?? $"账号 {_accounts.Count + 1}",
                UserId = user?.Id ?? 0,
                DisplayName = user?.DisplayName ?? ""
            };

            try
            {
                _accounts.Add(account);
                AccountManager.Save(_accounts);
                Reload(blob);
            }
            catch (Exception ex)
            {
                Fail("AccountsPage::AddCurrent", ex);
                return;
            }

            if (user is null)
                Warn("存下了，但没查到是谁", "cookie 可能已经过期，或者网络不通。先按临时名字存着，右边可以改备注名。");
            else
                Info("存下了", $"「{account.DisplayName}」(@{account.Name})", InfoBarSeverity.Success);
        }

        private void OnSwitch(object sender, RoutedEventArgs e)
        {
            if (AccountList.SelectedItem is not AccountItem item)
            {
                Warn("先选一个", "在左边列表里点一下要切过去的账号。");
                return;
            }

            if (AccountManager.IsRobloxRunning())
            {
                Warn("Roblox 还开着", "先把 Roblox 完全关掉（托盘里也退掉），再切换。开着的时候写进去会被它自己覆盖回去。");
                return;
            }

            var confirm = MessageBox.Show(
                $"切换到「{item.Title}」？\n\n" +
                "会把 Roblox 现在的登录态换成这个账号，下次打开游戏就是它。\n" +
                "第一次切换时，原来那份登录态会备份到 RobloxCookies.dat.luckstrap-bak。",
                "切换账号",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                RobloxCookies.BackupOnce();
                RobloxCookies.WriteBlob(item.Account.CookieBlob);
                Reload(item.Account.CookieBlob);

                Info("切换完成", $"现在登录态是「{item.Title}」。打开游戏看一眼，如果显示未登录，说明这个存档的凭据过期了——重新登录一次，再点「用当前登录态覆盖选中」。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("AccountsPage::Switch", ex);
            }
        }

        private async void OnOverwriteSelected(object sender, RoutedEventArgs e)
        {
            if (AccountList.SelectedItem is not AccountItem item)
            {
                Warn("先选一个", "在左边列表里点一下要覆盖的账号。");
                return;
            }

            string? blob = RobloxCookies.ReadBlob();

            if (blob is null)
            {
                Warn("读不到登录态", "当前没有可用的登录态。先在游戏里登录一次。");
                return;
            }

            if (blob == item.Account.CookieBlob)
            {
                Info("不用覆盖", "选中这个账号存的凭据跟当前登录的一模一样。", InfoBarSeverity.Informational);
                return;
            }

            var confirm = MessageBox.Show(
                $"用当前登录的凭据覆盖「{item.Title}」吗？\n\n" +
                "覆盖之后这个存档就变成你现在登录的那个号了，原来那份就没了。",
                "覆盖存档",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            Busy("正在更新…");

            item.Account.CookieBlob = blob;
            item.Account.SavedAt = DateTime.Now;

            // 顺手把名字也刷新一下，免得存档名跟实际登录的号对不上
            string? cookie = RobloxCookies.ReadAuthCookie(blob);

            if (cookie is not null)
            {
                AuthenticatedUser? user = await AccountManager.WhoAmI(cookie);

                if (user is not null)
                {
                    item.Account.Name = user.Name;
                    item.Account.DisplayName = user.DisplayName;
                    item.Account.UserId = user.Id;
                }
            }

            try
            {
                AccountManager.Save(_accounts);
                Reload(blob);
                Info("覆盖完成", $"「{item.Title}」现在对应的是你刚才登录的那个号。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("AccountsPage::Overwrite", ex);
            }
        }

        private void OnRename(object sender, RoutedEventArgs e)
        {
            if (AccountList.SelectedItem is not AccountItem item)
            {
                Warn("先选一个", "在左边列表里点一下要改名的账号。");
                return;
            }

            string name = NameBox.Text.Trim();

            if (name.Length == 0)
            {
                Warn("名字不能空", "填个名字再保存。");
                return;
            }

            item.Account.Name = name;

            try
            {
                AccountManager.Save(_accounts);
                Reload(item.Account.CookieBlob);
                Info("改好了", $"备注名现在是「{name}」。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("AccountsPage::Rename", ex);
            }
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (AccountList.SelectedItem is not AccountItem item)
            {
                Warn("先选一个", "在左边列表里点一下要删的账号。");
                return;
            }

            var confirm = MessageBox.Show(
                $"从列表里删掉「{item.Title}」吗？\n\n只删这份存档，不会让你退出登录，也不影响现在游戏里的账号。",
                "删除存档",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                _accounts.RemoveAll(a => a.CookieBlob == item.Account.CookieBlob);
                AccountManager.Save(_accounts);
                Reload();
                Info("删掉了", "存档已经从列表里移走。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("AccountsPage::Delete", ex);
            }
        }

        private void OnRefresh(object sender, RoutedEventArgs e) => Reload();

        // ── 提示 ──────────────────────────────────────────────────────

        private void Busy(string message) => Info("稍等", message, InfoBarSeverity.Informational);

        private void Info(string title, string message, InfoBarSeverity severity)
        {
            StatusBar.Title = title;
            StatusBar.Message = message;
            StatusBar.Severity = severity;
            StatusBar.IsOpen = true;
        }

        private void Warn(string title, string message) => Info(title, message, InfoBarSeverity.Warning);

        private void Fail(string ident, Exception ex)
        {
            App.Logger.WriteLine(ident, ex.ToString());
            Info("出错了", ex.Message, InfoBarSeverity.Error);
        }
    }
}
