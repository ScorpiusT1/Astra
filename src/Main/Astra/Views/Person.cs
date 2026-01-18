using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Interfaces;
using EditorAttribute = Astra.UI.Abstractions.Attributes.EditorAttribute;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Astra.UI.PropertyEditors;

namespace Astra.Views
{
    // ==================== 测试数据模型 ====================

    public class Person : System.ComponentModel.INotifyPropertyChanged, IPropertyVisibilityProvider
    {
        private bool _hideAge;
        private bool _hideBasicInfoGroup;
        private bool _hideIsActive;
        private string _name;
        private int _age;
        private DateTime _birthDate;

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

        [Display(Name = "技能", GroupName = "扩展信息", Order = 2)]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(typeof(Astra.Views.SkillProvider), "GetAllSkills",DisplayMemberPath = "Name")]
       
        public ObservableCollection<Skill> Skills { get; set; }

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

        [Display(Name = "出生日期", GroupName = "基本信息", Order = 4, Description = "用户的出生日期")]
        public DateTime BirthDate
        {
            get => _birthDate;
            set
            {
                if (_birthDate != value)
                {
                    _birthDate = value;
                    OnPropertyChanged();
                }
            }
        }

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
}
