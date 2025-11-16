using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Astra.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<FeatureCard> _featureCards;

        public HomeViewModel()
        {
            InitializeFeatureCards();
        }

        private void InitializeFeatureCards()
        {
            FeatureCards = new ObservableCollection<FeatureCard>
            {
                new FeatureCard
                {
                    Icon = "📋",
                    Title = "序列配置",
                    Description = "配置和管理测试序列，支持复杂的测试流程编排"
                },
                new FeatureCard
                {
                    Icon = "👥",
                    Title = "权限管理",
                    Description = "管理用户权限和角色，确保系统安全访问"
                },
                new FeatureCard
                {
                    Icon = "⚙️",
                    Title = "配置管理",
                    Description = "系统配置和参数设置，个性化定制测试环境"
                },
                new FeatureCard
                {
                    Icon = "🔧",
                    Title = "调试工具",
                    Description = "强大的调试和诊断工具，快速定位问题"
                },
                new FeatureCard
                {
                    Icon = "📊",
                    Title = "测试报告",
                    Description = "详细的测试报告和数据分析，洞察测试结果"
                },
                new FeatureCard
                {
                    Icon = "🚀",
                    Title = "性能监控",
                    Description = "实时监控系统性能，确保测试环境稳定"
                }
            };
        }
    }

    public partial class FeatureCard : ObservableObject
    {
        [ObservableProperty]
        private string _icon;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _description;
    }
}
