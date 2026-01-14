using Astra.Core.Nodes.Models;
using Astra.UI.Models;
using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;


namespace Astra.UI.Converters
{
    /// <summary>
    /// 根据流程类型返回不同的模板选择器
    /// 主流程：使用包含 WorkflowReferenceNodeTemplate 的模板选择器
    /// 子流程：使用只包含 DefaultNodeTemplate 的模板选择器（或 null，使用默认模板）
    /// </summary>
    public class WorkflowTypeToTemplateSelectorConverter : IValueConverter
    {
        /// <summary>
        /// 主流程模板选择器（包含 WorkflowReferenceNode 特殊样式）
        /// </summary>
        public DataTemplateSelector MasterTemplateSelector { get; set; }

        /// <summary>
        /// 子流程模板选择器（只使用默认模板）
        /// </summary>
        public DataTemplateSelector SubTemplateSelector { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkflowType workflowType)
            {
                return workflowType == WorkflowType.Master 
                    ? MasterTemplateSelector 
                    : SubTemplateSelector;
            }
            
            // 如果 value 是 WorkflowTab，获取其 Type
            if (value is WorkflowTab tab)
            {
                return tab.Type == WorkflowType.Master 
                    ? MasterTemplateSelector 
                    : SubTemplateSelector;
            }
            
            // 默认返回子流程模板选择器
            return SubTemplateSelector;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

