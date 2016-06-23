using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace Discord_UWP
{
    public static class Log
    {
        public static ObservableCollection<string> CurrentMessages { get; } = new ObservableCollection<string>();

        public static void WriteLine(string line)
        {
            Debug.WriteLine(line);
            CoreApplication.MainView.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () => CurrentMessages.Add(line)
            ).AsTask();
        }

        internal static void LogExceptionCatch(Exception ex)
        {
            WriteLine($"Error({ex.GetType().Name}, {ex.HResult}): {ex.Message}");
        }

        internal static void Warning(string v)
        {
            WriteLine($"WARNING: {v}");
        }

        internal static void Error(string v)
        {
            throw new NotImplementedException();
        }
    }
}