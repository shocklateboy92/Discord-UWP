using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Discord_UWP
{
    public sealed partial class VoiceGraphPage : Page
    {
        public DiscordClient.VoiceGraphViewModel ViewModel { get; private set; } 

        public VoiceGraphPage()
        {
            this.InitializeComponent();
            App.Client.ChannelChanged += 
                Helpers.HandlerInUiThread<DiscordClient.VoiceGraphViewModel>(OnViewModelChanged);
        }

        public void OnViewModelChanged(object sender, DiscordClient.VoiceGraphViewModel viewModel)
        {
            if (ViewModel != null)
            {
                ViewModel.RehighlightItem -= OnHighlightRequested;
            }

            ViewModel = viewModel;
            if (ViewModel != null)
            {
                _itemsListView.ItemsSource = ViewModel.AudioSources;
                ViewModel.RehighlightItem += OnHighlightRequested;
            }
        }

        private void OnHighlightRequested(object sender, VoiceGraphInfo e)
        {
            var container = _itemsListView.ContainerFromItem(e) as ListViewItem;
            if (container == null)
            {
                // Maybe this thing hasn't been added to the ListView fully yet
                return;
            }

            var template = container.ContentTemplateRoot as FrameworkElement;
            Debug.Assert(template != null);

            var storyboard = template.FindName("Storyboard") as Storyboard;
            storyboard.Stop();
            storyboard.BeginTime = TimeSpan.FromTicks(0);
            storyboard.Begin();
        }
    }
}
