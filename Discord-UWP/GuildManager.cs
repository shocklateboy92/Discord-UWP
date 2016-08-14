using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_UWP
{
    public class GuildManager
    {
        public ObservableCollection<GuildInfo> CurrentGuilds { get; } =
            new ObservableCollection<GuildInfo>();

        public void ProcessInitialState(D initialState)
        {
            Helpers.RunInUiThread(() =>
            {
                foreach (var guild in initialState.Guilds)
                {
                    CurrentGuilds.Add(new GuildInfo(guild));
                }
            });
        }
    }
}
