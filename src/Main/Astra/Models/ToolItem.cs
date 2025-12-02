using Astra.UI.Controls;
using System.ComponentModel;

namespace Astra.Models
{
    /// <summary>
    /// 工具项实现
    /// </summary>
    public class ToolItem : IToolItem, INotifyPropertyChanged
    {
        private string _name;
        private string _iconCode;
        private string _description;
        private bool _isSelected;
        private bool _isEnabled = true;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string IconCode
        {
            get => _iconCode;
            set
            {
                if (_iconCode != value)
                {
                    _iconCode = value;
                    OnPropertyChanged(nameof(IconCode));
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

