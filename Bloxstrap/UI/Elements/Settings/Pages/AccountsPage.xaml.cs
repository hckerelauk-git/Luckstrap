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
                ? String.Format(Strings.DlgF_SavedNoLogin, _accounts.Count)
                : String.Format(Strings.DlgF_SavedCount, _accounts.Count);

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

            parts.Add(Strings.Dlg_004 + account.SavedAt.ToString("MM-dd HH:mm"));

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
                Warn(Strings.Dlg_005, String.Format(Strings.DlgF_CookieMissing, RobloxCookies.FilePath));
                return;
            }

            if (_accounts.Any(a => a.CookieBlob == blob))
            {
                Warn(Strings.Dlg_006, Strings.Dlg_007);
                return;
            }

            Busy(Strings.Dlg_008);

            AuthenticatedUser? user = null;
            string? cookie = RobloxCookies.ReadAuthCookie(blob);

            if (cookie is not null)
                user = await AccountManager.WhoAmI(cookie);

            var account = new SavedAccount
            {
                CookieBlob = blob,
                SavedAt = DateTime.Now,
                Name = user?.Name ?? String.Format(Strings.DlgF_AccountN, _accounts.Count + 1),
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
                Warn(Strings.Dlg_009, Strings.Dlg_010);
            else
                Info(Strings.Dlg_011, $"「{account.DisplayName}」(@{account.Name})", InfoBarSeverity.Success);
        }

        private void OnSwitch(object sender, RoutedEventArgs e)
        {
            if (AccountList.SelectedItem is not AccountItem item)
            {
                Warn(Strings.Dlg_012, Strings.Dlg_013);
                return;
            }

            if (AccountManager.IsRobloxRunning())
            {
                Warn(Strings.Dlg_014, Strings.Dlg_015);
                return;
            }

            var confirm = MessageBox.Show(
                String.Format(Strings.DlgF_SwitchConfirm, item.Title, Strings.Dlg_016),
                Strings.Dlg_017,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                RobloxCookies.BackupOnce();
                RobloxCookies.WriteBlob(item.Account.CookieBlob);
                Reload(item.Account.CookieBlob);

                // 右上角那张卡跟着换
                (Window.GetWindow(this) as Bloxstrap.UI.Elements.Settings.MainWindow)?.RefreshAccount();

                Info(Strings.Dlg_018, String.Format(Strings.DlgF_Switched, item.Title), InfoBarSeverity.Success);
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
                Warn(Strings.Dlg_012, Strings.Dlg_019);
                return;
            }

            string? blob = RobloxCookies.ReadBlob();

            if (blob is null)
            {
                Warn(Strings.Dlg_005, Strings.Dlg_020);
                return;
            }

            if (blob == item.Account.CookieBlob)
            {
                Info(Strings.Dlg_021, Strings.Dlg_022, InfoBarSeverity.Informational);
                return;
            }

            var confirm = MessageBox.Show(
                String.Format(Strings.DlgF_OverwriteConfirm, item.Title, Strings.Dlg_023),
                Strings.Dlg_024,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            Busy(Strings.Dlg_025);

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
                Info(Strings.Dlg_026, String.Format(Strings.DlgF_Overwritten, item.Title), InfoBarSeverity.Success);
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
                Warn(Strings.Dlg_012, Strings.Dlg_027);
                return;
            }

            string name = NameBox.Text.Trim();

            if (name.Length == 0)
            {
                Warn(Strings.Dlg_028, Strings.Dlg_029);
                return;
            }

            item.Account.Name = name;

            try
            {
                AccountManager.Save(_accounts);
                Reload(item.Account.CookieBlob);
                Info(Strings.Dlg_030, String.Format(Strings.DlgF_Renamed, name), InfoBarSeverity.Success);
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
                Warn(Strings.Dlg_012, Strings.Dlg_031);
                return;
            }

            var confirm = MessageBox.Show(
                String.Format(Strings.DlgF_DeleteConfirm, item.Title),
                Strings.Dlg_032,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                _accounts.RemoveAll(a => a.CookieBlob == item.Account.CookieBlob);
                AccountManager.Save(_accounts);
                Reload();
                Info(Strings.Dlg_033, Strings.Dlg_034, InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("AccountsPage::Delete", ex);
            }
        }

        private void OnRefresh(object sender, RoutedEventArgs e) => Reload();

        // ── 提示 ──────────────────────────────────────────────────────

        private void Busy(string message) => Info(Strings.Dlg_035, message, InfoBarSeverity.Informational);

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
            Info(Strings.Dlg_036, ex.Message, InfoBarSeverity.Error);
        }
    }
}
