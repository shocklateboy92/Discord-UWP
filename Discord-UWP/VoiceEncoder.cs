using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;

namespace Discord_UWP
{
    class VoiceEncoder
    {
        public const int SampleRate = VoiceDecoder.SampleRate;
        public const int NumChannels = 1;
        public const int TargetFrameSize = 1920;
        public const int MillisPerSec = 1000;

        static readonly int[] SupportedFrameSizes = { 120, 240, 480, 960, 1920 };

        public AudioFrameOutputNode Node { get; private set; }
        public static int DesiredProcessingInterval => TargetFrameSize * MillisPerSec / SampleRate;

        public VoiceEncoder(AudioGraph graph)
        {
            Node = graph.CreateFrameOutputNode();

            BoxedValue<int> error = new BoxedValue<int>();
            _encoder = OpusEncoder.Create(
                SampleRate,
                NumChannels,
                OpusApplication.OPUS_APPLICATION_AUDIO,
                error);

            if (error.Val != OpusError.OPUS_OK)
            {
                Log.WriteLine(string.Format(
                    "Unable to create opus encoder: 0x{1}",
                    error.Val.ToString("X8")
                ));
            }
        }

        public byte[] GetDataPacket(out int frameSize)
        {
            lock (_encodeLock)
            {
                frameSize = ReadDataFromFrame(Node.GetFrame());

                // We're abandoning extra data that won't fit nicely into a frame
                if (!SupportedFrameSizes.Contains(frameSize))
                {
                    var bestFrameSize = 0;
                    foreach (var size in SupportedFrameSizes)
                    {
                        if (size <= frameSize)
                        {
                            bestFrameSize = size;
                        }
                    }
                    Log.Warning($"Got frame of unsupported size: {frameSize}. Trimming to {bestFrameSize}");
                    frameSize = bestFrameSize;
                }

                if (frameSize == 0)
                {
                    return null;
                }

                int encodedLen = _encoder.Encode(
                    _preEncodeBuffer,
                    0,
                    frameSize,
                    _encodeBuffer,
                    0,
                    _encodeBuffer.Length
                );

                return _encodeBuffer.Take(encodedLen).ToArray();
            }
        }

        private unsafe int ReadDataFromFrame(AudioFrame frame)
        {
            using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
            {
                using (var reference = buffer.CreateReference())
                {
                    byte* buf;
                    uint length;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out buf, out length);

                    float* typedbuf = (float*)buf;
                    int samples = (int)(length / sizeof(float));

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
    }
}
