using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Discord_UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public ObservableCollection<string> LogMessages => Log.CurrentMessages;

        public ObservableCollection<GuildInfo> Guilds => App.Client.GuildManager.ActiveGuilds;

        public MainPage()
        {
            this.InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Do our discord app initialization
            Task.Run(App.InitializeApplication);
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateWrapMode(e.AddedItems, TextWrapping.Wrap);
            UpdateWrapMode(e.RemovedItems, TextWrapping.NoWrap);
        }

        private void UpdateWrapMode(IList<Object> items, TextWrapping wrapMode)
        {
            foreach (var item in items)
            {
                var container = (_logListView.ContainerFromItem(item)) as ListViewItem;
                var textBlock = container.ContentTemplateRoot as TextBlock;
                textBlock.TextWrapping = wrapMode;
            }
        }

        private void OnJoinScrubClicked(object sender, RoutedEventArgs e) => App.Client.JoinChannel();

        private void OnLeaveScrubClicked(object sender, RoutedEventArgs e) => App.Client.LeaveChannel();

        private void OnStartVoiceClicked(object sender, RoutedEventArgs e) => App.Client.StartSendingVoice();

        private void OnStopVoiceClicked(object sender, RoutedEventArgs e) => App.Client.StopSendingVoice();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _navSplit.IsPaneOpen = !_navSplit.IsPaneOpen;
        }

        private void OnSelectedNavItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Any())
            {
                App.Client.GuildManager.CurrentGuild = (GuildInfo)e.AddedItems.First();
            }
        }
    }
}
