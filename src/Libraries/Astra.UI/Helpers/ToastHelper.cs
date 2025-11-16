using Astra.UI.Styles.Controls;

namespace Astra.UI.Helpers
{
    /// <summary>
    /// Toast 通知帮助类 - 轻量级浮动提示
    /// </summary>
    public static class ToastHelper
    {
        /// <summary>
        /// 显示信息提示
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（默认："提示"）</param>
        /// <param name="duration">显示时长（秒，默认：3）</param>
        public static void ShowInfo(string message, string title = "提示", int duration = 3)
        {
            ToastNotification.ShowInfo(message, title, duration);
        }

        /// <summary>
        /// 显示成功提示
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（默认："成功"）</param>
        /// <param name="duration">显示时长（秒，默认：3）</param>
        public static void ShowSuccess(string message, string title = "成功", int duration = 3)
        {
            ToastNotification.ShowSuccess(message, title, duration);
        }

        /// <summary>
        /// 显示警告提示
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（默认："警告"）</param>
        /// <param name="duration">显示时长（秒，默认：3）</param>
        public static void ShowWarning(string message, string title = "警告", int duration = 3)
        {
            ToastNotification.ShowWarning(message, title, duration);
        }

        /// <summary>
        /// 显示错误提示
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（默认："错误"）</param>
        /// <param name="duration">显示时长（秒，默认：4）</param>
        public static void ShowError(string message, string title = "错误", int duration = 4)
        {
            ToastNotification.ShowError(message, title, duration);
        }

        /// <summary>
        /// 显示登录成功提示
        /// </summary>
        public static void ShowLoginSuccess(string username)
        {
            ShowSuccess($"欢迎回来，{username}！", "登录成功");
        }

        /// <summary>
        /// 显示登出提示
        /// </summary>
        public static void ShowLogoutSuccess()
        {
            ShowInfo("您已安全退出", "已登出");
        }

        /// <summary>
        /// 显示用户切换提示
        /// </summary>
        public static void ShowUserSwitched(string username)
        {
            ShowSuccess($"已切换到用户：{username}", "切换成功");
        }

        /// <summary>
        /// 显示保存成功提示
        /// </summary>
        public static void ShowSaveSuccess()
        {
            ShowSuccess("数据已保存", "保存成功");
        }

        /// <summary>
        /// 显示删除成功提示
        /// </summary>
        public static void ShowDeleteSuccess(string itemName = "项目")
        {
            ShowSuccess($"{itemName}已删除", "删除成功");
        }

        /// <summary>
        /// 显示操作成功提示
        /// </summary>
        public static void ShowOperationSuccess(string operation)
        {
            ShowSuccess($"{operation}已完成", "操作成功");
        }

        /// <summary>
        /// 显示测试Toast（用于调试定位问题）
        /// </summary>
        public static void ShowTestToast()
        {
            ShowWarning("这是一个测试Toast，用于验证定位是否正确", "测试定位");
        }

        /// <summary>
        /// 显示多个测试Toast
        /// </summary>
        public static void ShowMultipleTestToasts()
        {
            ShowWarning("Toast消息 1", "测试1");
            System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
            {
                ShowWarning("Toast消息 2", "测试2");
            });
            System.Threading.Tasks.Task.Delay(400).ContinueWith(_ =>
            {
                ShowWarning("Toast消息 3", "测试3");
            });
        }
    }
}
