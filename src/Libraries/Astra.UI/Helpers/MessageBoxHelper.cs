using Astra.UI.Styles.Controls;
using System.Windows;

namespace Astra.UI.Helpers
{
    /// <summary>
    /// 消息框助手类 - 提供便捷的静态方法
    /// </summary>
    public static class MessageBoxHelper
    {
        /// <summary>
        /// 显示信息消息
        /// </summary>
        public static void ShowInfo(string message, string title = "提示")
        {
            ModernMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 显示警告消息
        /// </summary>
        public static void ShowWarning(string message, string title = "警告")
        {
            ModernMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        public static void ShowError(string message, string title = "错误")
        {
            ModernMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// 显示成功消息（使用信息图标）
        /// </summary>
        public static void ShowSuccess(string message, string title = "成功")
        {
            ModernMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 显示确认对话框（是/否）
        /// </summary>
        public static bool Confirm(string message, string title = "确认")
        {
            var result = ModernMessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        /// <summary>
        /// 显示确认对话框（确定/取消）
        /// </summary>
        public static bool ConfirmOkCancel(string message, string title = "确认")
        {
            var result = ModernMessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            return result == MessageBoxResult.OK;
        }

        /// <summary>
        /// 显示带有三个按钮的确认对话框
        /// </summary>
        public static MessageBoxResult ConfirmYesNoCancel(string message, string title = "确认")
        {
            return ModernMessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        }

        /// <summary>
        /// 显示删除确认对话框
        /// </summary>
        public static bool ConfirmDelete(string itemName = "此项")
        {
            return Confirm(
                $"确定要删除 {itemName} 吗？\n\n此操作无法撤销。",
                "确认删除"
            );
        }

        /// <summary>
        /// 显示保存确认对话框
        /// </summary>
        public static MessageBoxResult ConfirmSave(string message = "是否保存更改？")
        {
            return ModernMessageBox.Show(
                message,
                "保存确认",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );
        }
    }
}
