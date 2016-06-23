﻿using System;
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
        public static readonly uint BitsPerSample = 16;

        private AudioGraph _incomingAudioGraph;
        private AudioGraph _outgoingAudioGraph;

        private AudioDeviceInputNode _inputDevice;
        private AudioDeviceOutputNode _outputDevice;

        private IDictionary<uint, VoiceDecoder> 
            _decoders = new Dictionary<uint, VoiceDecoder>();

        public async Task Initialize()
        {
            await CreateAudioGraphs();
            await CreateInputDevice();
            await CreateOutputDevice();

            _incomingAudioGraph.Start();
        }

        private async Task CreateAudioGraphs()
        {
            var result = await AudioGraph.CreateAsync(
                new AudioGraphSettings(AudioRenderCategory.Communications)
                {
                    EncodingProperties = AudioEncodingProperties.CreatePcm(
                        VoiceDecoder.SampleRate,
                        VoiceDecoder.NumChannels,
                        BitsPerSample
                    )
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
            _incomingAudioGraph = result.Graph;

            result = await AudioGraph.CreateAsync(
                new AudioGraphSettings(AudioRenderCategory.Communications)
                {
                    EncodingProperties = AudioEncodingProperties.CreatePcm(
                        VoiceEncoder.SampleRate,
                        VoiceEncoder.NumChannels,
                        BitsPerSample
                    )
                }
            );

            if (result.Status != AudioGraphCreationStatus.Success)
            {
                throw new VoiceException(
                    string.Format(
                        "Failed to create OutgoingAudio graph: {0}",
                        result.Status
                    )
                );
            }
            _outgoingAudioGraph = result.Graph;
        }

        private async Task CreateInputDevice()
        {
            var result = await _outgoingAudioGraph.CreateDeviceInputNodeAsync(
                Windows.Media.Capture.MediaCategory.Communications
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
            var result = await _incomingAudioGraph.CreateDeviceOutputNodeAsync();

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

        public void ProcessIncomingData(byte[] data, uint ssrc)
        {
            if (!_decoders.ContainsKey(ssrc))
            {
                _decoders[ssrc] = new VoiceDecoder(_incomingAudioGraph, ssrc);
                _decoders[ssrc].Node.AddOutgoingConnection(_outputDevice);
            }

            _decoders[ssrc].ProcessPacket(data);
        }
    }
}
