using Astra.Core.Access;
using Astra.Core.Access.Models;
using Astra.Core.Access.Services;
using Astra.UI.Helpers;
using Astra.UI.Styles.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;

namespace Astra.Views
{
    /// <summary>
    /// LoginDialog.xaml 的交互逻辑
    /// </summary>
    public partial class LoginDialog : Window
    {
        public static readonly DependencyProperty AvailableUsersProperty =
            DependencyProperty.Register(nameof(AvailableUsers), typeof(ObservableCollection<User>),
                typeof(LoginDialog), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedUserProperty =
            DependencyProperty.Register(nameof(SelectedUser), typeof(User),
                typeof(LoginDialog), new PropertyMetadata(null));

        /// <summary>
        /// 可用用户列表
        /// </summary>
        public ObservableCollection<User> AvailableUsers
        {
            get => (ObservableCollection<User>)GetValue(AvailableUsersProperty);
            set => SetValue(AvailableUsersProperty, value);
        }

        /// <summary>
        /// 选中的用户
        /// </summary>
        public User SelectedUser
        {
            get => (User)GetValue(SelectedUserProperty);
            set => SetValue(SelectedUserProperty, value);
        }

        /// <summary>
        /// 登录成功的用户
        /// </summary>
        public User LoggedInUser { get; private set; }

        /// <summary>
        /// 用户管理服务
        /// </summary>
        private readonly IUserManagementService _userManagementService;

        /// <summary>
        /// 设置默认选择的用户
        /// </summary>
        public void SetDefaultUser(User? user)
        {
            if (user != null && AvailableUsers != null)
            {
                // 确保用户存在于可用用户列表中
                var existingUser = AvailableUsers.FirstOrDefault(u => u.Username == user.Username);

                if (existingUser != null)
                {
                    SelectedUser = existingUser;

                    // 强制更新UI
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UserComboBox.SelectedItem = existingUser;
                        UserComboBox.UpdateLayout();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            else
            {
                if (AvailableUsers == null || AvailableUsers.Count == 0)
                {
                    return;
                }

                var existingUser = AvailableUsers?.FirstOrDefault(u=>u.Role == UserRole.Operator);

                if(existingUser == null)
                {
                    return;
                }

                SelectedUser = existingUser;

                // 强制更新UI
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UserComboBox.SelectedItem = existingUser;
                    UserComboBox.UpdateLayout();
                }), System.Windows.Threading.DispatcherPriority.Loaded);

            }
        }

        public LoginDialog(IUserManagementService userManagementService = null)
        {
            InitializeComponent();
            _userManagementService = userManagementService;

            // ⭐ 确保UI完全加载后再设置焦点
            Loaded += (s, e) =>
            {
                PasswordInput.Focus();
            };
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedUser == null)
                {
                    ToastHelper.ShowWarning("请选择用户");
                    return;
                }

                var password = PasswordInput.Password;
                if (string.IsNullOrWhiteSpace(password))
                {
                    ErrorText.Text = "密码不能为空";
                    ErrorText.Visibility = Visibility.Visible;
                    TryShake();
                    PasswordInput.Focus();
                    return;
                }

                // 验证密码
                if (_userManagementService != null)
                {
                    try
                    {
                        LoggedInUser = _userManagementService.Login(SelectedUser.Username, password);
                        DialogResult = true;
                        Close();
                    }
                    catch (Exception ex)
                    {
                        ErrorText.Text = $"登录失败：{ex.Message}";
                        ErrorText.Visibility = Visibility.Visible;
                        PasswordInput.Clear();
                        PasswordInput.Focus();
                        TryShake();
                    }
                }
                else
                {
                    // 模拟模式 - 假设密码正确
                    LoggedInUser = SelectedUser;
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"登录失败：{ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
                TryShake();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            LoggedInUser = null;
            DialogResult = false;
            Close();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == System.Windows.Input.Key.Enter)
            {
                LoginButton_Click(this, null);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                CancelButton_Click(this, null);
            }
        }

        private void TryShake()
        {
            try
            {
                if (FindResource("ShakeStoryboard") is Storyboard storyboard)
                {
                    storyboard.Begin(RootBorder);
                }
            }
            catch
            {
                // ignore animation failures
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
            LoggedInUser = null;
            DialogResult = false;
            Close();
        }
    }
}

