using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Astra.Converters
{
    /// <summary>
    /// 登录状态到可见性的转换器
    /// </summary>
    public class LoginStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isLoggedIn = value is bool loggedIn && loggedIn;
            string param = parameter?.ToString() ?? "LoggedIn";

            if (param == "LoggedIn")
            {
                return isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
            }
            else // "NotLoggedIn"
            {
                return isLoggedIn ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

