using System;
using System.Globalization;
using System.Windows.Data;
using Astra.Core.Access;
using Astra.Core.Access.Models;

namespace Astra.Converters
{
    /// <summary>
    /// 用户权限到Emoji的转换器
    /// 为不同的权限等级提供视觉区分的图标
    /// </summary>
    public class UserRoleToEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserRole role)
            {
                return role switch
                {
                    UserRole.SuperAdministrator => "👑",  // 超级管理员
                    UserRole.Administrator => "👨‍💻",  // 管理员
                    UserRole.Engineer => "👨‍🔧",      // 工程师
                    UserRole.Operator => "👨‍💼",      // 操作员
                    _ => "👤"                          // 未知用户
                };
            }
            return "👤";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
