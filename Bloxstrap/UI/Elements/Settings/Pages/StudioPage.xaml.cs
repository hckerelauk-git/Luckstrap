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
            DataContext = new StudioViewModel();
            InitializeComponent();

            Loaded += (_, _) => RefreshState();
        }

        private void RefreshState()
        {
            bool installed = App.IsStudioVisible;

            StateText.Text = installed ? Strings.Studio_Installed : Strings.Studio_Missing;

            if (installed)
            {
                PathText.Text = new RobloxStudioData().Directory;
                PathText.Visibility = Visibility.Visible;
            }
            else
            {
                PathText.Text = "";
                PathText.Visibility = Visibility.Collapsed;
            }

            LaunchButton.IsEnabled = installed;
            FolderButton.IsEnabled = installed;
        }

        private void OnLaunch(object sender, RoutedEventArgs e)
        {
            if (!App.IsStudioVisible)
            {
                ShowStatus(Strings.Studio_LaunchFailed, Strings.Studio_Missing, InfoBarSeverity.Warning);
                return;
            }

            try
            {
                ProcessStartInfo psi;

                if (App.Settings.Prop.StudioCheckForUpdates)
                {
                    // 走完整流程：更新检查、FastFlags、模组都由 Luckstrap 处理。
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
