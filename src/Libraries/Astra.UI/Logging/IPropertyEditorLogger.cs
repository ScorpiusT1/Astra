namespace Astra.UI.Logging
{
    /// <summary>
    /// 属性编辑器日志接口
    /// 提供统一的日志记录接口，允许外部注入日志实现
    /// </summary>
    public interface IPropertyEditorLogger
    {
        /// <summary>
        /// 记录调试信息
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// 记录信息
        /// </summary>
        void Info(string message);

        /// <summary>
        /// 记录警告
        /// </summary>
        void Warn(string message);

        /// <summary>
        /// 记录错误
        /// </summary>
        void Error(string message, System.Exception exception = null);
    }
}

