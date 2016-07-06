using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace Discord_UWP
{
    public static class Helpers
    {
        public static uint BitsIn(int bytes)
        {
            return (uint) bytes * 8;
        }

        public static EventHandler<ArgType> HandlerInUiThread<ArgType>(EventHandler<ArgType> onDataReceived)
        {
            return (s, e) => 
                CoreApplication.MainView.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () => onDataReceived(s, e)
                ).AsTask();
        }
    }
}
