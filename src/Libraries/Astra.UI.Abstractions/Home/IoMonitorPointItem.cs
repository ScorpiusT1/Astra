using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Astra.UI.Abstractions.Home
{
    /// <summary>
    /// 首页 IO 监控模块中的单行点位（与 IO 配置中的勾选项对应）。
    /// </summary>
    public sealed class IoMonitorPointItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _address = string.Empty;
        private string _value = string.Empty;
        private bool _isOn;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>PLC 地址（用于区分同名或快速辨认点位）。</summary>
        public string Address
        {
            get => _address;
            set
            {
                if (_address == value)
                {
                    return;
                }

                _address = value;
                OnPropertyChanged();
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                if (_value == value)
                {
                    return;
                }

                _value = value;
                OnPropertyChanged();
            }
        }

        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value)
                {
                    return;
                }

                _isOn = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
