using System.Collections.ObjectModel;
using Windows.UI.Xaml.Controls;

namespace Discord_UWP
{
    public sealed partial class VoiceGraphPage : Page
    {
        public ObservableCollection<VoiceGraphInfo> Graphs { get; private set; }

        public VoiceGraphPage()
        {
            this.InitializeComponent();
        }
    }
}
