using System.Collections.ObjectModel;
using System.Linq;

namespace Discord_UWP
{
    public class GuildInfo
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

        private Guild _guild;
    }
}
