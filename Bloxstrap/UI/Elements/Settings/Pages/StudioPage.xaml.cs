using System;
using System.Diagnostics;
using System.Windows;
using Bloxstrap.AppData;
using Bloxstrap.UI.ViewModels.Settings;
using Wpf.Ui.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// 工作室页：显示 Roblox Studio 的安装状态，从这里直接启动，
    /// 并且提供只作用于工作室的启动设置。
    /// </summary>
    public partial class StudioPage
    {
        public StudioPage()
        {
            var viewModel = new StudioViewModel();

            // 选项行的按钮走命令绑定，结果从事件回过来，统一由页面弹 InfoBar
            viewModel.StatusRequest += (_, status) =>
                ShowStatus(status.Title, status.Message, status.Error ? InfoBarSeverity.Error : InfoBarSeverity.Success);

            DataContext = viewModel;
            InitializeComponent();

            Loaded += (_, _) => RefreshState();
        }

        private void RefreshState()
        {
            bool installed = App.IsStudioVisible;

            if (installed)
            {
                StateText.Text = Strings.Studio_Installed;
                PathText.Text = new RobloxStudioData().Directory;
                PathText.Visibility = Visibility.Visible;
                LaunchButton.Content = Strings.Studio_Launch;
            }
            else
            {
                // 没装也能点：走 -studio 流程，Bootstrapper 会把工作室装到 Paths.Versions
                StateText.Text = Strings.Studio_Missing_Installable;
                PathText.Text = Paths.Versions;
                PathText.Visibility = Visibility.Visible;
                LaunchButton.Content = Strings.Studio_Install;
            }

            LaunchButton.IsEnabled = true;
            FolderButton.IsEnabled = installed;
        }

        private void OnLaunch(object sender, RoutedEventArgs e)
        {
            try
            {
                ProcessStartInfo psi;

                // 没装的时候必须走 Luckstrap，不然没有可执行文件可以拉起来
                if (!App.IsStudioVisible || App.Settings.Prop.StudioCheckForUpdates)
                {
                    // 走完整流程：更新检查、安装、FastFlags、模组都由 Luckstrap 处理。
                    // 拉新进程而不是在设置窗口里直接启动，免得把设置窗口一起关掉
                    psi = new ProcessStartInfo
                    {
                        FileName = Paths.Process,
                        Arguments = "-studio",
                        UseShellExecute = true
                    };
                }
                else
                {
                    // 跳过更新和模组注入，直接拉起本地已安装的那份
                    psi = new ProcessStartInfo
                    {
                        FileName = new RobloxStudioData().ExecutablePath,
                        UseShellExecute = true
                    };
                }

                App.Logger.WriteLine("StudioPage::OnLaunch", $"Launching studio, check updates = {App.Settings.Prop.StudioCheckForUpdates}");

                Process.Start(psi);

                ShowStatus(Strings.Studio_Launched, "", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("StudioPage::OnLaunch", "Failed to launch studio");
                App.Logger.WriteException("StudioPage::OnLaunch", ex);

                ShowStatus(Strings.Studio_LaunchFailed, ex.Message, InfoBarSeverity.Error);
            }
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            if (!App.IsStudioVisible)
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{new RobloxStudioData().Directory}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("StudioPage::OnOpenFolder", ex);

                ShowStatus(Strings.Studio_FolderFailed, ex.Message, InfoBarSeverity.Error);
            }
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
