using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_UWP
{
    public class FriendSourceFlags
    {

        [JsonProperty("all")]
        public bool All { get; set; }
    }

    public class UserSettings
    {

        [JsonProperty("theme")]
        public string Theme { get; set; }

        [JsonProperty("show_current_game")]
        public bool ShowCurrentGame { get; set; }

        [JsonProperty("restricted_guilds")]
        public IList<object> RestrictedGuilds { get; set; }

        [JsonProperty("render_embeds")]
        public bool RenderEmbeds { get; set; }

        [JsonProperty("message_display_compact")]
        public bool MessageDisplayCompact { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; }

        [JsonProperty("inline_embed_media")]
        public bool InlineEmbedMedia { get; set; }

        [JsonProperty("inline_attachment_media")]
        public bool InlineAttachmentMedia { get; set; }

        [JsonProperty("guild_positions")]
        public IList<object> GuildPositions { get; set; }

        [JsonProperty("friend_source_flags")]
        public FriendSourceFlags FriendSourceFlags { get; set; }

        [JsonProperty("enable_tts_command")]
        public bool EnableTtsCommand { get; set; }

        [JsonProperty("developer_mode")]
        public bool DeveloperMode { get; set; }

        [JsonProperty("convert_emoticons")]
        public bool ConvertEmoticons { get; set; }

        [JsonProperty("allow_email_friend_request")]
        public bool AllowEmailFriendRequest { get; set; }
    }

    public class ChannelOverride
    {

        [JsonProperty("muted")]
        public bool Muted { get; set; }

        [JsonProperty("message_notifications")]
        public int MessageNotifications { get; set; }

        [JsonProperty("channel_id")]
        public string ChannelId { get; set; }
    }

    public class UserGuildSetting
    {

        [JsonProperty("suppress_everyone")]
        public bool SuppressEveryone { get; set; }

        [JsonProperty("muted")]
        public bool Muted { get; set; }

        [JsonProperty("mobile_push")]
        public bool MobilePush { get; set; }

        [JsonProperty("message_notifications")]
        public int MessageNotifications { get; set; }

        [JsonProperty("guild_id")]
        public string GuildId { get; set; }

        [JsonProperty("channel_overrides")]
        public IList<ChannelOverride> ChannelOverrides { get; set; }
    }

    public class User
    {
        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("discriminator")]
        public string Discriminator { get; set; }

        [JsonProperty("avatar")]
        public object Avatar { get; set; }
    }

    public class Relationship
    {
        [JsonProperty("user")]
        public User User { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class ReadState
    {

        [JsonProperty("mention_count")]
        public int MentionCount { get; set; }

        [JsonProperty("last_message_id")]
        public string LastMessageId { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class Recipient
    {

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("discriminator")]
        public string Discriminator { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }
    }

    public class PrivateChannel
    {

        [JsonProperty("recipient")]
        public Recipient Recipient { get; set; }

        [JsonProperty("last_message_id")]
        public string LastMessageId { get; set; }

        [JsonProperty("is_private")]
        public bool IsPrivate { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class Presence
    {

        [JsonProperty("user")]
        public User User { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("last_modified")]
        public long LastModified { get; set; }

        [JsonProperty("game")]
        public object Game { get; set; }
    }

    public class Notes
    {
    }

    public class VoiceState
    {

        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("suppress")]
        public bool Suppress { get; set; }

        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("self_mute")]
        public bool SelfMute { get; set; }

        [JsonProperty("self_deaf")]
        public bool SelfDeaf { get; set; }

        [JsonProperty("mute")]
        public bool Mute { get; set; }

        [JsonProperty("deaf")]
        public bool Deaf { get; set; }

        [JsonProperty("channel_id")]
        public string ChannelId { get; set; }
    }

    public class Role
    {

        [JsonProperty("position")]
        public int Position { get; set; }

        [JsonProperty("permissions")]
        public int Permissions { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("mentionable")]
        public bool Mentionable { get; set; }

        [JsonProperty("managed")]
        public bool Managed { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("hoist")]
        public bool Hoist { get; set; }

        [JsonProperty("color")]
        public int Color { get; set; }
    }

    public class Game
    {

        [JsonProperty("type")]
        public int? Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("game")]
        public string GameName { get; set; }
    }



    public class Member
    {

        [JsonProperty("user")]
        public User User { get; set; }

        [JsonProperty("roles")]
        public IList<string> Roles { get; set; }

        [JsonProperty("mute")]
        public bool Mute { get; set; }

        [JsonProperty("joined_at")]
        public DateTime JoinedAt { get; set; }

        [JsonProperty("deaf")]
        public bool Deaf { get; set; }

        [JsonProperty("nick")]
        public string Nick { get; set; }
    }

    public class PermissionOverwrite
    {

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("deny")]
        public int Deny { get; set; }

        [JsonProperty("allow")]
        public int Allow { get; set; }
    }

    public class Channel
    {

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("topic")]
        public string Topic { get; set; }

        [JsonProperty("position")]
        public int Position { get; set; }

        [JsonProperty("permission_overwrites")]
        public IList<PermissionOverwrite> PermissionOverwrites { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("last_message_id")]
        public string LastMessageId { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("user_limit")]
        public int? UserLimit { get; set; }

        [JsonProperty("bitrate")]
        public int? Bitrate { get; set; }
    }

    public class Guild
    {

        [JsonProperty("voice_states")]
        public IList<VoiceStateUpdate> VoiceStates { get; set; }

        [JsonProperty("verification_level")]
        public int VerificationLevel { get; set; }

        [JsonProperty("splash")]
        public object Splash { get; set; }

        [JsonProperty("roles")]
        public IList<Role> Roles { get; set; }

        [JsonProperty("region")]
        public string Region { get; set; }

        [JsonProperty("presences")]
        public IList<Presence> Presences { get; set; }

        [JsonProperty("owner_id")]
        public string OwnerId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("members")]
        public IList<Member> Members { get; set; }

        [JsonProperty("member_count")]
        public int MemberCount { get; set; }

        [JsonProperty("large")]
        public bool Large { get; set; }

        [JsonProperty("joined_at")]
        public DateTime JoinedAt { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("features")]
        public IList<object> Features { get; set; }

        [JsonProperty("emojis")]
        public IList<object> Emojis { get; set; }

        [JsonProperty("default_message_notifications")]
        public int DefaultMessageNotifications { get; set; }

        [JsonProperty("channels")]
        public IList<Channel> Channels { get; set; }

        [JsonProperty("afk_timeout")]
        public int AfkTimeout { get; set; }

        [JsonProperty("afk_channel_id")]
        public object AfkChannelId { get; set; }
    }

    public class D
    {

        [JsonProperty("v")]
        public int V { get; set; }

        [JsonProperty("user_settings")]
        public UserSettings UserSettings { get; set; }

        [JsonProperty("user_guild_settings")]
        public IList<UserGuildSetting> UserGuildSettings { get; set; }

        [JsonProperty("user")]
        public User User { get; set; }

        [JsonProperty("tutorial")]
        public object Tutorial { get; set; }

        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("relationships")]
        public IList<Relationship> Relationships { get; set; }

        [JsonProperty("read_state")]
        public IList<ReadState> ReadState { get; set; }

        [JsonProperty("private_channels")]
        public IList<PrivateChannel> PrivateChannels { get; set; }

        [JsonProperty("presences")]
        public IList<Presence> Presences { get; set; }

        [JsonProperty("notes")]
        public Notes Notes { get; set; }

        [JsonProperty("heartbeat_interval")]
        public int HeartbeatInterval { get; set; }

        [JsonProperty("guilds")]
        public IList<Guild> Guilds { get; set; }

        [JsonProperty("_trace")]
        public IList<string> Trace { get; set; }
    }

    public class MessageFormat
    {

        [JsonProperty("t")]
        public string T { get; set; }

        [JsonProperty("s")]
        public int S { get; set; }

        [JsonProperty("op")]
        public int Op { get; set; }

        [JsonProperty("d")]
        public D D { get; set; }
    }

    // Voice event data
    public class VoiceStateUpdate
    {

        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("suppress")]
        public bool Suppress { get; set; }

        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("self_mute")]
        public bool SelfMute { get; set; }

        [JsonProperty("self_deaf")]
        public bool SelfDeaf { get; set; }

        [JsonProperty("mute")]
        public bool Mute { get; set; }

        [JsonProperty("guild_id")]
        public string GuildId { get; set; }

        [JsonProperty("deaf")]
        public bool Deaf { get; set; }

        [JsonProperty("channel_id")]
        public string ChannelId { get; set; }
    }

    public class VoiceServerUpdate
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("guild_id")]
        public string GuildId { get; set; }

        [JsonProperty("endpoint")]
        public string Endpoint { get; set; }
    }

    // Voice Socket OperationData
    public class VoiceReadyData
    {

        [JsonProperty("ssrc")]
        public uint Ssrc { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("modes")]
        public IList<string> Modes { get; set; }

        [JsonProperty("heartbeat_interval")]
        public int HeartbeatInterval { get; set; }
    }
}