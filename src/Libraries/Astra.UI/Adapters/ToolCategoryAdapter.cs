using Astra.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Astra.UI.Adapters
{
    /// <summary>
    /// 工具类别适配器 - 将不同泛型参数的 IToolCategory<T> 适配为统一的 IToolCategory<IToolItem>
    /// </summary>
    internal class ToolCategoryAdapter : IToolCategory<IToolItem>
    {
        private readonly object _sourceCategory;
        private readonly Type _interfaceType;
        private readonly PropertyInfo _nameProperty;
        private readonly PropertyInfo _iconCodeProperty;
        private readonly PropertyInfo _descriptionProperty;
        private readonly PropertyInfo _toolsProperty;
        private readonly PropertyInfo _isSelectedProperty;
        private readonly PropertyInfo _isEnabledProperty;
        private readonly PropertyInfo _categoryColorProperty;
        private readonly PropertyInfo _categoryLightColorProperty;
        private ObservableCollection<IToolItem> _adaptedTools;

        public ToolCategoryAdapter(object sourceCategory, Type interfaceType)
        {
            _sourceCategory = sourceCategory ?? throw new ArgumentNullException(nameof(sourceCategory));
            _interfaceType = interfaceType ?? throw new ArgumentNullException(nameof(interfaceType));

            // 缓存属性反射信息
            _nameProperty = _interfaceType.GetProperty(nameof(Name));
            _iconCodeProperty = _interfaceType.GetProperty(nameof(IconCode));
            _descriptionProperty = _interfaceType.GetProperty(nameof(Description));
            _toolsProperty = _interfaceType.GetProperty(nameof(Tools));
            _isSelectedProperty = _interfaceType.GetProperty(nameof(IsSelected));
            _isEnabledProperty = _interfaceType.GetProperty(nameof(IsEnabled));
            _categoryColorProperty = _interfaceType.GetProperty(nameof(CategoryColor));
            _categoryLightColorProperty = _interfaceType.GetProperty(nameof(CategoryLightColor));

            // 订阅源对象的属性变更
            if (_sourceCategory is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += OnSourcePropertyChanged;
            }
        }

        public string Name
        {
            get => _nameProperty?.GetValue(_sourceCategory) as string ?? string.Empty;
            set => _nameProperty?.SetValue(_sourceCategory, value);
        }

        public string IconCode
        {
            get => _iconCodeProperty?.GetValue(_sourceCategory) as string ?? string.Empty;
            set => _iconCodeProperty?.SetValue(_sourceCategory, value);
        }

        public string Description
        {
            get => _descriptionProperty?.GetValue(_sourceCategory) as string ?? string.Empty;
            set => _descriptionProperty?.SetValue(_sourceCategory, value);
        }

        public ObservableCollection<IToolItem> Tools
        {
            get
            {
                if (_adaptedTools == null)
                {
                    var sourceTools = _toolsProperty?.GetValue(_sourceCategory);
                    if (sourceTools != null)
                    {
                        // 将源集合中的项转换为 IToolItem
                        var items = ((System.Collections.IEnumerable)sourceTools)
                            .Cast<object>()
                            .OfType<IToolItem>()
                            .ToList();
                        _adaptedTools = new ObservableCollection<IToolItem>(items);
                    }
                    else
                    {
                        _adaptedTools = new ObservableCollection<IToolItem>();
                    }
                }
                return _adaptedTools;
            }
            set => _adaptedTools = value;
        }

        public bool IsSelected
        {
            get => _isSelectedProperty?.GetValue(_sourceCategory) is bool selected && selected;
            set => _isSelectedProperty?.SetValue(_sourceCategory, value);
        }

        public bool IsEnabled
        {
            get => _isEnabledProperty?.GetValue(_sourceCategory) is bool enabled ? enabled : true;
            set => _isEnabledProperty?.SetValue(_sourceCategory, value);
        }

        public Brush CategoryColor
        {
            get => _categoryColorProperty?.GetValue(_sourceCategory) as Brush;
            set => _categoryColorProperty?.SetValue(_sourceCategory, value);
        }

        public Brush CategoryLightColor
        {
            get => _categoryLightColorProperty?.GetValue(_sourceCategory) as Brush;
            set => _categoryLightColorProperty?.SetValue(_sourceCategory, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 转发属性变更通知
            PropertyChanged?.Invoke(this, e);

            // 如果 Tools 属性变更，清除缓存
            if (e.PropertyName == nameof(Tools))
            {
                _adaptedTools = null;
            }
        }

        /// <summary>
        /// 获取源对象（用于比较）
        /// </summary>
        public object GetSourceCategory() => _sourceCategory;
    }
}
