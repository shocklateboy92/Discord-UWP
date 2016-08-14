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
        public ObservableCollection<UserInfo> Users { get; } =
            new ObservableCollection<UserInfo>();

        public string ChannelName => _channel.Name;

        public string Id => _channel.Id;

        public bool IsVoice => string.Equals(
            _channel.Type, "voice", StringComparison.OrdinalIgnoreCase
        );

        public ChannelInfo(Channel channel, IList<VoiceStateUpdate> voiceStates)
        {
            _channel = channel;
            foreach (var state in voiceStates)
            {
                if (state.ChannelId == Id)
                {
                    Users.Add(UserInfo.FromId(state.UserId));
                }
            }
        }

        private Channel _channel;

        public void ProcessVoiceStateUpdate(VoiceStateUpdate voiceState)
        {
            Helpers.RunInUiThread(() =>
            {
                var user = UserInfo.FromId(voiceState.UserId);
                if (string.IsNullOrWhiteSpace(voiceState.ChannelId))
                {
                    // They've left a channel - it might have been ours
                    Users.Remove(user);
                }
                else if (voiceState.ChannelId == _channel.Id)
                {
                    // They're in our channel - they might have just joined
                    if (!Users.Contains(user))
                    {
                        Users.Add(user);
                    }
                }
            });
        }
    }
}
