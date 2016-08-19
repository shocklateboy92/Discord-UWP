using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Discord_UWP
{
    public sealed partial class ChannelInfoPage : UserControl, INotifyPropertyChanged
    {
        GuildInfo _viewModel;
        public GuildInfo ViewModel
        {
            get
            {
                return _viewModel;
            }
            private set
            {
                if (_viewModel != value)
                {
                    _viewModel = value;
                    PropertyChanged?.Invoke(
                        this,
                        new PropertyChangedEventArgs(nameof(ViewModel))
                    );
                    _itemSource.Source = _viewModel.Channels.OrderBy(c => c.IsVoice);
                }
            }
        }
        public ChannelInfoPage()
        {
            this.InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            App.Client.GuildManager.CurrentGuildChanged += OnGuildChanged;
            ViewModel = App.Client.GuildManager.CurrentGuild;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) =>
            App.Client.GuildManager.CurrentGuildChanged -= OnGuildChanged;

        private void OnGuildChanged(object sender, GuildInfo e) =>
            Helpers.RunInUiThread(() => { ViewModel = e; });

        private void OnChannelSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Any())
            {
                var channel = (ChannelInfo)e.AddedItems.First();
                if (channel.IsVoice)
                {
                    App.Client.TargetChannel = channel;
                }
            }
        }
    }
}
