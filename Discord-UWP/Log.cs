using System;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace Discord_UWP
{
    public static class Log
    {
        public static ObservableCollection<string> CurrentMessages { get; } = new ObservableCollection<string>();

        public static void WriteLine(string line)
        {
            //CoreApplication.GetCurrentView().CoreWindow.Dispatcher.RunAsync(
            //    CoreDispatcherPriority.Normal, () => CurrentMessages.Add(line)
            //).AsTask();
            CoreApplication.MainView.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () => CurrentMessages.Add(line)
            ).AsTask();
        }
    }
}