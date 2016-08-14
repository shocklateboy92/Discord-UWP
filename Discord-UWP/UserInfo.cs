using System;
using System.Collections.Generic;

namespace Discord_UWP
{
    public class UserInfo
    {
        public string Name => _user.Username;

        public string Id => _user.Id;

        public UserInfo(User user)
        {
            _user = user;
            if (!_userMap.ContainsKey(Id))
            {
                _userMap.Add(Id, this);
            }
        }

        public static UserInfo FromId(string userId) =>
            _userMap[userId];

        private User _user;
        private static IDictionary<string, UserInfo> _userMap =
            new Dictionary<string, UserInfo>();
    }
}