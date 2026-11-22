using System.Windows;
using System.Windows.Input;
using Bloxstrap.Integrations;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.ContextMenu
{
    internal class ServerInformationViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher _activityWatcher;

        public string InstanceId => _activityWatcher.Data.JobId;

        public string ServerType => _activityWatcher.Data.ServerType.ToTranslatedString();

        public string ServerLocation { get; private set; } = Strings.Common_Loading;

        public Visibility ServerLocationVisibility => App.Settings.Prop.ShowServerDetails ? Visibility.Visible : Visibility.Collapsed;

        public ICommand CopyInstanceIdCommand => new RelayCommand(CopyInstanceId);

        public ServerInformationViewModel(Watcher watcher)
        {
            _activityWatcher = watcher.ActivityWatcher!;

            if (ServerLocationVisibility == Visibility.Visible)
                QueryServerLocation();
        }

        public async void QueryServerLocation()
        {
            // async void，异常会打到 UI 线程把程序干掉，必须自己接住
            string? location = null;

            try
            {
                location = await _activityWatcher.Data.QueryServerLocation();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("ServerInformationViewModel::QueryServerLocation", "Location query failed");
                App.Logger.WriteException("ServerInformationViewModel::QueryServerLocation", ex);
            }

            if (String.IsNullOrEmpty(location))
                ServerLocation = Strings.Common_NotAvailable;
            else
                ServerLocation = location;

            OnPropertyChanged(nameof(ServerLocation));
        }

        private void CopyInstanceId() => Clipboard.SetDataObject(InstanceId);
    }
}
