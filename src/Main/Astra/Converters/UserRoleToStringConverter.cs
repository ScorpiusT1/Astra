using System;
using System.Globalization;
using System.Windows.Data;
using Astra.Core.Access;
using Astra.Core.Access.Models;

namespace Astra.Converters
{
    public class UserRoleToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserRole role)
            {
                return role switch
                {
                    UserRole.SuperAdministrator => "超级管理员",
                    UserRole.Operator => "操作员",
                    UserRole.Engineer => "工程师",
                    UserRole.Administrator => "管理员",
                    _ => "未知"
                };
            }
            return "未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string roleString)
            {
                return roleString switch
                {
                    "超级管理员" => UserRole.SuperAdministrator,
                    "操作员" => UserRole.Operator,
                    "工程师" => UserRole.Engineer,
                    "管理员" => UserRole.Administrator,
                    _ => UserRole.Operator
                };
            }
            return UserRole.Operator;
        }
    }
}
