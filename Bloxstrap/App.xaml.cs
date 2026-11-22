using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;

using Microsoft.Win32;

namespace Bloxstrap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if QA_BUILD
        public const string ProjectName = "Luckstrap QA";
#else
        public const string ProjectName = "Luckstrap";
#endif
        public const string ProjectOwner = "Luckstrap";

        // 我们自己的仓库
        public const string ProjectRepository = "hckerelauk-git/Luckstrap";

        // 上游仓库。排错用的 wiki 还在那边，内容对 Luckstrap 同样适用，所以帮助链接继续指过去
        public const string UpstreamRepository = "bloxstraplabs/bloxstrap";

        // 自更新查的仓库，跟 ProjectRepository 是同一个。单独起个名字是为了让调用点读起来知道用途
        public const string UpdateRepository = ProjectRepository;

        public const string ProjectDownloadLink = "https://github.com/hckerelauk-git/Luckstrap/releases";
        public const string ProjectHelpLink = "https://github.com/hckerelauk-git/Luckstrap#readme";
        public const string ProjectSupportLink = "https://github.com/hckerelauk-git/Luckstrap/issues/new";

        /// <summary>
        /// GitHub 国内直连基本是几 KB/s，检查更新和下载更新包都先过镜像。
        /// 这些就是 Edge 上「GitHub 下载加速」那类扩展背后用的站，用法是在原地址前面拼前缀。
        /// 按顺序试，全挂了再回退直连。
        /// </summary>
        public static readonly string[] GithubMirrors = new[]
        {
            "https://ghproxy.net/",
            "https://ghfast.top/",
            // gh-proxy 现在对页面地址返回它自己的落地页，里面没有 release 信息，
            // 二进制也一律 403，排最后当兜底
            "https://gh-proxy.com/"
        };

        /// <summary>
        /// 下载 exe 用的是 /releases/download/ 路径，跟检查更新打的 release 页面不是一回事。
        /// gh-proxy 对 /releases/download/ 直接返回 403（拿别的仓库试过也一样，不是我们仓库的问题），
        /// 所以二进制单独排一遍顺序，把确认能代理大文件的放前面。
        /// </summary>
        public static readonly string[] GithubDownloadMirrors = new[]
        {
            "https://ghproxy.net/",
            "https://ghfast.top/",
            "https://gh-proxy.com/"
        };

        // 数据目录名，跟原版 Bloxstrap 隔离开，避免互相覆盖配置
        public const string ProjectDataFolder = "Luckstrap";

        public const string RobloxPlayerAppName = "RobloxPlayerBeta";
        public const string RobloxStudioAppName = "RobloxStudioBeta";

        // simple shorthand for extremely frequently used and long string - this goes under HKCU
        public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";

        public static LaunchSettings LaunchSettings { get; private set; } = null!;

        public static BuildMetadataAttribute BuildMetadata = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;

        public static string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString()[..^2];

        public static Bootstrapper? Bootstrapper { get; set; } = null!;

        public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);

        public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);

        public static bool IsStudioVisible => !String.IsNullOrEmpty(App.RobloxState.Prop.Studio.VersionGuid);

        public static readonly MD5 MD5Provider = MD5.Create();

        public static readonly Logger Logger = new();

        public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

        public static readonly JsonManager<Settings> Settings = new();

        public static readonly JsonManager<State> State = new();

        public static readonly JsonManager<RobloxState> RobloxState = new();

        public static readonly FastFlagManager FastFlags = new();

        public static readonly HttpClient HttpClient = new(
            new HttpClientLoggingHandler(
                new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,

                    // 用系统默认协议版本时，客户端可能去试 TLS 1.3 或者 HTTP/2，
                    // 碰上中间设备不支持就是「Received an unexpected EOF or 0 bytes from the transport stream」。
                    // 这里把协议钉死在 TLS 1.2/1.3，请求走 HTTP/1.1，握手就不会再协商出奇怪的结果
                    SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }
            )
        )
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

        private static bool _showingExceptionDialog = false;

        public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::Terminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Environment.Exit(exitCodeNum);
        }

        public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::SoftTerminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Current.Dispatcher.Invoke(() => Current.Shutdown(exitCodeNum));
        }

        void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Logger.WriteLine("App::GlobalExceptionHandler", "An exception occurred");

            // 网络类的异常不该把整个程序退掉。以前 ipinfo.io 连不上都会弹框退出，
            // 表现就是「一进游戏就报 SSL 错误」。这类问题记日志，让程序继续跑
            if (e.Exception is HttpRequestException
                or TaskCanceledException
                or OperationCanceledException
                or System.Net.Sockets.SocketException)
            {
                Logger.WriteLine("App::GlobalExceptionHandler", "Network failure, logging and continuing");
                Logger.WriteException("App::GlobalExceptionHandler", e.Exception);
                return;
            }

            FinalizeExceptionHandling(e.Exception);
        }

        public static void FinalizeExceptionHandling(AggregateException ex)
        {
            foreach (var innerEx in ex.InnerExceptions)
                Logger.WriteException("App::FinalizeExceptionHandling", innerEx);

            FinalizeExceptionHandling(ex.GetBaseException(), false);
        }

        public static void FinalizeExceptionHandling(Exception ex, bool log = true)
        {
            if (log)
                Logger.WriteException("App::FinalizeExceptionHandling", ex);

            if (_showingExceptionDialog)
                return;

            _showingExceptionDialog = true;

            SendLog();

            if (Bootstrapper?.Dialog != null)
            {
                if (Bootstrapper.Dialog.TaskbarProgressValue == 0)
                    Bootstrapper.Dialog.TaskbarProgressValue = 1; // make sure it's visible

                Bootstrapper.Dialog.TaskbarProgressState = TaskbarItemProgressState.Error;
            }

            Frontend.ShowExceptionDialog(ex);

            Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
        }

        public static async Task<GithubRelease?> GetLatestRelease()
        {
            const string LOG_IDENT = "App::GetLatestRelease";

            // api.github.com 那些镜像站不代理，只能打 releases/latest，
            // 让它跳到 /releases/tag/vX.Y.Z，再从页面里把 tag 抠出来。
            // 镜像按顺序试，最后才直连 —— 国内直连这一步基本会超时
            var sources = new List<string>(GithubMirrors) { "" };

            foreach (string prefix in sources)
            {
                string source = String.IsNullOrEmpty(prefix) ? "direct" : prefix;
                string url = $"{prefix}https://github.com/{UpdateRepository}/releases/latest";

                try
                {
                    // 每个源只等 12 秒。这里读的是一张几十 KB 的网页，
                    // 正常镜像一秒内就回；超过 12 秒基本可以判定这个源挂了，
                    // 没必要为了它把 HttpClient 那 30 秒耗光，四个源排下来就是两分钟
                    using var sourceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));

                    using var response = await HttpClient.GetAsync(url, sourceTimeout.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.WriteLine(LOG_IDENT, $"{source} returned {(int)response.StatusCode}, trying next");
                        continue;
                    }

                    // 先看最终落在哪个地址。releases/latest 正常会 302 到 /releases/tag/vX.Y.Z，
                    // 镜像代理也是这个行为。比抠页面内容靠谱：release 页面是前端渲染的，
                    // 而且 gh-proxy 现在对页面地址直接返回它自己的落地页，里面根本没有 tag
                    string? tag = ExtractTagFromUri(response.RequestMessage?.RequestUri);

                    if (tag is null)
                    {
                        string html = await response.Content.ReadAsStringAsync(sourceTimeout.Token);
                        tag = ExtractLatestReleaseTag(html);
                    }

                    if (tag is null)
                    {
                        // 仓库还没有任何 release 时就是这个情况，页面里没有 tag 链接
                        Logger.WriteLine(LOG_IDENT, $"{source} page has no release tag, trying next");
                        continue;
                    }

                    Logger.WriteLine(LOG_IDENT, $"Latest release is {tag} (via {source})");

                    return new GithubRelease
                    {
                        TagName = tag,
                        Name = $"Luckstrap {tag}",
                        Body = String.Empty,
                        CreatedAt = String.Empty,
                        Assets = new List<GithubReleaseAsset>
                        {
                            new()
                            {
                                Name = "Luckstrap.exe",
                                BrowserDownloadUrl = $"https://github.com/{UpdateRepository}/releases/download/{tag}/Luckstrap.exe"
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LOG_IDENT, $"{source} failed, trying next");
                    Logger.WriteException(LOG_IDENT, ex);
                }
            }

            Logger.WriteLine(LOG_IDENT, "No release source reachable");
            return null;
        }

        /// <summary>
        /// 从 release 页面 HTML 里抠最新 tag。GitHub 的 tag 链接格式很固定，第一个就是最新的
        /// </summary>
        private static string? ExtractLatestReleaseTag(string html)
        {
            Match match = Regex.Match(html, @"/releases/tag/(v?\d[\w.\-]*)");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// 从跳转后的最终地址里取 tag。releases/latest 会 302 到 releases/tag/vX.Y.Z，
        /// 所以地址本身就带着版本号，不用管页面长什么样
        /// </summary>
        private static string? ExtractTagFromUri(Uri? uri)
        {
            if (uri is null)
                return null;

            Match match = Regex.Match(uri.AbsoluteUri, @"/releases/tag/(v?\d[\w.\-]*)");
            return match.Success ? match.Groups[1].Value : null;
        }

        // 上游的统计服务架在 bloxstraplabs.com 上，我们没自己的收集端。
        // 与其把用户数据往别人的服务器送，不如干脆不发，只写本地日志。
        // 方法签名保持原样，调用点不用动。
        public static void SendStat(string key, string value)
        {
            Logger.WriteLine("App::SendStat", $"{key}={value} (analytics disabled, not sent)");
        }

        public static void SendLog()
        {
            Logger.WriteLine("App::SendLog", "analytics disabled, log not sent");
        }

        public static void AssertWindowsOSVersion()
        {
            const string LOG_IDENT = "App::AssertWindowsOSVersion";

            int major = Environment.OSVersion.Version.Major;
            if (major < 10) // Windows 10 and newer only
            {
                Logger.WriteLine(LOG_IDENT, $"Detected unsupported Windows version ({Environment.OSVersion.Version}).");

                if (!LaunchSettings.QuietFlag.Active)
                    Frontend.ShowMessageBox(Strings.App_OSDeprecation_Win7_81, MessageBoxImage.Error);

                Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string LOG_IDENT = "App::OnStartup";

            Locale.Initialize();

            base.OnStartup(e);

            Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");

            string userAgent = $"{ProjectName}/{Version}";

            if (IsActionBuild)
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef})");

                if (IsProductionBuild)
                    userAgent += $" (Production)";
                else
                    userAgent += $" (Artifact {BuildMetadata.CommitHash}, {BuildMetadata.CommitRef})";
            }
            else
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from {BuildMetadata.Machine}");

#if QA_BUILD
                userAgent += " (QA)";
#else
                userAgent += $" (Build {Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))})";
#endif
            }

            Logger.WriteLine(LOG_IDENT, $"OSVersion: {Environment.OSVersion}");

            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");
            Logger.WriteLine(LOG_IDENT, $"Temp path is {Paths.Temp}");
            Logger.WriteLine(LOG_IDENT, $"WindowsStartMenu path is {Paths.WindowsStartMenu}");

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            HttpClient.Timeout = TimeSpan.FromSeconds(30);
            HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            LaunchSettings = new LaunchSettings(e.Args);

            // installation check begins here
            using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
            string? installLocation = null;
            bool fixInstallLocation = false;
            
            if (uninstallKey?.GetValue("InstallLocation") is string value)
            {
                if (Directory.Exists(value))
                {
                    installLocation = value;
                }
                else
                {
                    // check if user profile folder has been renamed
                    var match = Regex.Match(value, @"^[a-zA-Z]:\\Users\\([^\\]+)", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        string newLocation = value.Replace(match.Value, Paths.UserProfile, StringComparison.InvariantCultureIgnoreCase);

                        if (Directory.Exists(newLocation))
                        {
                            installLocation = newLocation;
                            fixInstallLocation = true;
                        }
                    }
                }
            }

            // silently change install location if we detect a portable run
            if (installLocation is null && Directory.GetParent(Paths.Process)?.FullName is string processDir)
            {
                var files = Directory.GetFiles(processDir).Select(x => Path.GetFileName(x)).ToArray();

                // check if settings.json and state.json are the only files in the folder
                if (files.Length <= 3 && files.Contains("Settings.json") && files.Contains("State.json"))
                {
                    installLocation = processDir;
                    fixInstallLocation = true;
                }
            }

            if (fixInstallLocation && installLocation is not null)
            {
                var installer = new Installer
                {
                    InstallLocation = installLocation,
                    IsImplicitInstall = true
                };

                if (installer.CheckInstallLocation())
                {
                    Logger.WriteLine(LOG_IDENT, $"Changing install location to '{installLocation}'");
                    installer.DoInstall();
                }
                else
                {
                    // force reinstall
                    installLocation = null;
                }
            }

            if (installLocation is null)
            {
                Logger.Initialize(true);
                Logger.WriteLine(LOG_IDENT, "Not installed, launching the installer");
                AssertWindowsOSVersion(); // prevent new installs from unsupported operating systems
                LaunchHandler.LaunchInstaller();
            }
            else
            {
                Paths.Initialize(installLocation);

                Logger.WriteLine(LOG_IDENT, "Entering main logic");

                // ensure executable is in the install directory
                if (Paths.Process != Paths.Application && !File.Exists(Paths.Application))
                {
                    Logger.WriteLine(LOG_IDENT, "Copying to install directory");
                    File.Copy(Paths.Process, Paths.Application);
                }

                Logger.Initialize(LaunchSettings.UninstallFlag.Active);

                if (!Logger.Initialized && !Logger.NoWriteMode)
                {
                    Logger.WriteLine(LOG_IDENT, "Possible duplicate launch detected, terminating.");
                    Terminate();
                }

                Settings.Load();
                State.Load();
                RobloxState.Load();
                FastFlags.Load();

                if (!Locale.SupportedLocales.ContainsKey(Settings.Prop.Locale))
                {
                    Settings.Prop.Locale = "nil";
                    Settings.Save();
                }

                Logger.WriteLine(LOG_IDENT, $"Developer mode: {Settings.Prop.DeveloperMode}");
                Logger.WriteLine(LOG_IDENT, $"Web environment: {Settings.Prop.WebEnvironment}");

                Locale.Set(Settings.Prop.Locale);

                if (!LaunchSettings.BypassUpdateCheck)
                    Installer.HandleUpgrade();

                LaunchHandler.ProcessLaunchArgs();
            }

            // you must *explicitly* call terminate when everything is done, it won't be called implicitly
            Logger.WriteLine(LOG_IDENT, "Startup finished");
        }
    }
}
