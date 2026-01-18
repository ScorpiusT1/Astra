using System;
using System.Diagnostics;

namespace Astra.UI.Logging
{
    /// <summary>
    /// 默认属性编辑器日志实现
    /// 使用 System.Diagnostics.Debug 作为输出
    /// </summary>
    internal class DefaultPropertyEditorLogger : IPropertyEditorLogger
    {
        private const string LogPrefix = "[PropertyEditor]";

        public void Debug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"{LogPrefix} [DEBUG] {message}");
        }

        public void Info(string message)
        {
            System.Diagnostics.Debug.WriteLine($"{LogPrefix} [INFO] {message}");
        }

        public void Warn(string message)
        {
            System.Diagnostics.Debug.WriteLine($"{LogPrefix} [WARN] {message}");
        }

        public void Error(string message, Exception exception = null)
        {
            var errorMessage = $"{LogPrefix} [ERROR] {message}";
            if (exception != null)
            {
                errorMessage += $"\n异常: {exception.GetType().Name}\n消息: {exception.Message}";
                if (exception.InnerException != null)
                {
                    errorMessage += $"\n内部异常: {exception.InnerException.Message}";
                }
            }
            System.Diagnostics.Debug.WriteLine(errorMessage);
        }
    }
}

