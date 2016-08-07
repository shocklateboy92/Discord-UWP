using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Discord_UWP
{
    public sealed partial class VoiceGraphPage : UserControl, INotifyPropertyChanged
    {
        public VoiceGraphViewModel ViewModel { get; private set; }

        public VoiceGraphPage()
        {
            this.InitializeComponent();
            App.Client.ChannelChanged += OnViewModelChanged;
        }

        ~VoiceGraphPage()
        {
            Log.WriteLine("clearning out");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnViewModelChanged(object sender, VoiceGraphViewModel viewModel)
        {
            Helpers.RunInUiThread(() =>
            {
                if (ViewModel != null)
                {
                    ViewModel.RehighlightItem -= OnHighlightRequested;
                }

                ViewModel = viewModel;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModel)));
                
                if (ViewModel != null)
                {
                    _itemsListView.ItemsSource = ViewModel.AudioSources;
                    ViewModel.RehighlightItem += OnHighlightRequested;
                }
            });
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

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // We want those events to be un-bound when we're gone
            if (ViewModel != null)
            {
                ViewModel.VisualizationEnabled = false;
            }
        }
    }
}
