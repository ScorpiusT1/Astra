using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Astra.Core.Access;
using Astra.Core.Access.Models;

namespace Astra.Converters
{
    /// <summary>
    /// 用户权限到颜色的转换器
    /// 用于将不同的用户权限级别转换为对应的主题颜色
    /// 权限与颜色的映射关系:
    /// - 管理员: 使用 Danger 色系 (红色)
    /// - 工程师: 使用 Warn 色系 (橙色)
    /// - 操作员: 使用 Info 色系 (青色)
    /// </summary>
    public class UserRoleToColorConverter : IValueConverter
    {
        /// <summary>
        /// 将UserRole转换为颜色刷（使用主题资源）
        /// </summary>
        /// <param name="parameter">
        /// 转换器参数:
        /// - "Light": 返回浅色背景颜色 (Light*Brush)
        /// - "Dark": 返回深色前景颜色 (Dark*Brush)
        /// - 默认: 返回浅色背景颜色
        /// </param>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserRole role)
            {
                string parameterType = parameter?.ToString() ?? "Light";
                string resourceKey = GetResourceKey(role, parameterType);
                
                try
                {
                    // 尝试从应用资源中获取Brush
                    var resource = Application.Current?.Resources[resourceKey];
                    if (resource is Brush brush)
                    {
                        return brush;
                    }
                }
                catch
                {
                    // 如果获取失败，返回透明色
                }
            }

            return new SolidColorBrush(Colors.Transparent);
        }

        /// <summary>
        /// 根据权限等级和参数类型获取对应的资源键
        /// </summary>
        private static string GetResourceKey(UserRole role, string parameterType)
        {
            return role switch
            {
                // 管理员: 使用 Danger 色系 (红色) - 最高权限
                UserRole.Administrator => parameterType == "Dark" 
                    ? "DarkDangerBrush"      // 深红色文字
                    : "LightDangerBrush",    // 浅红色背景
                
                // 工程师: 使用 Warn 色系 (橙色) - 中级权限
                UserRole.Engineer => parameterType == "Dark"
                    ? "DarkWarningBrush"     // 深橙色文字
                    : "LightWarningBrush",   // 浅橙色背景
                
                // 操作员: 使用 Info 色系 (青色) - 最低权限
                UserRole.Operator => parameterType == "Dark"
                    ? "DarkInfoBrush"        // 深青色文字
                    : "LightInfoBrush",      // 浅青色背景
                
                // 未知权限: 使用 Default 色系 (灰色)
                _ => parameterType == "Dark"
                    ? "DarkDefaultBrush"     // 深灰色文字
                    : "DefaultBrush"         // 浅灰色背景
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
