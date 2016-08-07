using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Linq;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;

namespace Discord_UWP
{
    class VoiceEncoder
    {
        public const int SampleRate = VoiceDecoder.SampleRate;
        public const int NumChannels = 1;

        static readonly uint[] SupportedFrameSizes = { 120, 240, 480, 960, 1920 };

        public AudioFrameOutputNode Node { get; private set; }

        public uint Ssrc { get; set; }

        public double RequiredEnergy { get; set; } = 0.03;

        public int MaxTrailingPackets { get; set; } = 5;

        public VoiceEncoder(AudioGraph graph)
        {
            Node = graph.CreateFrameOutputNode(
                AudioEncodingProperties.CreatePcm(
                    SampleRate,
                    NumChannels,
                    VoiceDataManager.BitsPerSample
                )
            );

            _encoder = OpusEncoder.Create(
                SampleRate,
                NumChannels,
                OpusApplication.OPUS_APPLICATION_AUDIO
            );
        }

        public VoiceDataSocket.VoicePacket GetVoicePacket()
        {
            lock (_encodeLock)
            {
                var frameSize = ReadDataFromFrame(Node.GetFrame());

                var e = Math.Sqrt(_preEncodeBuffer.Take((int)frameSize).Select(x => ((double)x) * x).Sum() / frameSize);
                if (e < RequiredEnergy)
                {
                    _currentTrailingPackets++;
                    if (_currentTrailingPackets > MaxTrailingPackets)
                    {
                        return null;
                    }
                } else
                {
                    _currentTrailingPackets = 0;
                }

                // We're abandoning extra data that won't fit nicely into a frame
                if (!SupportedFrameSizes.Contains(frameSize))
                {
                    uint bestFrameSize = 0;
                    foreach (var size in SupportedFrameSizes)
                    {
                        if (size <= frameSize)
                        {
                            bestFrameSize = size;
                        }
                    }

                    if (bestFrameSize == 0)
                    {
                        return null;
                    }

                    Log.Warning($"Got frame of unsupported size: {frameSize}. Trimming to {bestFrameSize}");
                    frameSize = bestFrameSize;
                }

                int encodedLen = _encoder.Encode(
                    _preEncodeBuffer,
                    0,
                    (int) frameSize,
                    _encodeBuffer,
                    0,
                    _encodeBuffer.Length
                );

                _sequence++;
                _timeStamp += frameSize;

                return new VoiceDataSocket.VoicePacket
                {
                    Data = _encodeBuffer.Take(encodedLen).ToArray(),
                    SequenceNumber = _sequence,
                    TimeStamp = _timeStamp,
                    Ssrc = Ssrc,
                    Energy = e
                };
            }
        }

        private unsafe uint ReadDataFromFrame(AudioFrame frame)
        {
            using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
            {
                using (var reference = buffer.CreateReference())
                {
                    byte* buf;
                    uint length;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out buf, out length);

                    float* typedbuf = (float*)buf;
                    uint samples = length / sizeof(float);

                    if (samples > _preEncodeBuffer.Length)
                    {
                        Log.Warning($"Received way to much audio data to process: {samples}");
                        samples = SupportedFrameSizes.Last();
                    }

                    for (int i = 0; i < samples; i++)
                    {
                        _preEncodeBuffer[i] = typedbuf[i];
                    }

                    return samples;
                }
            }
        }

        private OpusEncoder _encoder;
        private byte[] _encodeBuffer = new byte[4096];
        private float[] _preEncodeBuffer = new float[SupportedFrameSizes.Last() * 2];
        private object _encodeLock = new object();
        private ushort _sequence;
        private uint _timeStamp;
        private int _currentTrailingPackets;
    }
}
