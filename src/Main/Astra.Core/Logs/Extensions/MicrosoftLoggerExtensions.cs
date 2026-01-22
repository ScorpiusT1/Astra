using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using MSLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MSILogger = Microsoft.Extensions.Logging.ILogger;

namespace Astra.Core.Logs.Extensions
{
    /// <summary>
    /// Microsoft.Extensions.Logging.ILogger 扩展方法
    /// 提供与自定义日志系统兼容的扩展方法，支持 LogCategory 等功能
    /// </summary>
    public static class MicrosoftLoggerExtensions
    {
        /// <summary>
        /// 记录 Debug 日志（带分类）
        /// </summary>
        public static void LogDebug(this MSILogger logger, string message, LogCategory category = LogCategory.System, Dictionary<string, object> data = null)
        {
            if (logger == null || !logger.IsEnabled(MSLogLevel.Debug))
                return;

            var state = new Dictionary<string, object>
            {
                ["Category"] = category.ToString(),
                ["Message"] = message
            };

            if (data != null)
            {
                foreach (var kv in data)
                {
                    state[kv.Key] = kv.Value;
                }
            }

            logger.Log(MSLogLevel.Debug, new EventId(0, category.ToString()), state, null, (s, e) => message);
        }

        /// <summary>
        /// 记录 Information 日志（带分类）
        /// </summary>
        public static void LogInfo(this MSILogger logger, string message, LogCategory category = LogCategory.System, Dictionary<string, object> data = null)
        {
            if (logger == null || !logger.IsEnabled(MSLogLevel.Information))
                return;

            var state = new Dictionary<string, object>
            {
                ["Category"] = category.ToString(),
                ["Message"] = message
            };

            if (data != null)
            {
                foreach (var kv in data)
                {
                    state[kv.Key] = kv.Value;
                }
            }

            logger.Log(MSLogLevel.Information, new EventId(0, category.ToString()), state, null, (s, e) => message);
        }

        /// <summary>
        /// 记录 Warning 日志（带分类）
        /// </summary>
        public static void LogWarn(this MSILogger logger, string message, LogCategory category = LogCategory.System, Dictionary<string, object> data = null)
        {
            if (logger == null || !logger.IsEnabled(MSLogLevel.Warning))
                return;

            var state = new Dictionary<string, object>
            {
                ["Category"] = category.ToString(),
                ["Message"] = message
            };

            if (data != null)
            {
                foreach (var kv in data)
                {
                    state[kv.Key] = kv.Value;
                }
            }

            logger.Log(MSLogLevel.Warning, new EventId(0, category.ToString()), state, null, (s, e) => message);
        }

        /// <summary>
        /// 记录 Error 日志（带分类）
        /// </summary>
        public static void LogError(this MSILogger logger, string message, Exception ex = null, LogCategory category = LogCategory.System, Dictionary<string, object> data = null)
        {
            if (logger == null || !logger.IsEnabled(MSLogLevel.Error))
                return;

            var state = new Dictionary<string, object>
            {
                ["Category"] = category.ToString(),
                ["Message"] = message
            };

            if (data != null)
            {
                foreach (var kv in data)
                {
                    state[kv.Key] = kv.Value;
                }
            }

            logger.Log(MSLogLevel.Error, new EventId(0, category.ToString()), state, ex, (s, e) => message);
        }

        /// <summary>
        /// 记录 Critical 日志（带分类）
        /// </summary>
        public static void LogCritical(this MSILogger logger, string message, Exception ex = null, LogCategory category = LogCategory.System, Dictionary<string, object> data = null)
        {
            if (logger == null || !logger.IsEnabled(MSLogLevel.Critical))
                return;

            var state = new Dictionary<string, object>
            {
                ["Category"] = category.ToString(),
                ["Message"] = message
            };

            if (data != null)
            {
                foreach (var kv in data)
                {
                    state[kv.Key] = kv.Value;
                }
            }

            logger.Log(MSLogLevel.Critical, new EventId(0, category.ToString()), state, ex, (s, e) => message);
        }

        /// <summary>
        /// 将自定义 LogLevel 转换为 Microsoft.Extensions.Logging.LogLevel
        /// </summary>
        public static MSLogLevel ToMicrosoftLogLevel(this LogLevel customLevel)
        {
            return customLevel switch
            {
                LogLevel.Debug => MSLogLevel.Debug,
                LogLevel.Info => MSLogLevel.Information,
                LogLevel.Warning => MSLogLevel.Warning,
                LogLevel.Error => MSLogLevel.Error,
                LogLevel.Critical => MSLogLevel.Critical,
                _ => MSLogLevel.Information
            };
        }
    }
}

