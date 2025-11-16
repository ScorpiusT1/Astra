using Astra.Core.Access.Services;
using Astra.UI.Helpers;
using System.Windows;

namespace Astra.Views
{
    /// <summary>
    /// ChangePasswordDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ChangePasswordDialog : Window
    {
        public static readonly DependencyProperty UsernameProperty =
            DependencyProperty.Register(nameof(Username), typeof(string), typeof(ChangePasswordDialog),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// 用户名
        /// </summary>
        public string Username
        {
            get => (string)GetValue(UsernameProperty);
            set => SetValue(UsernameProperty, value);
        }

        /// <summary>
        /// 旧密码
        /// </summary>
        public string OldPassword { get; private set; }

        /// <summary>
        /// 新密码
        /// </summary>
        public string NewPassword { get; private set; }

        private readonly IUserManagementService _userManagementService;

        public ChangePasswordDialog(IUserManagementService userManagementService = null)
        {
            InitializeComponent();
            _userManagementService = userManagementService;
            OldPasswordInput.Focus();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证旧密码
            OldPassword = OldPasswordInput.Password;

            if (string.IsNullOrWhiteSpace(OldPassword))
            {
                ToastHelper.ShowError("请输入当前密码");
                //MessageBox.Show("请输入当前密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                OldPasswordInput.Focus();
                return;
            }

            // 使用用户服务验证旧密码是否正确（如果可用）
            if (_userManagementService != null && !string.IsNullOrWhiteSpace(Username))
            {
                try
                {
                    // 尝试登录以校验旧密码
                    _userManagementService.Login(Username, OldPassword);
                }
                catch
                {
                    //MessageBox.Show("当前密码不正确", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ToastHelper.ShowError("当前密码不正确");
                    OldPasswordInput.Clear();
                    OldPasswordInput.Focus();
                    return;
                }
            }


            // 验证新密码
            NewPassword = NewPasswordInput.Password;

            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                //MessageBox.Show("请输入新密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                ToastHelper.ShowInfo("请输入新密码");
                NewPasswordInput.Focus();
                return;
            }

            if (NewPassword.Length < 6)
            {
                ToastHelper.ShowWarning("新密码长度至少6位");
                NewPasswordInput.Focus();
                return;
            }

            // 验证确认密码
            var confirmPassword = ConfirmPasswordInput.Password;

            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                ToastHelper.ShowWarning("请确认新密码");
                ConfirmPasswordInput.Focus();
                return;
            }

            if (NewPassword != confirmPassword)
            {               
                ToastHelper.ShowWarning("两次输入的密码不一致");
                NewPasswordInput.Clear();
                ConfirmPasswordInput.Clear();
                NewPasswordInput.Focus();
                return;
            }

            // 验证新旧密码不能相同
            if (OldPassword == NewPassword)
            {              
                ToastHelper.ShowWarning("新密码不能与当前密码相同");
                NewPasswordInput.Clear();
                ConfirmPasswordInput.Clear();
                NewPasswordInput.Focus();
                return;
            }

            // 所有验证通过
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            OldPassword = null;
            NewPassword = null;
            DialogResult = false;
            Close();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // ⭐ 支持回车键确认
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ConfirmButton_Click(this, null);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                CancelButton_Click(this, null);
            }
        }

        /// <summary>
        /// 窗口拖动处理
        /// </summary>
        private void RootBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            OldPassword = null;
            NewPassword = null;
            DialogResult = false;
            Close();
        }
    }
}

