using Astra.Core.Nodes.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// ExecutionResult 到执行时间字符串的转换器
    /// </summary>
    public class ExecutionResultToTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ExecutionResult result)
            {
                if (result.Duration.HasValue)
                {
                    var duration = result.Duration.Value;
                    if (duration.TotalSeconds < 1)
                    {
                        return $"{duration.TotalMilliseconds:F0} ms";
                    }
                    else if (duration.TotalSeconds < 60)
                    {
                        return $"{duration.TotalSeconds:F2} s";
                    }
                    else if (duration.TotalMinutes < 60)
                    {
                        return $"{duration.TotalMinutes:F2} min";
                    }
                    else
                    {
                        return $"{duration.TotalHours:F2} h";
                    }
                }
            }

            return "0 s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

