using System.Windows;
using System.Windows.Controls;

namespace Astra.UI.Helpers
{
    public static class PasswordHelper
    {
        // 用于标记是否正在更新，防止循环更新
        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(PasswordHelper));

        // 附加属性：用于绑定
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.RegisterAttached(
                "Password",
                typeof(string),
                typeof(PasswordHelper),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnPasswordPropertyChanged));

        // 附加属性：用于启用绑定
        public static readonly DependencyProperty AttachProperty =
            DependencyProperty.RegisterAttached(
                "Attach",
                typeof(bool),
                typeof(PasswordHelper),
                new PropertyMetadata(false, OnAttachChanged));

        // 获取Password属性
        public static string GetPassword(DependencyObject dp)
        {
            return (string)dp.GetValue(PasswordProperty);
        }

        // 设置Password属性
        public static void SetPassword(DependencyObject dp, string value)
        {
            dp.SetValue(PasswordProperty, value);
        }

        // 获取Attach属性
        public static bool GetAttach(DependencyObject dp)
        {
            return (bool)dp.GetValue(AttachProperty);
        }

        // 设置Attach属性
        public static void SetAttach(DependencyObject dp, bool value)
        {
            dp.SetValue(AttachProperty, value);
        }

        // 获取IsUpdating标记
        private static bool GetIsUpdating(DependencyObject dp)
        {
            return (bool)dp.GetValue(IsUpdatingProperty);
        }

        // 设置IsUpdating标记
        private static void SetIsUpdating(DependencyObject dp, bool value)
        {
            dp.SetValue(IsUpdatingProperty, value);
        }

        // 当Attach属性改变时
        private static void OnAttachChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                if ((bool)e.OldValue)
                {
                    // 取消订阅
                    passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
                }

                if ((bool)e.NewValue)
                {
                    // 订阅事件
                    passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
                }
            }
        }

        // 当PasswordBox的密码改变时（用户输入）
        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox == null)
                return;

            // 如果正在更新，跳过（防止循环）
            if (GetIsUpdating(passwordBox))
                return;

            // 设置更新标记
            SetIsUpdating(passwordBox, true);

            // 更新附加属性（这会触发ViewModel的属性更新）
            SetPassword(passwordBox, passwordBox.Password);

            // 清除更新标记
            SetIsUpdating(passwordBox, false);
        }

        // 当Password附加属性改变时（ViewModel更新）
        private static void OnPasswordPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox == null)
                return;

            // 如果正在更新，跳过（防止循环）
            if (GetIsUpdating(passwordBox))
                return;

            // 获取新密码
            var newPassword = (string)e.NewValue;

            // 如果密码不同，更新PasswordBox
            if (passwordBox.Password != newPassword)
            {
                // 设置更新标记
                SetIsUpdating(passwordBox, true);

                // 更新PasswordBox的密码
                passwordBox.Password = newPassword ?? string.Empty;

                // 清除更新标记
                SetIsUpdating(passwordBox, false);
            }
        }
    }
}
