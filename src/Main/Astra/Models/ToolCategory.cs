using Astra.UI.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace Astra.Models
{
    /// <summary>
    /// 工具类别实现
    /// </summary>
    public class ToolCategory : IToolCategory<IToolItem>, INotifyPropertyChanged
    {
        private string _name;
        private string _iconCode;
        private string _description;
        private bool _isSelected;
        private bool _isEnabled = true;
        private Brush _categoryColor;
        private Brush _categoryLightColor;

        public ToolCategory()
        {
            Tools = new ObservableCollection<IToolItem>();
        }

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

        public ObservableCollection<IToolItem> Tools { get; set; }

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

        public Brush CategoryColor
        {
            get => _categoryColor;
            set
            {
                if (_categoryColor != value)
                {
                    _categoryColor = value;
                    OnPropertyChanged(nameof(CategoryColor));
                }
            }
        }

        public Brush CategoryLightColor
        {
            get => _categoryLightColor;
            set
            {
                if (_categoryLightColor != value)
                {
                    _categoryLightColor = value;
                    OnPropertyChanged(nameof(CategoryLightColor));
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

