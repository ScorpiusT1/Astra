using Astra.Core.Foundation.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志配置类
    /// 包含日志器的所有配置选项，用于创建和配置Logger实例
    /// </summary>
    public class LogConfig
    {
        /// <summary>
        /// 日志器名称（标识日志来源）
        /// </summary>
        public string Name { get; set; } = "app";

        /// <summary>
        /// 日志级别阈值（低于此级别的日志将被过滤）
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.Info;

        /// <summary>
        /// 是否输出到控制台
        /// </summary>
        public bool Console { get; set; } = true;

        /// <summary>
        /// 日志文件路径（如果为空则不写入文件）
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 是否使用异步模式（使用后台线程处理日志）
        /// </summary>
        public bool AsyncMode { get; set; } = true;

        /// <summary>
        /// 日志保留天数（超过此天数的日志将被自动清理）
        /// </summary>
        public int RetentionDays { get; set; } = 7;

        /// <summary>
        /// 异步模式下的最大队列大小
        /// </summary>
        public int MaxQueueSize { get; set; } = 10000;

        /// <summary>
        /// 时间戳格式字符串
        /// </summary>
        public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>
        /// 启用的日志分类（null表示全部启用）
        /// </summary>
        public HashSet<LogCategory> EnabledCategories { get; set; } = null;

        /// <summary>
        /// 流程ID（用于流程日志）
        /// </summary>
        public string WorkflowId { get; set; }

        /// <summary>
        /// 是否在日志文件开头写入流程头信息
        /// </summary>
        public bool WriteWorkflowHeader { get; set; } = true;

        /// <summary>
        /// 日志根目录
        /// </summary>
        public string LogRootDirectory { get; set; }

        /// <summary>
        /// 默认是否触发UI更新事件（可以在每条日志中单独控制）
        /// </summary>
        public bool DefaultTriggerUIEvent { get; set; } = true;

        /// <summary>
        /// 哪些级别的日志触发UI事件（null表示所有级别）
        /// </summary>
        public HashSet<LogLevel> UIEventLevels { get; set; } = null;

        /// <summary>
        /// 哪些分类的日志触发UI事件（null表示所有分类）
        /// </summary>
        public HashSet<LogCategory> UIEventCategories { get; set; } = null;

        /// <summary>
        /// 创建新的配置建造者
        /// </summary>
        public static LogConfigBuilder CreateBuilder()
        {
            return new LogConfigBuilder();
        }

        /// <summary>
        /// 快速配置（最常用的配置组合）
        /// </summary>
        public static LogConfig QuickSetup(string name, LogLevel level, string filePath = null)
        {
            return new LogConfigBuilder()
                .QuickSetup(name, level, filePath)
                .Build();
        }

        /// <summary>
        /// 开发环境配置
        /// - 控制台输出：启用
        /// - 日志级别：Debug
        /// - 异步模式：启用
        /// - UI事件：启用
        /// </summary>
        public static LogConfig Development(string name = "Development")
        {
            return new LogConfigBuilder()
                .WithName(name)
                .WithLevel(LogLevel.Debug)
                .WithConsole(true)
                .WithAsyncMode(true)
                .WithDefaultTriggerUIEvent(true)
                .Build();
        }

        /// <summary>
        /// 生产环境配置
        /// - 控制台输出：禁用
        /// - 日志级别：Info
        /// - 异步模式：启用
        /// - UI事件：禁用
        /// - 文件输出：必需
        /// </summary>
        public static LogConfig Production(string name = "Production", string filePath = null)
        {
            var builder = new LogConfigBuilder()
                .WithName(name)
                .WithLevel(LogLevel.Info)
                .WithConsole(false)
                .WithAsyncMode(true)
                .WithDefaultTriggerUIEvent(false)
                .WithRetention(30); // 生产环境保留30天

            if (!string.IsNullOrEmpty(filePath))
            {
                builder.WithFile(filePath);
            }

            return builder.Build();
        }

        /// <summary>
        /// 测试环境配置
        /// - 控制台输出：启用
        /// - 日志级别：Info
        /// - 异步模式：禁用（同步便于测试）
        /// - UI事件：禁用
        /// </summary>
        public static LogConfig Testing(string name = "Testing")
        {
            return new LogConfigBuilder()
                .WithName(name)
                .WithLevel(LogLevel.Info)
                .WithConsole(true)
                .WithAsyncMode(false) // 同步模式便于测试
                .WithDefaultTriggerUIEvent(false)
                .Build();
        }

        /// <summary>
        /// 最小配置（仅控制台输出，用于快速调试）
        /// </summary>
        public static LogConfig Minimal(string name = "Minimal", LogLevel level = LogLevel.Info)
        {
            return new LogConfigBuilder()
                .WithName(name)
                .WithLevel(level)
                .WithConsole(true)
                .WithAsyncMode(false)
                .WithDefaultTriggerUIEvent(false)
                .Build();
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        /// <returns>验证结果</returns>
        public OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            // 验证日志器名称
            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("日志器名称不能为空");
            }

            // 验证队列大小
            if (MaxQueueSize <= 0)
            {
                errors.Add("队列大小必须大于0");
            }

            // 验证保留天数
            if (RetentionDays < 0)
            {
                errors.Add("保留天数不能为负数");
            }

            // 验证文件路径
            if (!string.IsNullOrEmpty(FilePath))
            {
                try
                {
                    var directory = Path.GetDirectoryName(FilePath);
                    if (string.IsNullOrEmpty(directory))
                    {
                        errors.Add("日志文件路径无效：无法获取目录");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"日志文件路径无效：{ex.Message}");
                }
            }

            // 验证时间格式
            if (string.IsNullOrWhiteSpace(DateTimeFormat))
            {
                errors.Add("时间戳格式不能为空");
            }
            else
            {
                try
                {
                    // 尝试使用格式字符串格式化当前时间
                    DateTime.Now.ToString(DateTimeFormat);
                }
                catch (Exception ex)
                {
                    errors.Add($"时间戳格式无效：{ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                return OperationResult<bool>.Fail(
                    $"配置验证失败，发现 {errors.Count} 个问题：" + Environment.NewLine + string.Join(Environment.NewLine + "  - ", errors),
                    LogErrorCodes.InvalidConfig);
            }

            return OperationResult<bool>.Succeed(true, "配置验证通过");
        }
    }
}
