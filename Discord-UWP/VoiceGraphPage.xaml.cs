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
        public DiscordClient.VoiceGraphViewModel ViewModel { get; } 
            = new DiscordClient.VoiceGraphViewModel();

        public VoiceGraphPage()
        {
            this.InitializeComponent();
            ViewModel.AudioSources.CollectionChanged += AudioSources_CollectionChanged;
            AudioSources_CollectionChanged(null, null);
        }

        private void AudioSources_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var items = ViewModel.AudioSources as IEnumerable<VoiceGraphInfo>;
            if (e != null)
            {
                Debug.Assert(e.Action == NotifyCollectionChangedAction.Add);
                items = e.NewItems.Cast<VoiceGraphInfo>();
            }

            Debug.Assert(items != null, "Audio source list update error");

            foreach (var item in items)
            {
                item.Rehighlight += Item_Rehighlight;
            }
        }

        private void Item_Rehighlight(object sender, object e)
        {
            var container = _itemsListView.ContainerFromItem(sender) as ListViewItem;
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
