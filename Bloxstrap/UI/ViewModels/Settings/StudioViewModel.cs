using System.Windows.Input;
using Bloxstrap.Utility;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class StudioViewModel : NotifyPropertyChangedViewModel
    {
        /// <summary>
        /// 选项行里的按钮在 OptionControl 里，那边的元素拿不到 x:Name，
        /// 只能走命令绑定。结果通过这个事件回给页面去弹 InfoBar
        /// </summary>
        public event EventHandler<(string Title, string Message, bool Error)>? StatusRequest;

        /// <summary>
        /// 从 Luckstrap 启动工作室时是否先走更新检查
        /// </summary>
        public bool CheckForUpdates
        {
            get => App.Settings.Prop.StudioCheckForUpdates;
            set
            {
                App.Settings.Prop.StudioCheckForUpdates = value;
                OnPropertyChanged(nameof(CheckForUpdates));
            }
        }

        public ICommand OpenLocalFileCommand => new RelayCommand(OpenLocalFile);

        public ICommand CreateLocalShortcutCommand => new RelayCommand(CreateLocalShortcut);

        private void OpenLocalFile()
        {
            string? path = PickPlaceFile();

            if (path is null)
                return;

            try
            {
                // ParseStudio 认不出 roblox-studio: 开头就当本地工程文件，
                // 拼成 -task EditFile -localPlaceFile "路径"
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = $"-studio \"{path}\"",
                    UseShellExecute = true
                });

                Report(Strings.Studio_Launched, Path.GetFileName(path), false);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("StudioViewModel::OpenLocalFile", ex);
                Report(Strings.Studio_LaunchFailed, ex.Message, true);
            }
        }

        private void CreateLocalShortcut()
        {
            string? path = PickPlaceFile();

            if (path is null)
                return;

            string name = GameShortcut.SanitizeName(Path.GetFileNameWithoutExtension(path));
            string lnk = Path.Combine(Paths.Desktop, $"{name}.lnk");

            try
            {
                if (File.Exists(lnk))
                {
                    Report(Strings.Studio_ShortcutExists, lnk, false);
                    return;
                }

                Shortcut.Create(Paths.Process, $"-studio \"{path}\"", lnk);

                if (!File.Exists(lnk))
                {
                    Report(Strings.Studio_ShortcutFailed, lnk, true);
                    return;
                }

                Report(Strings.Studio_ShortcutCreated, lnk, false);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("StudioViewModel::CreateLocalShortcut", ex);
                Report(Strings.Studio_ShortcutFailed, ex.Message, true);
            }
        }

        private static string? PickPlaceFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = Strings.Studio_PickFile,
                Filter = "Roblox place (*.rbxl;*.rbxlx)|*.rbxl;*.rbxlx|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private void Report(string title, string message, bool error)
            => StatusRequest?.Invoke(this, (title, message, error));
    }
}
