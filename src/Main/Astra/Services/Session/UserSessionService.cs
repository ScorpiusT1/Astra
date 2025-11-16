using Astra.Core.Access;
using Astra.Core.Access.Models;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Windows.Threading;

namespace Astra.Services.Session
{
    /// <summary>
    /// 用户会话状态变更消息
    /// </summary>
    public class UserSessionChangedMessage
    {
        public User CurrentUser { get; set; }
        public bool IsLoggedIn { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// 自动登出警告消息
    /// </summary>
    public class AutoLogoutWarningMessage
    {
        public int RemainingSeconds { get; set; }
    }

    /// <summary>
    /// 用户会话管理服务接口
    /// </summary>
    public interface IUserSessionService
    {
        /// <summary>
        /// 当前用户
        /// </summary>
        User CurrentUser { get; }

        /// <summary>
        /// 是否已登录
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// 是否为管理员
        /// </summary>
        bool IsAdministrator { get; }

        /// <summary>
        /// 登录用户
        /// </summary>
        void Login(User user);

        /// <summary>
        /// 登出当前用户
        /// </summary>
        void Logout(string reason = "用户主动登出");

        /// <summary>
        /// 更新用户活动时间
        /// </summary>
        void UpdateActivity();

        /// <summary>
        /// 配置自动登出
        /// </summary>
        void ConfigureAutoLogout(bool enabled, int timeoutMinutes = 5);
    }

    /// <summary>
    /// 用户会话管理服务实现 - 单例模式
    /// </summary>
    public class UserSessionService : IUserSessionService
    {
        private readonly IMessenger _messenger;
        private User _currentUser;
        private DateTime _lastActivityTime;
        private DispatcherTimer _autoLogoutTimer;
        private bool _autoLogoutEnabled;
        private int _autoLogoutMinutes;
        private const int WARNING_SECONDS = 30;

        public User CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser != null;
        public bool IsAdministrator => _currentUser?.Role == UserRole.Administrator;

        public UserSessionService(IMessenger messenger = null)
        {
            _messenger = messenger ?? WeakReferenceMessenger.Default;
            _lastActivityTime = DateTime.Now;
            InitializeAutoLogoutTimer();
        }

        public void Login(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            _currentUser = user;
            UpdateActivity();
            
            // 发送会话变更消息
            _messenger.Send(new UserSessionChangedMessage
            {
                CurrentUser = user,
                IsLoggedIn = true,
                Reason = "用户登录"
            });

            // 根据用户角色管理自动登出
            ManageAutoLogout();

            System.Diagnostics.Debug.WriteLine($"[UserSessionService] 用户登录: {user.Username}, 角色: {user.Role}");
        }

        public void Logout(string reason = "用户主动登出")
        {
            if (_currentUser == null)
                return;

            var loggedOutUser = _currentUser;
            _currentUser = null;

            // 停止自动登出计时器
            _autoLogoutTimer?.Stop();

            // 发送会话变更消息
            _messenger.Send(new UserSessionChangedMessage
            {
                CurrentUser = null,
                IsLoggedIn = false,
                Reason = reason
            });

            System.Diagnostics.Debug.WriteLine($"[UserSessionService] 用户登出: {loggedOutUser.Username}, 原因: {reason}");
        }

        public void UpdateActivity()
        {
            _lastActivityTime = DateTime.Now;
            System.Diagnostics.Debug.WriteLine($"[UserSessionService] 更新活动时间: {_lastActivityTime:HH:mm:ss}");
        }

        public void ConfigureAutoLogout(bool enabled, int timeoutMinutes = 5)
        {
            _autoLogoutEnabled = enabled;
            _autoLogoutMinutes = timeoutMinutes;
            ManageAutoLogout();
        }

        private void InitializeAutoLogoutTimer()
        {
            _autoLogoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoLogoutTimer.Tick += OnAutoLogoutTimerTick;
        }

        private void ManageAutoLogout()
        {
            if (_autoLogoutTimer == null)
                return;

            // 只有管理员且启用自动登出时才启动计时器
            if (IsAdministrator && _autoLogoutEnabled)
            {
                if (!_autoLogoutTimer.IsEnabled)
                {
                    _autoLogoutTimer.Start();
                    System.Diagnostics.Debug.WriteLine("[UserSessionService] 启动自动登出计时器");
                }
            }
            else
            {
                if (_autoLogoutTimer.IsEnabled)
                {
                    _autoLogoutTimer.Stop();
                    System.Diagnostics.Debug.WriteLine("[UserSessionService] 停止自动登出计时器");
                }
            }
        }

        private void OnAutoLogoutTimerTick(object sender, EventArgs e)
        {
            if (!IsAdministrator || !_autoLogoutEnabled)
            {
                ManageAutoLogout();
                return;
            }

            var elapsed = DateTime.Now - _lastActivityTime;
            var remainingSeconds = (int)(TimeSpan.FromMinutes(_autoLogoutMinutes) - elapsed).TotalSeconds;

            if (remainingSeconds <= 0)
            {
                // 时间到,自动登出
                AutoLogoutToOperator();
            }
            else if (remainingSeconds <= WARNING_SECONDS)
            {
                // 发送警告消息
                _messenger.Send(new AutoLogoutWarningMessage
                {
                    RemainingSeconds = remainingSeconds
                });
            }
        }

        private void AutoLogoutToOperator()
        {
            System.Diagnostics.Debug.WriteLine("[UserSessionService] 执行自动登出");
            
            // 停止计时器
            _autoLogoutTimer?.Stop();

            // 登出当前管理员
            Logout("管理员权限超时自动登出");

            // 这里可以自动切换到默认操作员
            // 具体实现取决于业务需求
        }
    }
}
