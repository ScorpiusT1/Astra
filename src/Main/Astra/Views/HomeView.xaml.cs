using Astra.UI.Controls;
using HandyControl.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                Address = new Address { City = "北京", Street = "中关村大街1号" },
                Skills = new ObservableCollection<Skill>(
                    SkillProvider.GetAllSkills().Where(s => s.Id == 1 || s.Id == 3).ToList())
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

    /// <summary>
    /// 技能定义
    /// </summary>
    public class Skill
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }

        public override bool Equals(object obj)
        {
            return obj is Skill skill && Id == skill.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// 技能数据提供者
    /// </summary>
    public static class SkillProvider
    {
        private static readonly List<Skill> _allSkills = new List<Skill>
        {
            new Skill { Id = 1, Name = "C#", Category = "编程语言", Description = "C# 编程语言" },
            new Skill { Id = 2, Name = "Java", Category = "编程语言", Description = "Java 编程语言" },
            new Skill { Id = 3, Name = "Python", Category = "编程语言", Description = "Python 编程语言" },
            new Skill { Id = 4, Name = "JavaScript", Category = "编程语言", Description = "JavaScript 编程语言" },
            new Skill { Id = 5, Name = "WPF", Category = "框架", Description = "Windows Presentation Foundation" },
            new Skill { Id = 6, Name = "ASP.NET", Category = "框架", Description = "ASP.NET 框架" },
            new Skill { Id = 7, Name = "SQL", Category = "数据库", Description = "SQL 数据库查询" },
            new Skill { Id = 8, Name = "MySQL", Category = "数据库", Description = "MySQL 数据库" },
            new Skill { Id = 9, Name = "Git", Category = "工具", Description = "Git 版本控制" },
            new Skill { Id = 10, Name = "Docker", Category = "工具", Description = "Docker 容器化" }
        };

        /// <summary>
        /// 获取所有可用技能列表（供 ItemsSource 使用）
        /// </summary>
        public static IEnumerable<Skill> GetAllSkills()
        {
            return _allSkills;
        }
    }
}
