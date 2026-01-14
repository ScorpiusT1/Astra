using System;
using System.Globalization;
using System.Windows.Data;
using Astra.Core.Nodes.Models;
using Astra.UI.Models;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 根据流程类型返回工具箱宽度
    /// 主流程：0（隐藏工具箱）
    /// 子流程：90（显示工具箱）
    /// </summary>
    public class WorkflowTypeToToolBoxWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkflowType workflowType)
            {
                return workflowType == WorkflowType.Master ? 0.0 : 90.0;
            }
            
            // 如果 value 是 WorkflowTab，获取其 Type
            if (value is WorkflowTab tab)
            {
                return tab.Type == WorkflowType.Master ? 0.0 : 90.0;
            }
            
            // 默认返回 90（显示工具箱）
            return 90.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

