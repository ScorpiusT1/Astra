using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志器核心接口
    /// 提供统一的日志记录接口，支持多种日志级别和分类
    /// 只包含核心的日志记录方法，领域特定的日志功能通过扩展方法提供
    /// </summary>
    public interface ILogger : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 日志事件 - 用于界面更新
        /// 支持多个订阅者
        /// </summary>
        event EventHandler<LogEntryEventArgs> OnLog;

        /// <summary>
        /// 记录 Debug 日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类</param>
        /// <param name="data">附加数据</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        void Debug(string message, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null);

        /// <summary>
        /// 记录 Info 日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类</param>
        /// <param name="data">附加数据</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        void Info(string message, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null);

        /// <summary>
        /// 记录 Warning 日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类</param>
        /// <param name="data">附加数据</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        void Warning(string message, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null);

        /// <summary>
        /// 记录 Error 日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="ex">异常对象</param>
        /// <param name="category">日志分类</param>
        /// <param name="data">附加数据</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        void Error(string message, Exception ex = null, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null);

        /// <summary>
        /// 记录 Critical 日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="ex">异常对象</param>
        /// <param name="category">日志分类</param>
        /// <param name="data">附加数据</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        void Critical(string message, Exception ex = null, LogCategory category = LogCategory.System, Dictionary<string, object> data = null, bool? triggerUIEvent = null);

        /// <summary>
        /// 关闭日志器（异步）
        /// </summary>
        Task ShutdownAsync();
    }
}

