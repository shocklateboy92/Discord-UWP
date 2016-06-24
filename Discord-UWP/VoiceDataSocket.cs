using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Discord_UWP
{
    class VoiceDataSocket
    {
        private DatagramSocket _dataSocket;
        private DataWriter _udpWriter;
        private bool _ipDiscoveryCompleted;

        public event EventHandler<ReadyEventArgs> Ready;
        public event EventHandler<VoicePacket> PacketReceived;

        public uint Ssrc { get; set; }

        public async Task Initialize(string endpoint, int port)
        {
            _dataSocket = new DatagramSocket();
            _dataSocket.MessageReceived += OnUdpMessageReceived;
            _udpWriter = new DataWriter(_dataSocket.OutputStream);

                await _dataSocket.ConnectAsync(new EndpointPair(new HostName("192.168.1.104"), "7771", new HostName(endpoint), port.ToString()));
            //await _dataSocket.ConnectAsync(
            //    new EndpointPair(
            //        new HostName("192.168.1.104"),
            //        "7771",
            //        new HostName(endpoint),
            //        port.ToString()
            //    )
            //);

            // Perform IP discovery
            _ipDiscoveryCompleted = false;
            // Packet to ask the server to send back our (NAT/external) address
            _udpWriter.WriteUInt32(Ssrc);
            _udpWriter.WriteBytes(new byte[70 - sizeof(uint)]);
            await _udpWriter.StoreAsync();
        }

        private void OnUdpMessageReceived(
            DatagramSocket sender,
            DatagramSocketMessageReceivedEventArgs args)
        {
            var reader = args.GetDataReader();
            reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

            // IP Discovery response packet
            if (!_ipDiscoveryCompleted)
            {
                var ssrc = (uint)IPAddress.NetworkToHostOrder(reader.ReadInt32());
                if (ssrc != Ssrc)
                {
                    Log.Error(
                        string.Format(
                            "Incorrect SSRC in IP Discovery packet. Expected: {0}, Actual: {1}",
                            Ssrc,
                            ssrc
                        )
                    );
                }

                var remainingBytes = reader.ReadString(reader.UnconsumedBufferLength - sizeof(ushort));
                var localAddress = string.Join("", remainingBytes.TakeWhile(c => c != '\u0000'));
                Log.WriteLine("Localhost = " + localAddress);

                var localPort = reader.ReadUInt16();
                Log.WriteLine("LocalPort = " + localPort);

                _ipDiscoveryCompleted = true;

                Ready?.Invoke(this, new ReadyEventArgs
                {
                    Port = localPort,
                    Address = localAddress
                });
            }
            // Data Packet
            else
            {
                var header = new byte[12];
                reader.ReadBytes(header);
                if (header[0] != 0x80) return; //flags
                if (header[1] != 0x78) return; //payload type. you know, from before.

                ushort sequenceNumber = (ushort)((header[2] << 8) | header[3] << 0);
                uint timDocuestamp = (uint)((header[4] << 24) | header[5] << 16 | header[6] << 8 | header[7] << 0);
                uint ssrc = (uint)((header[8] << 24) | (header[9] << 16) | (header[10] << 8) | (header[11] << 0));

                int packetLength = (int)reader.UnconsumedBufferLength;
                byte[] packet = new byte[packetLength];
                reader.ReadBytes(packet);

                if (packetLength < 12)
                {
                    return;
                }

                PacketReceived?.Invoke(this, new VoicePacket
                {
                    Data = packet,
                    SequenceNumber = sequenceNumber,
                    Ssrc = ssrc,
                    TimeStamp = timDocuestamp
                });
            }
        }

        public struct ReadyEventArgs
        {
            public string Address { get; set; }
            public ushort Port { get; set; }
        }

        public struct VoicePacket
        {
            public byte[] Data { get; set; }
            public uint Ssrc { get; set; }
            public ushort SequenceNumber { get; set; }
            public uint TimeStamp { get; set; }
        }
    }
}
