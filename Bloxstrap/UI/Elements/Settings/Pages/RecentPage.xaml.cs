using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Bloxstrap.Integrations;
using Bloxstrap.Models.Entities;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>列表里的一条。只留显示要用的，原始数据在 ActivityData 里</summary>
    public class RecentItem
    {
        public string GameName { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public ICommand RejoinCommand { get; set; } = null!;
    }

    /// <summary>
    /// 最近玩过。数据就是 ServerHistoryStore 落盘的那份，跟托盘右键的「游戏历史」是同一个来源，
    /// 区别是这个页面能一次看全，不用先右键托盘
    /// </summary>
    public partial class RecentPage
    {
        public RecentPage()
        {
            InitializeComponent();

            Loaded += async (_, _) => await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            var history = ServerHistoryStore.Load();

            if (history.Count == 0)
            {
                HistoryList.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                return;
            }

            EmptyText.Visibility = Visibility.Collapsed;

            // 封面和游戏名不在历史文件里，要补。补失败就算了，下面会用 PlaceId 兜底
            var missing = history
                .Where(x => x.UniverseDetails is null && x.UniverseId != 0)
                .Select(x => x.UniverseId)
                .Distinct()
                .ToList();

            if (missing.Count > 0)
            {
                try
                {
                    await UniverseDetails.FetchBulk(String.Join(',', missing));
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("RecentPage::Reload", "Failed to fetch universe details");
                    App.Logger.WriteException("RecentPage::Reload", ex);
                }
            }

            var items = new List<RecentItem>();

            foreach (var entry in history)
            {
                UniverseDetails? details = entry.UniverseDetails ?? UniverseDetails.LoadFromCache(entry.UniverseId);

                items.Add(new RecentItem
                {
                    GameName = details?.Data?.Name ?? $"PlaceId {entry.PlaceId}",
                    ThumbnailUrl = details?.Thumbnail?.ImageUrl ?? String.Empty,
                    Subtitle = BuildSubtitle(entry),
                    RejoinCommand = entry.RejoinServerCommand
                });
            }

            HistoryList.ItemsSource = items;
        }

        private static string BuildSubtitle(ActivityData entry)
        {
            var parts = new List<string>();

            parts.Add(entry.ServerType switch
            {
                ServerType.Private => "私服",
                ServerType.Reserved => "预留服",
                _ => "公开服"
            });

            if (!String.IsNullOrEmpty(entry.JobId))
                parts.Add(entry.JobId[..Math.Min(8, entry.JobId.Length)]);

            parts.Add(entry.TimeJoined.ToString("MM-dd HH:mm"));

            if (entry.TimeLeft is DateTime left && left > entry.TimeJoined)
            {
                TimeSpan span = left - entry.TimeJoined;

                parts.Add(span.TotalHours >= 1
                    ? $"待了 {(int)span.TotalHours} 小时 {span.Minutes} 分"
                    : $"待了 {Math.Max(1, (int)span.TotalMinutes)} 分钟");
            }

            return String.Join(" · ", parts);
        }

        private async void OnRefresh(object sender, RoutedEventArgs e) => await ReloadAsync();

        private void OnClear(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "清空之后这些记录就没了，想回上一个服得自己找。确定吗？",
                "清空最近玩过",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                if (File.Exists(ServerHistoryStore.FileLocation))
                    File.Delete(ServerHistoryStore.FileLocation);

                // 内存里那份也要清，不然下次退游戏保存时又写回来了
                ActivityWatcher.Current?.History.Clear();

                HistoryList.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("RecentPage::OnClear", "Failed to clear history");
                App.Logger.WriteException("RecentPage::OnClear", ex);
            }
        }
    }
}
