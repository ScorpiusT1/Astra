using Astra.UI.Controls;
using System.ComponentModel;

namespace Astra.UI.Models
{
    /// <summary>
    /// 工具项实现
    /// </summary>
    public class ToolItem : IToolItem, INotifyPropertyChanged
    {
        private string _name;
        private string _iconCode;
        private string _description;
        private object _nodeType;
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

        /// <summary>
        /// 节点类型 - 可以是 Type 对象或类型名称字符串
        /// </summary>
        public object NodeType
        {
            get => _nodeType;
            set
            {
                if (_nodeType != value)
                {
                    _nodeType = value;
                    OnPropertyChanged(nameof(NodeType));
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

