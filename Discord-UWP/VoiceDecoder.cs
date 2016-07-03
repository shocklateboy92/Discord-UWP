using Concentus;
using Concentus.Common.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Concentus.Structs;
using Concentus.Enums;
using Windows.Media;

namespace Discord_UWP
{
    public class VoiceDecoder
    {
        public const int SampleRate = 48000;
        public const int NumChannels = 2;
        public const int MaxFrameSize = 5760;

        public AudioFrameInputNode Node { get; private set; }

        public VoiceDecoder(AudioGraph graph, uint ssrc)
        {
            Node = graph.CreateFrameInputNode(
                AudioEncodingProperties.CreatePcm(
                    SampleRate,
                    NumChannels,
                    BitHelpers.BitsIn(sizeof(short))
                )
            );

            BoxedValue<int> error = new BoxedValue<int>();
            _decoder = OpusDecoder.Create(
                SampleRate,
                NumChannels,
                error
            );

            if (error.Val != OpusError.OPUS_OK)
            {
                Log.WriteLine(string.Format(
                    "Unable to create opus decoder for SSRC {0}: 0x{1}",
                    ssrc,
                    error.Val.ToString("X8")
                ));
            }
        }

        public void ProcessPacket(byte[] packet)
        {
            lock (_decodeLock)
            {
                var samplesDecoded = _decoder.Decode(
                    packet,
                    0,
                    packet.Length,
                    _decodeBuffer,
                    0,
                    MaxFrameSize,
                    false
                );

                var frame = CreateFrame(_decodeBuffer, samplesDecoded);
                // TODO: try keeping a priority queue of AudioFrame => SequenceNumber
                Node.AddFrame(frame);
            }
        }

        unsafe public static AudioFrame CreateFrame(short[] _decodeBuffer, int numSamples)
        {
            var frame = new AudioFrame((uint)numSamples * sizeof(short) * NumChannels);
            using (AudioBuffer buf = frame.LockBuffer(AudioBufferAccessMode.Write))
            {
                using (var reference = buf.CreateReference())
                {
                    byte* dataInBytes;
                    uint capacity;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(
                        out dataInBytes, 
                        out capacity
                    );

                    short* dataInShort = (short*)dataInBytes;
                    for (int i = 0; i < numSamples * NumChannels; i++)
                    {
                        dataInShort[i] = _decodeBuffer[i];
                    }
                }
            }
            return frame;
        }

        private short[] _decodeBuffer = new short[MaxFrameSize * NumChannels];
        private object _decodeLock = new object();
        private OpusDecoder _decoder;
    }
}
