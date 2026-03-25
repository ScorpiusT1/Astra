using Astra.Core.Nodes.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 拼接节点 ToolTip：显示节点名 + 最近一次执行结果 Message（如 Skipped/Failed 原因）。
    /// </summary>
    public sealed class NodeExecutionResultToTooltipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string title = values != null && values.Length > 0 ? values[0] as string : null;
            string message = values != null && values.Length > 1 ? values[1] as string : null;

            // 第3个输入用于判断是否是 Skipped（可选）
            NodeExecutionState state = NodeExecutionState.Idle;
            if (values != null && values.Length > 2 && values[2] is NodeExecutionState s)
            {
                state = s;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = "节点";
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return title;
            }

            if (state == NodeExecutionState.Skipped)
            {
                return $"{title}{Environment.NewLine}跳过原因：{message}";
            }

            // 其他终态/运行中也尽量展示 message（例如 Failed 的错误原因）
            return $"{title}{Environment.NewLine}{message}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

