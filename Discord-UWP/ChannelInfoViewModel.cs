using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_UWP
{
    public class ChannelInfo
    {
        public ObservableCollection<User> Users
            => App.Client?.UserManager.CurrentUsers;

        public string ChannelName => _channel.Name;

        public bool IsVoice => string.Equals(
            _channel.Type, "voice", StringComparison.OrdinalIgnoreCase
        );

        public ChannelInfo(Channel channel)
        {
            _channel = channel;
        }

        private Channel _channel;
    }
}
