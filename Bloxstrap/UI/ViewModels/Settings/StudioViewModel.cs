namespace Bloxstrap.UI.ViewModels.Settings
{
    public class StudioViewModel : NotifyPropertyChangedViewModel
    {
        /// <summary>
        /// 从 Luckstrap 启动工作室时是否先走更新检查。
        /// 开关放在 OptionControl 里，那里的元素拿不到 x:Name，只能走绑定
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
    }
}
