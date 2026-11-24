namespace Bloxstrap.UI.ViewModels.Installer
{
    public class WelcomeViewModel : NotifyPropertyChangedViewModel
    {
        // formatting is done here instead of in xaml, it's just a bit easier
        public string MainText => String.Format(
            Strings.Installer_Welcome_MainText,
            "[github.com/hckerelauk-git/Luckstrap](https://github.com/hckerelauk-git/Luckstrap)",
            "[github.com/hckerelauk-git/Luckstrap/releases](https://github.com/hckerelauk-git/Luckstrap/releases)"
        );

        public string VersionNotice { get; private set; } = "";

        public bool CanContinue { get; set; } = false;

        public event EventHandler? CanContinueEvent;

        // called by codebehind on page load
        public async void DoChecks()
        {
            const string LOG_IDENT = "WelcomeViewModel::DoChecks";

            // 拉不到 release 就跳过版本提示，不能因此把安装向导卡住
            try
            {
                var releaseInfo = await App.GetLatestRelease();

                if (releaseInfo is not null)
                {
                    if (Utilities.CompareVersions(App.Version, releaseInfo.TagName) == VersionComparison.LessThan)
                    {
                        VersionNotice = String.Format(Strings.Installer_Welcome_UpdateNotice, App.Version, releaseInfo.TagName.Replace("v", ""));
                        OnPropertyChanged(nameof(VersionNotice));
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Could not check for updates");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            CanContinue = true;
            OnPropertyChanged(nameof(CanContinue));

            CanContinueEvent?.Invoke(this, new EventArgs());
        }
    }
}
