using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_UWP
{
    public class GuildManager
    {
        public ObservableCollection<GuildInfo> ActiveGuilds { get; } =
            new ObservableCollection<GuildInfo>();

        GuildInfo _currentGuild;
        public GuildInfo CurrentGuild
        {
            get
            {
                return _currentGuild;
            }
            set
            {
                _currentGuild = value;
                CurrentGuildChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<GuildInfo> CurrentGuildChanged;

        public void ProcessInitialState(D initialState)
        {
            Helpers.RunInUiThread(() =>
            {
                foreach (var guild in initialState.Guilds)
                {
                    foreach (var user in guild.Members)
                    {
                        // This class keeps an internal static list of these,
                        // so all we have to do is instantiate them
                        new UserInfo(user.User);
                    }

                    ActiveGuilds.Add(new GuildInfo(guild));
                }

                CurrentGuild = ActiveGuilds.FirstOrDefault();
            });
        }
        public void ProcessVoiceStateUpdate(VoiceStateUpdate voiceState)
        {
            var guild = ActiveGuilds.FirstOrDefault(
                (g) => g.Id == voiceState.GuildId
            );

            foreach (var channel in guild?.Channels)
            {
                // We have to do this for every channel, because
                // we don't know what channel they were in before
                channel.ProcessVoiceStateUpdate(voiceState);
            }
        }
    }
}
