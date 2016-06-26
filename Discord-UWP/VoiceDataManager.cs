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
    class VoiceDataManager
    {
        public const uint BitsPerSample = 16;
        public event EventHandler<VoiceDataSocket.VoicePacket> OutgoingDataReady;

        public async Task Initialize()
        {
            await CreateAudioGraphs();
            await CreateInputDevice();
            await CreateOutputDevice();

            _encoder = new VoiceEncoder(_audioGraph);
            _inputDevice.AddOutgoingConnection(_encoder.Node);

            _audioGraph.Start();
        }

        public void ProcessIncomingData(byte[] data, uint ssrc)
        {
            if (!_decoders.ContainsKey(ssrc))
            {
                _decoders[ssrc] = new VoiceDecoder(_audioGraph, ssrc);
                _decoders[ssrc].Node.AddOutgoingConnection(_outputDevice);
                _decoders[ssrc].Node.Start();
            }

            _decoders[ssrc].ProcessPacket(data);
        }

        public void StartOutgoingAudio(uint ssrc)
        {
            _encoder.Ssrc = ssrc;
            _encoder.Node.Start();
            _audioGraph.QuantumProcessed += OnOutgoingQuantumProcessed;
        }

        public void StopOutgoingAudio()
        {
            _audioGraph.QuantumProcessed -= OnOutgoingQuantumProcessed;
            _encoder.Node.Stop();
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

        private AudioGraph _audioGraph;

        private AudioDeviceInputNode _inputDevice;
        private AudioDeviceOutputNode _outputDevice;

        private IDictionary<uint, VoiceDecoder> 
            _decoders = new Dictionary<uint, VoiceDecoder>();
        private VoiceEncoder _encoder;
    }
}
