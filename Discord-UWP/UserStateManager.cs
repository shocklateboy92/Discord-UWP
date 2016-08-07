using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_UWP
{
    public class GuildUserManager
    {
        public ObservableCollection<User> CurrentUsers { get; set; } 
            = new ObservableCollection<User>();

        public void ProcessGuildChannel(Guild newGuild, Channel targetChannel)
        {
            if (_guild != null && newGuild.Id != _guild.Id)
            {
                Log.Error($"Trying to apply guild update for {newGuild.Id} ({newGuild.Name}) to object of {_guild.Id} ({_guild.Name})");
            }
            _guild = newGuild;

            foreach (var member in _guild.Members)
            {
                _usersMap.Add(member.User.Id, member.User);
            }

            foreach (var state in _guild.VoiceStates)
            {
                if (state.ChannelId == targetChannel.Id)
                {
                    CurrentUsers.Add(GetUser(state.UserId));
                }
            }
        }

        public User GetUser(string id) => _usersMap[id];

        private Guild _guild;

        private IDictionary<string, User> _usersMap =
            new Dictionary<string, User>();
    }
}
