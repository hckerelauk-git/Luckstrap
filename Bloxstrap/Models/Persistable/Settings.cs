using System.Collections.ObjectModel;

namespace Bloxstrap.Models.Persistable
{
    public class Settings
    {
        // bloxstrap configuration
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.FluentDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconBloxstrap;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public Theme Theme { get; set; } = Theme.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DeveloperMode { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        public bool ConfirmLaunches { get; set; } = false;
        public string Locale { get; set; } = "nil";
        public bool ForceRobloxLanguage { get; set; } = false;
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool EnableAnalytics { get; set; } = true;
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
        public string? SelectedCustomTheme { get; set; } = null;
        public WebEnvironment WebEnvironment { get; set; } = WebEnvironment.Production;

        // integration configuration
        public bool EnableActivityTracking { get; set; } = true;
        public bool UseDiscordRichPresence { get; set; } = true;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;

        /// <summary>
        /// 网页版 Discord 的状态桥（实验性）。开了会在 127.0.0.1:1337 起一个 arRPC 兼容的
        /// WebSocket，浏览器那边装了 arRPC 客户端插件才能看到状态。默认关，因为要额外装插件，
        /// 而且端口可能被 arRPC 本体占掉
        /// </summary>
        public bool UseArRpcBridge { get; set; } = false;

        /// <summary>
        /// 进服务器时弹一条通知，告诉你连到哪个机房了。
        /// 默认开：这个功能的全部意义就是自动告诉你，默认关掉的话没人会去翻设置
        /// </summary>
        public bool ShowServerDetails { get; set; } = true;
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // mod preset configuration
        public bool UseDisableAppPatch { get; set; } = false;

        /// <summary>
        /// 浏览器里点 Play 时，是不是由 Luckstrap 接住那个 roblox-player 链接。
        /// 关掉就把协议还给 Roblox 官方 launcher，代价是 FastFlags 和模组不会生效
        /// </summary>
        public bool InterceptBrowserLaunches { get; set; } = true;
    }
}
