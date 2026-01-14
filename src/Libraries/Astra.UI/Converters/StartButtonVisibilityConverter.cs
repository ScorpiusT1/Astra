using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Astra.Core.Nodes.Models;
using Astra.UI.Models;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 启动按钮可见性转换器（MultiValueConverter）
    /// 根据流程类型和IsStartButtonVisible属性决定启动按钮的可见性
    /// </summary>
    public class StartButtonVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] 应该是 WorkflowType
            // values[1] 应该是 IsStartButtonVisible (bool)
            
            if (values == null || values.Length < 2)
            {
                return Visibility.Collapsed;
            }

            // 检查流程类型
            WorkflowType? workflowType = null;
            if (values[0] is WorkflowType type)
            {
                workflowType = type;
            }
            else if (values[0] is WorkflowTab tab)
            {
                workflowType = tab.Type;
            }

            // 检查IsStartButtonVisible
            bool isStartButtonVisible = false;
            if (values[1] is bool visible)
            {
                isStartButtonVisible = visible;
            }

            // 只有在子流程且IsStartButtonVisible为true时才显示
            bool shouldShow = workflowType == WorkflowType.Sub && isStartButtonVisible;

            if (targetType == typeof(Visibility) || targetType == typeof(Visibility?))
            {
                return shouldShow ? Visibility.Visible : Visibility.Collapsed;
            }

            return shouldShow;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

