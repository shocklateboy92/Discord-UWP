using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_UWP
{
    public class ChannelInfoViewModel
    {
        public ObservableCollection<User> Users
            => App.Client?.UserManager.CurrentUsers;
    }
}
