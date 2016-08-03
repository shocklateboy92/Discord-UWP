using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Windows.Media.Render;

namespace Discord_UWP
{
    class VoiceDataManager : IDisposable
    {
        public const uint BitsPerSample = 16;
        public event EventHandler<VoiceDataSocket.VoicePacket> OutgoingDataReady;

        public async Task Initialize()
        {
            try
            {
                await CreateAudioGraphs();
                await CreateInputDevice();
                await CreateOutputDevice();

                _encoder = new VoiceEncoder(_audioGraph);
                _inputDevice.AddOutgoingConnection(_encoder.Node);

                _audioGraph.Start();
            }
            catch (Exception ex)
            {
                Log.LogExceptionCatch(ex);
            }
        }

        public void ProcessIncomingData(object sender, VoiceDataSocket.VoicePacket packet)
        {
            if (_audioGraph == null) return;

            if (!_decoders.ContainsKey(packet.Ssrc))
            {
                var decoder = new VoiceDecoder(_audioGraph, packet.Ssrc);
                decoder.Node.AddOutgoingConnection(_outputDevice);
                decoder.Node.Start();

                _decoders[packet.Ssrc] = decoder;
            }

            _decoders[packet.Ssrc].ProcessPacket(packet.Data);
        }

        public void StartOutgoingAudio(uint ssrc)
        {
            if (_audioGraph == null) return;

            _encoder.Ssrc = ssrc;
            _encoder.Node.Start();
            _audioGraph.QuantumProcessed += OnOutgoingQuantumProcessed;
        }

        public void StopOutgoingAudio()
        {
            if (_audioGraph == null) return;

            _audioGraph.QuantumProcessed -= OnOutgoingQuantumProcessed;
            _encoder.Node.Stop();
        }

        public double OutgoingGain
        {
            get
            {
                return _inputDevice.OutgoingGain;
            }
            set
            {
                _inputDevice.OutgoingGain = value;
            }
        }

        public double RequiredEnergy
        {
            get
            {
                return _encoder.RequiredEnergy;
            }
            set
            {
                _encoder.RequiredEnergy = value;
            }
        }

        public VoiceDecoder DecoderForSsrc(uint ssrc)
        {
            if (_decoders.ContainsKey(ssrc))
            {
                return _decoders[ssrc];
            }

            return null;
        }

        private async Task CreateAudioGraphs()
        {
            var result = await AudioGraph.CreateAsync(
                new AudioGraphSettings(AudioRenderCategory.GameChat)
                {
                    EncodingProperties = AudioEncodingProperties.CreatePcm(
                        VoiceDecoder.SampleRate,
                        VoiceDecoder.NumChannels,
                        BitsPerSample
                    ),
                    QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency
                }
            );

            if (result.Status != AudioGraphCreationStatus.Success)
            {
                throw new VoiceException(
                    string.Format(
                        "Failed to create IncomingAudio graph: {0}",
                        result.Status
                    )
                );
            }
            _audioGraph = result.Graph;
        }

        private async Task CreateInputDevice()
        {
            var result = await _audioGraph.CreateDeviceInputNodeAsync(
                Windows.Media.Capture.MediaCategory.Other
            );

            if (result.Status != AudioDeviceNodeCreationStatus.Success)
            {
                throw new VoiceException(
                    string.Format(
                        "Failed to create audio input device node: {0}",
                        result.Status
                    )
                );
            }
            _inputDevice = result.DeviceInputNode;
        }

        private async Task CreateOutputDevice()
        {
            var result = await _audioGraph.CreateDeviceOutputNodeAsync();

            if (result.Status != AudioDeviceNodeCreationStatus.Success)
            {
                throw new VoiceException(
                    string.Format(
                        "Failed to create audio output device node: {0}",
                        result.Status
                    )
                );
            }
            _outputDevice = result.DeviceOutputNode;
        }

        private void OnOutgoingQuantumProcessed(AudioGraph sender, object args)
        {
            var voicePacket = _encoder.GetVoicePacket();
            if (voicePacket != null)
            {
                OutgoingDataReady?.Invoke(this, voicePacket);
            }
        }

        public void Dispose()
        {
            if (_audioGraph == null) return;

            StopOutgoingAudio();
            _audioGraph.Dispose();
            OutgoingDataReady = null;
        }

        private AudioGraph _audioGraph;

        private AudioDeviceInputNode _inputDevice;
        private AudioDeviceOutputNode _outputDevice;

        private IDictionary<uint, VoiceDecoder> 
            _decoders = new Dictionary<uint, VoiceDecoder>();
        private VoiceEncoder _encoder;
    }
}
