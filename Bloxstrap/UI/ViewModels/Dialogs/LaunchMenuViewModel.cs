using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

using Bloxstrap.UI.Elements.About;

namespace Bloxstrap.UI.ViewModels.Installer
{
    public class LaunchMenuViewModel
    {
        public string Version => string.Format(Strings.Menu_About_Version, App.Version);

        /// <summary>
        /// 没装工作室时也要给入口。点了一样走 -studio 流程，
        /// Bootstrapper 发现本地没有版本就会把工作室装到 Luckstrap 的 Versions 目录
        /// </summary>
        public string StudioOptionText => App.IsStudioVisible
            ? Strings.LaunchMenu_LaunchRobloxStudio
            : Strings.LaunchMenu_InstallRobloxStudio;

        public string StudioOptionNote => Strings.LaunchMenu_InstallRobloxStudio_Description;

        public Visibility StudioOptionNoteVisibility => App.IsStudioVisible ? Visibility.Collapsed : Visibility.Visible;

        public ICommand LaunchSettingsCommand => new RelayCommand(LaunchSettings);

        public ICommand LaunchRobloxCommand => new RelayCommand(LaunchRoblox);

        public ICommand LaunchRobloxStudioCommand => new RelayCommand(LaunchRobloxStudio);

        public ICommand LaunchAboutCommand => new RelayCommand(LaunchAbout);

        public event EventHandler<NextAction>? CloseWindowRequest;

        private void LaunchSettings() => CloseWindowRequest?.Invoke(this, NextAction.LaunchSettings);

        private void LaunchRoblox() => CloseWindowRequest?.Invoke(this, NextAction.LaunchRoblox);

        private void LaunchRobloxStudio() => CloseWindowRequest?.Invoke(this, NextAction.LaunchRobloxStudio);

        private void LaunchAbout() => new MainWindow().ShowDialog();
    }
}
