using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Interfaces;
using Astra.UI.Controls;
using HandyControl.Themes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YamlDotNet.Core;

namespace Astra.Views
{
    /// <summary>
    /// HomeView.xaml 的交互逻辑
    /// </summary>
    public partial class HomeView : UserControl
    {
        private Person _person;
        private AppConfig _config;
        public HomeView()
        {
            InitializeComponent();


            // 初始化测试数据
            _person = new Person
            {
                Name = "张三",
                Age = 30,
                Email = "zhangsan@example.com",
                IsActive = true,
                Gender = Gender.Male,
                BirthDate = new DateTime(1994, 5, 20),
                Salary = 15000.50m,
                Tags = new List<string> { "开发", "测试" },
                Address = new Address { City = "北京", Street = "中关村大街1号" }
            };

            _config = new AppConfig
            {
                Theme = Theme.Dark,
                Language = "zh-CN",
                AutoSave = true,
                SaveInterval = 300,
                MaxHistory = 50
            };

            // 默认显示Person
            PropertyEditor.SelectedObject = _person;
        }

        private void LoadPerson_Click(object sender, RoutedEventArgs e)
        {
            PropertyEditor.SelectedObject = _person;
        }

        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            PropertyEditor.SelectedObject = _config;
        }

        private void HideAge_Click(object sender, RoutedEventArgs e)
        {
            // 方式1：通过 PropertyEditor 直接设置（旧方式）
            // PropertyEditor.SetPropertyVisibility("Age", false);

            // 方式2：通过对象自身的属性来控制（新方式，推荐）
            if (_person != null)
            {
                _person.HideAge = !_person.HideAge;
                // 由于 Person 实现了 IPropertyVisibilityProvider 和 INotifyPropertyChanged，
                // PropertyEditor 会自动检测到变化并更新属性可见性
            }
        }

        private void HideBasicInfoGroup_Click(object sender, RoutedEventArgs e)
        {
            // 切换"基本信息"组的显示/隐藏
            // 当组内所有属性都被隐藏时，组也会自动隐藏
            if (_person != null)
            {
                _person.HideBasicInfoGroup = !_person.HideBasicInfoGroup;
                // 由于 Person 实现了 IPropertyVisibilityProvider 和 INotifyPropertyChanged，
                // PropertyEditor 会自动检测到变化并更新属性可见性
                // 当组内所有属性都隐藏后，GroupItemCountToVisibilityConverter 会自动隐藏该组
            }
        }

        private void HideIsActive_Click(object sender, RoutedEventArgs e)
        {
            // 切换 IsActive 属性的显示/隐藏
            if (_person != null)
            {
                _person.HideIsActive = !_person.HideIsActive;
            }
        }

        private void GetValues_Click(object sender, RoutedEventArgs e)
        {
            var properties = PropertyEditor.GetProperties();
            var message = "当前属性值:\n\n";

            foreach (var prop in properties)
            {
                message += $"{prop.DisplayName}: {prop.Value}\n";
            }

            MessageBox.Show(message, "属性值", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PropertyEditor_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e)
        {
            Console.WriteLine($"属性 '{e.Property.DisplayName}' 已更改: {e.OldValue} → {e.NewValue}");
        }
    }

    // ==================== 测试数据模型 ====================

    public class Person : System.ComponentModel.INotifyPropertyChanged, IPropertyVisibilityProvider
    {
        private bool _hideAge;
        private bool _hideBasicInfoGroup;
        private bool _hideIsActive;
        private string _name;
        private int _age;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        [Display(Name = "姓名", GroupName = "基本信息", Order = 1, Description = "用户的姓名")]
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        [Display(Name = "年龄", GroupName = "基本信息", Order = 2)]
        public int Age
        {
            get => _age;
            set
            {
                if (_age != value)
                {
                    _age = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 控制是否隐藏 Age 属性
        /// </summary>
        [Browsable(false)]
        public bool HideAge
        {
            get => _hideAge;
            set
            {
                if (_hideAge != value)
                {
                    _hideAge = value;
                    OnPropertyChanged();
                    // 当 HideAge 改变时，触发属性可见性更新
                    OnPropertyChanged(nameof(Age));
                }
            }
        }

        /// <summary>
        /// 控制是否隐藏"基本信息"组的所有属性
        /// </summary>
        [Browsable(false)]
        public bool HideBasicInfoGroup
        {
            get => _hideBasicInfoGroup;
            set
            {
                if (_hideBasicInfoGroup != value)
                {
                    _hideBasicInfoGroup = value;
                    OnPropertyChanged();
                    // 触发所有"基本信息"组属性的可见性更新
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(Age));
                    OnPropertyChanged(nameof(Gender));
                    OnPropertyChanged(nameof(BirthDate));
                }
            }
        }

        /// <summary>
        /// 实现 IPropertyVisibilityProvider 接口，动态控制属性可见性
        /// </summary>
        public bool IsPropertyVisible(string propertyName)
        {
            // 如果隐藏"基本信息"组，则隐藏该组内的所有属性
            if (HideBasicInfoGroup)
            {
                if (propertyName == nameof(Name) || 
                    propertyName == nameof(Age) || 
                    propertyName == nameof(Gender) || 
                    propertyName == nameof(BirthDate))
                {
                    return false;
                }
            }

            // 如果 HideAge 为 true，则隐藏 Age 属性（优先级高于组隐藏）
            if (propertyName == nameof(Age))
            {
                return !HideAge;
            }

            // 如果 HideIsActive 为 true，则隐藏 IsActive 属性
            if (propertyName == nameof(IsActive))
            {
                return !HideIsActive;
            }

            // 其他属性默认可见
            return true;
        }

        [Display(Name = "电子邮件", GroupName = "联系方式", Order = 1)]
        public string Email { get; set; }

        /// <summary>
        /// 控制是否隐藏 IsActive 属性
        /// </summary>
        [Browsable(false)]
        public bool HideIsActive
        {
            get => _hideIsActive;
            set
            {
                if (_hideIsActive != value)
                {
                    _hideIsActive = value;
                    OnPropertyChanged();
                    // 当 HideIsActive 改变时，触发属性可见性更新
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        [Display(Name = "是否激活", GroupName = "状态", Order = 1)]
        public bool IsActive { get; set; }

        [Display(Name = "性别", GroupName = "基本信息", Order = 3)]
        public Gender Gender { get; set; }

        [Display(Name = "出生日期", GroupName = "基本信息", Order = 4)]
        public DateTime BirthDate { get; set; }

        [Display(Name = "薪资", GroupName = "财务", Order = 1)]
        public decimal Salary { get; set; }

        [Display(Name = "标签", GroupName = "扩展信息")]
        [CollectionEditor(ItemType = typeof(string))]
        public List<string> Tags { get; set; }

        [Display(Name = "地址", GroupName = "联系方式", Order = 2)]
        [System.ComponentModel.ReadOnly(true)]
        public Address Address { get; set; }

        [System.ComponentModel.Browsable(false)]
        public string InternalId { get; set; } = Guid.NewGuid().ToString();
    }

    public class Address
    {
        public string City { get; set; }
        public string Street { get; set; }

        public override string ToString()
        {
            return $"{City}, {Street}";
        }
    }

    public enum Gender
    {
        [Description("男性")]
        Male,
        [Description("女性")]
        Female,
        [Description("其他")]
        Other
    }

    public class AppConfig
    {
        [Display(Name = "主题", GroupName = "外观", Order = 1)]
        public Theme Theme { get; set; }

        [Display(Name = "语言", GroupName = "区域", Order = 1)]
        public string Language { get; set; }

        [Display(Name = "自动保存", GroupName = "行为", Order = 1)]
        public bool AutoSave { get; set; }

        [Display(Name = "保存间隔(秒)", GroupName = "行为", Order = 2)]
        public int SaveInterval { get; set; }

        [Display(Name = "最大历史记录", GroupName = "高级", Order = 1)]
        public int MaxHistory { get; set; }
    }

    public enum Theme
    {
        Light,
        Dark,
        Auto
    }
}
