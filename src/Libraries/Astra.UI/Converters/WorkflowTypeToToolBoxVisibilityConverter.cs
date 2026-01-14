using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Astra.Core.Nodes.Models;
using Astra.UI.Models;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 根据流程类型返回工具箱可见性
    /// 主流程：隐藏工具箱（false）
    /// 子流程：显示工具箱（true）
    /// </summary>
    public class WorkflowTypeToToolBoxVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool shouldShow = false; // 默认隐藏
            
            // 判断流程类型
            if (value is WorkflowType workflowType)
            {
                // 主流程隐藏工具箱，子流程显示工具箱
                shouldShow = workflowType == WorkflowType.Sub;
            }
            // 如果 value 是 WorkflowTab，获取其 Type
            else if (value is WorkflowTab tab)
            {
                // 主流程隐藏工具箱，子流程显示工具箱
                shouldShow = tab.Type == WorkflowType.Sub;
            }
            
            // 如果目标类型是 bool，返回 bool 值
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                return shouldShow; // true 表示显示（IsToolBoxVisible = true）
            }
            
            // 如果目标类型是 Visibility，返回 Visibility 值
            if (targetType == typeof(Visibility) || targetType == typeof(Visibility?))
            {
                return shouldShow ? Visibility.Visible : Visibility.Collapsed;
            }
            
            // 默认返回 bool（根据流程类型）
            return shouldShow;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}



