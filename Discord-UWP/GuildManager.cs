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
                    ActiveGuilds.Add(new GuildInfo(guild));
                }

                CurrentGuild = ActiveGuilds.FirstOrDefault();
            });
        }
    }
}
