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

        public User Self { get; private set; }
        public Guild TargetGuild { get; private set; }
        public Channel TargetChannel { get; private set; }
        public uint SelfSsrc { get; private set; }
        public event EventHandler<VoiceGraphViewModel> ChannelChanged;

        public DiscordClient()
        {
            _gateway = new GatewaySocket
            {
                MessageHandlers = new Dictionary<string, IMessageHandler>
                {
                    {"VOICE_STATE_UPDATE", new MessageHandler<VoiceStateUpdate>(OnVoiceStateUpdate)},
                    {"VOICE_SERVER_UPDATE", new MessageHandler<VoiceServerUpdate>(OnVoiceServerUpdate)}
                }
            };
            _gateway.InitialStateReceived += OnInitialStateReceived;
        }

        private void OnInitialStateReceived(D initialState)
        {
            Self = initialState.User;
            foreach (var guild in initialState.Guilds)
            {
                var voiceChannels = guild.Channels.Where(c => string.Compare(c.Type, "voice", ignoreCase: true) == 0).Select(c => $"'{c.Name}' ({c.Id})");
                Log.WriteLine($"found guild: '{guild.Name}' ({guild.Id}) with voice channels: {string.Join(", ", voiceChannels)}");

                var hotChannel = guild.Channels.FirstOrDefault(c => c.Id == "184883715053322241");
                //var hotChannel = guild.Channels.FirstOrDefault(c => c.Id == "130584500072742913");

                if (hotChannel != null)
                {
                    TargetChannel = hotChannel;
                    TargetGuild = guild;
                }
            }
        }

        internal async void StopSendingVoice()
        {
            await _voiceSocket.SendMessage(new
            {
                op = 5,
                d = new
                {
                    speaking = false,
                    delay = 0
                }
            });
            _dataManager.StopOutgoingAudio();
        }

        internal async void StartSendingVoice()
        {
            await _voiceSocket.SendMessage(new
            {
                op = 5,
                d = new
                {
                    speaking = true,
                    delay = 0
                }
            });
            _dataManager.StartOutgoingAudio(SelfSsrc);
        }

        internal async void LeaveChannel()
        {
            _wakeLock?.RequestActive();

            await _gateway.SendMessage(new
            {
                op = 4,
                d = new VoiceStateUpdate
                {
                    GuildId = TargetGuild.Id,
                    ChannelId = null,
                    SelfDeaf = false,
                    SelfMute = false
                }
            });
            _voiceSocket.CloseSocket();
            _dataManager.Dispose();
            _dataSocket.Dispose();
            _voiceSocket = null;
            _dataManager = null;
            _dataSocket = null;
        }

        internal async void JoinChannel()
        {
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
            _gateway.CloseSocket();
            _voiceSocket?.CloseSocket();
        }

        public class VoiceGraphViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<VoiceGraphInfo> AudioSources { get; set; }
                = new ObservableCollection<VoiceGraphInfo>();

            public event EventHandler<VoiceGraphInfo> RehighlightItem;
            public event PropertyChangedEventHandler PropertyChanged;

            public double OutgoingGain
            {
                get
                {
                    return _dataManager.OutgoingGain;
                }
                set
                {
                    _dataManager.OutgoingGain = value;
                }
            }

            public double RequiredEnergy
            {
                get
                {
                    return _dataManager.RequiredEnergy;
                }
                set
                {
                    _dataManager.RequiredEnergy = value;
                }
            }

            public double LastEnergy { get; private set; }

            internal VoiceGraphViewModel(VoiceDataManager dataManager, VoiceDataSocket dataSocket)
            {
                dataSocket.PacketReceived += 
                    Helpers.HandlerInUiThread<VoiceDataSocket.VoicePacket>(OnDataReceived);
                _dataManager = dataManager;
                _dataManager.OutgoingDataReady += 
                    Helpers.HandlerInUiThread<VoiceDataSocket.VoicePacket>(OnOutgoingData);
            }

            private void OnOutgoingData(object sender, VoiceDataSocket.VoicePacket e)
            {
                LastEnergy = e.Energy;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastEnergy)));
            }

            private void OnDataReceived(object sender, VoiceDataSocket.VoicePacket e)
            {
                if (!_sourcesMap.ContainsKey(e.Ssrc))
                {
                    var decoder = App.Client._dataManager.DecoderForSsrc(e.Ssrc);
                    if (decoder != null)
                    {
                        var info = new VoiceGraphInfo(e.Ssrc, decoder);
                        _sourcesMap.Add(e.Ssrc, info);
                        AudioSources.Add(info);
                    }
                }

                RehighlightItem?.Invoke(this, _sourcesMap[e.Ssrc]);
            }

            private VoiceDataManager _dataManager;

            private IDictionary<uint, VoiceGraphInfo> _sourcesMap 
                = new Dictionary<uint, VoiceGraphInfo>();
        }
    }
}
