using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_UWP
{
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

        public bool VisualizationEnabled
        {
            get
            {
                return _visualizationEnabled;
            }
            set
            {
                _visualizationEnabled = value;

                if (_visualizationEnabled)
                {
                    _dataSocket.PacketReceived += OnDataReceived;
                    _dataManager.OutgoingDataReady += OnOutgoingData;
                }
                else
                {
                    _dataSocket.PacketReceived -= OnDataReceived;
                    _dataManager.OutgoingDataReady -= OnOutgoingData;
                }
            }
        }

        internal VoiceGraphViewModel(VoiceDataManager dataManager, VoiceDataSocket dataSocket)
        {
            _dataManager = dataManager;
            _dataSocket = dataSocket;
            _visualizationEnabled = false;
        }

        private void OnOutgoingData(object sender, VoiceDataSocket.VoicePacket e)
        {
            Helpers.RunInUiThread(() =>
            {
                LastEnergy = e.Energy;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastEnergy)));
            });
        }

        private void OnDataReceived(object sender, VoiceDataSocket.VoicePacket e)
        {
            Helpers.RunInUiThread(() =>
            {
                if (!_sourcesMap.ContainsKey(e.Ssrc))
                {
                    var decoder = _dataManager.DecoderForSsrc(e.Ssrc);
                    if (decoder != null)
                    {
                        var info = new VoiceGraphInfo(e.Ssrc, decoder);
                        _sourcesMap.Add(e.Ssrc, info);
                        AudioSources.Add(info);
                    }
                }

                RehighlightItem?.Invoke(this, _sourcesMap[e.Ssrc]);
            });
        }

        private VoiceDataManager _dataManager;

        private IDictionary<uint, VoiceGraphInfo> _sourcesMap
            = new Dictionary<uint, VoiceGraphInfo>();
        private VoiceDataSocket _dataSocket;
        private bool _visualizationEnabled;
    }
}
