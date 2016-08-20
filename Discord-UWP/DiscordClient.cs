using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Display;

namespace Discord_UWP
{
    public class DiscordClient
    {
        public static readonly string EndpointBase = "https://discordapp.com/api";

        private GatewaySocket _gateway;
        private VoiceSocket _voiceSocket;
        private VoiceDataSocket _dataSocket;
        private VoiceDataManager _dataManager;
        private DisplayRequest _wakeLock;
        ChannelInfo _targetChannel;

        public UserInfo Self { get; private set; }
        public GuildInfo TargetGuild => GuildManager.CurrentGuild;
        public uint SelfSsrc { get; private set; }
        public GuildManager GuildManager { get; } = new GuildManager();
        public ChannelInfo TargetChannel
        {
            get
            {
                return _targetChannel;
            }
            set
            {
                if (_targetChannel != value)
                {
                    _targetChannel = value;
                    TargetChanged?.Invoke(this, null);
                }
            }
        }

        public event EventHandler<VoiceGraphViewModel> ChannelChanged;
        public event EventHandler TargetChanged;
        public event EventHandler Ready;

        public DiscordClient()
        {
            _gateway = new GatewaySocket
            {
                MessageHandlers = new Dictionary<string, IMessageHandler>
                {
                    {"VOICE_STATE_UPDATE", new MessageHandler<VoiceStateUpdate>(OnVoiceStateUpdate)},
                    {"VOICE_SERVER_UPDATE", new MessageHandler<VoiceServerUpdate>(OnVoiceServerUpdate)},
                    {"GUILD_UPDATE", new MessageHandler<Guild>(GuildManager.ProcessGuildUpdate) }
                }
            };
            _gateway.InitialStateReceived += OnInitialStateReceived;
        }

        private void OnInitialStateReceived(D initialState)
        {
            Self = new UserInfo(initialState.User);
            foreach (var guild in initialState.Guilds)
            {
                var voiceChannels = guild.Channels.Where(c => string.Compare(c.Type, "voice", ignoreCase: true) == 0).Select(c => $"'{c.Name}' ({c.Id})");
                Log.WriteLine($"found guild: '{guild.Name}' ({guild.Id}) with voice channels: {string.Join(", ", voiceChannels)}");

                var hotChannel = guild.Channels.FirstOrDefault(c => c.Id == "184883715053322241");  // Test channel
                //var hotChannel = guild.Channels.FirstOrDefault(c => c.Id == "130584500072742913"); // Scrub chat
            }

            GuildManager.ProcessInitialState(initialState);

            // By this point everything should be populated
            Ready?.Invoke(this, null);
        }

        internal void StopSendingVoice()
        {
            _voiceSocket?.SendMessage(new
            {
                op = 5,
                d = new
                {
                    speaking = false,
                    delay = 0
                }
            });
            _dataManager?.StopOutgoingAudio();
        }

        internal void StartSendingVoice()
        {
            _voiceSocket?.SendMessage(new
            {
                op = 5,
                d = new
                {
                    speaking = true,
                    delay = 0
                }
            });
            _dataManager?.StartOutgoingAudio(SelfSsrc);
        }

        internal async void LeaveChannel()
        {
            _wakeLock?.RequestRelease();

            await _gateway?.SendMessage(new
            {
                op = 4,
                d = new VoiceStateUpdate
                {
                    GuildId = TargetGuild?.Id,
                    ChannelId = null,
                    SelfDeaf = false,
                    SelfMute = false
                }
            });
            _voiceSocket?.CloseSocket();
            _dataManager?.Dispose();
            _dataSocket?.Dispose();
            _voiceSocket = null;
            _dataManager = null;
            _dataSocket = null;
        }

        internal async void JoinChannel()
        {
            if (TargetGuild == null || TargetChannel == null)
            {
                // Don't crash if user hasn't selected a channel
                return;
            }

            if (_wakeLock == null)
            {
                _wakeLock = new DisplayRequest();
            }

            _wakeLock.RequestActive();

            await _gateway.SendMessage(new
            {
                op = 4,
                d = new VoiceStateUpdate
                {
                    GuildId = TargetGuild.Id,
                    ChannelId = TargetChannel.Id,
                    SelfDeaf = false,
                    SelfMute = false
                }
            });
        }

        private void OnVoiceStateUpdate(VoiceStateUpdate voiceState)
        {
            if (voiceState.UserId == Self.Id)
            {
                bool doConnect = false;
                if (_voiceSocket != null)
                {
                    // Other event has already happened
                    doConnect = true;
                }
                else
                {
                    _voiceSocket = new VoiceSocket();
                    _voiceSocket.Ready += OnVoiceSocketReady;
                }
                _voiceSocket.UserId = voiceState.UserId;
                _voiceSocket.SessionId = voiceState.SessionId;
                _voiceSocket.ServerId = voiceState.GuildId;

                if (doConnect)
                {
                    Task.Run(_voiceSocket.BeginConnection);
                }
            }

            // This thing can handle seeing self
            GuildManager.ProcessVoiceStateUpdate(voiceState);
        }

        private void OnVoiceServerUpdate(VoiceServerUpdate voiceServer)
        {
            bool doConnect = false;
            if (_voiceSocket != null)
            {
                doConnect = true;
            }
            else
            {
                _voiceSocket = new VoiceSocket();
                _voiceSocket.Ready += OnVoiceSocketReady;
            }
            // ditch the useless port attached to the hostname
            _voiceSocket.Endpoint = voiceServer.Endpoint.Replace(":80", "");
            _voiceSocket.Token = voiceServer.Token;

            if (doConnect)
            {
                Task.Run(_voiceSocket.BeginConnection);
            }
        }

        private async void OnVoiceSocketReady(object sender, VoiceReadyData e)
        {
            SelfSsrc = e.Ssrc;
            _dataSocket = new VoiceDataSocket
            {
                Ssrc = e.Ssrc
            };
            if (_dataManager == null)
            {
                _dataManager = new VoiceDataManager();
                _dataManager.OutgoingDataReady += _dataSocket.SendPacket;
                await _dataManager.Initialize();
            }
            _dataSocket.Ready += async (o, args) =>
            {
                await _voiceSocket.SendMessage(new
                {
                    op = 1,
                    d = new
                    {
                        protocol = "udp",
                        data = new
                        {
                            address = args.Address,
                            port = args.Port,
                            mode = "plain"
                        },
                    }
                });

                ChannelChanged?.Invoke(this, new VoiceGraphViewModel(_dataManager, _dataSocket));
            };
            _dataSocket.PacketReceived += _dataManager.ProcessIncomingData;
            await _dataSocket.Initialize(_voiceSocket.Endpoint, e.Port);
        }

        public async Task UpdateGateway()
        {
            await _gateway.BeginConnection();
        }

        public void CloseSockets()
        {
            Debug.WriteLine("Closing socket...");
            _gateway?.CloseSocket();
            _voiceSocket?.CloseSocket();
            _voiceSocket = null;
            var newGateway = new GatewaySocket()
            {
                MessageHandlers = _gateway.MessageHandlers
            };
            _gateway = newGateway;
        }
    }
}
