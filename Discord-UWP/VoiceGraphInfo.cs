﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_UWP
{
    public class VoiceGraphInfo
    {
        public event EventHandler<object> Rehighlight;

        public uint Ssrc { get; private set; }

        public double Gain => _decoder.Node.OutgoingGain;

        private VoiceDecoder _decoder;

        public VoiceGraphInfo(uint ssrc, VoiceDecoder decoder)
        {
            Ssrc = ssrc;
            _decoder = decoder;
        }

        public void OnDataReceived() => Rehighlight?.Invoke(this, null);
    }
}
