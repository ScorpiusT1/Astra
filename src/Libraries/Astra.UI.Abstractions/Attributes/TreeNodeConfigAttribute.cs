using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TreeNodeConfigAttribute : Attribute
    {
        public TreeNodeConfigAttribute(string category, string? icon, Type view, Type ViewModel, int order = -1, string? header = null)
        {
            Category = category;
            Header = header;
            ViewType = view;
            ViewModelType = ViewModel;
            Icon = icon;
            Order = order;
            AllowAddOnRoot = true;
        }

        public string? Header { get; set; }

        public string? Icon { get; set; }

        public Type ViewModelType { get; set; }

        public Type ViewType { get; set; }

        public string Category { get; set; }

        public int Order { get; set; }

        /// <summary>
        /// 根节点是否显示“+”新增按钮，默认 true。
        /// 某些全局配置（如软件配置）可将其设为 false。
        /// </summary>
        public bool AllowAddOnRoot { get; set; }
    }

    /// <summary>
    /// 配置界面 UI 映射特性：仅负责配置类型到 View/ViewModel 的映射，
    /// 不关心树结构（分类、图标等由 TreeNodeConfigAttribute 管理）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ConfigUIAttribute : Attribute
    {
        public ConfigUIAttribute(Type viewType, Type? viewModelType = null)
        {
            ViewType = viewType;
            ViewModelType = viewModelType;
        }

        public Type ViewType { get; }

        public Type? ViewModelType { get; }
    }

    // TreeNodeDebugAttribute 已废弃，调试界面分组改为使用配置类型上的 TreeNodeConfigAttribute
}
