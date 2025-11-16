using Astra.Core.Access;
using Astra.Core.Access.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Astra.Converters
{
    /// <summary>
    /// 判断用户是否为管理员的转换器
    /// </summary>
    public class IsAdministratorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is User user)
            {
                bool isAdmin = user.Role == UserRole.Administrator;
                
                // 如果目标类型是Visibility
                if (targetType == typeof(Visibility))
                {
                    return isAdmin ? Visibility.Visible : Visibility.Collapsed;
                }
                
                // 否则返回bool
                return isAdmin;
            }
            
            if (targetType == typeof(Visibility))
            {
                return Visibility.Collapsed;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

