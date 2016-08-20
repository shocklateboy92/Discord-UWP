using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Discord_UWP
{
    public class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void SetValue<T>(ref T _target, T value, [CallerMemberName] string propertyName = null) where T : class
        {
            if (!_target.Equals(value))
            {
                _target = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
