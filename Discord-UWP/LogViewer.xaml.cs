using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public sealed partial class LogViewer : PivotItem
    {
        public ObservableCollection<string> LogMessages => Log.CurrentMessages;

        public LogViewer()
        {
            this.InitializeComponent();
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
    }
}
