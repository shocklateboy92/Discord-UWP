using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Discord_UWP
{
    public class GuildInfo : Observable
    {
        public ObservableCollection<ChannelInfo> Channels { get; } =
            new ObservableCollection<ChannelInfo>();

        public string Name => _guild.Name;

        public string Id => _guild.Id;

        public string IconUrl => $"https://cdn.discordapp.com/icons/{_guild.Id}/{_guild.Icon}.jpg";

        public bool HasIcon => !string.IsNullOrWhiteSpace(_guild.Icon);

        public char TitleText => char.ToUpper(Name.First((c) => char.IsLetterOrDigit(c)));

        public GuildInfo(Guild guild)
        {
            _guild = guild;

            foreach (var channel in guild.Channels)
            {
                Channels.Add(new ChannelInfo(channel, guild.VoiceStates));
            }
        }

        internal void ProcessUpdate(Guild update)
        {
            UpdateProperty(nameof(_guild.Name), update.Name, new List<string> { nameof(Name), nameof(TitleText) });
            UpdateProperty(nameof(_guild.Icon), update.Icon, new List<string> { nameof(IconUrl) });
        }

        private void UpdateProperty(string property, object value, IList<string> deps)
        {
            if (value != null)
            {
                var prop = _guild.GetType().GetProperty(property);
                if (!prop.GetValue(_guild).Equals(value))
                {
                    prop.SetValue(_guild, value);
                    Helpers.RunInUiThread(() =>
                    {
                        foreach (var d in deps)
                        {
                            OnPropertyChanged(d);
                        }
                    });
                }
            }
        }

        private Guild _guild;
    }
}
