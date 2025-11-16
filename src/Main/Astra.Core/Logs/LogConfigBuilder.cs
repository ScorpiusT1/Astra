using System;
using System.Collections.Generic;

namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志配置建造者
    /// 使用建造者模式简化 LogConfig 的创建过程
    /// </summary>
    public class LogConfigBuilder
    {
        private readonly LogConfig _config = new LogConfig();

        /// <summary>
        /// 设置日志器名称
        /// </summary>
        public LogConfigBuilder WithName(string name)
        {
            _config.Name = name ?? throw new ArgumentNullException(nameof(name));
            return this;
        }

        /// <summary>
        /// 设置日志级别
        /// </summary>
        public LogConfigBuilder WithLevel(LogLevel level)
        {
            _config.Level = level;
            return this;
        }

        /// <summary>
        /// 启用/禁用控制台输出
        /// </summary>
        public LogConfigBuilder WithConsole(bool enable = true)
        {
            _config.Console = enable;
            return this;
        }

        /// <summary>
        /// 设置日志文件路径
        /// </summary>
        public LogConfigBuilder WithFile(string filePath)
        {
            _config.FilePath = filePath;
            return this;
        }

        /// <summary>
        /// 启用/禁用异步模式
        /// </summary>
        public LogConfigBuilder WithAsyncMode(bool enable = true)
        {
            _config.AsyncMode = enable;
            return this;
        }

        /// <summary>
        /// 设置日志保留天数
        /// </summary>
        public LogConfigBuilder WithRetention(int days)
        {
            _config.RetentionDays = days;
            return this;
        }

        /// <summary>
        /// 设置最大队列大小
        /// </summary>
        public LogConfigBuilder WithMaxQueueSize(int size)
        {
            _config.MaxQueueSize = size;
            return this;
        }

        /// <summary>
        /// 设置时间戳格式
        /// </summary>
        public LogConfigBuilder WithDateTimeFormat(string format)
        {
            _config.DateTimeFormat = format ?? throw new ArgumentNullException(nameof(format));
            return this;
        }

        /// <summary>
        /// 设置启用的日志分类
        /// </summary>
        public LogConfigBuilder WithEnabledCategories(params LogCategory[] categories)
        {
            if (categories == null || categories.Length == 0)
            {
                _config.EnabledCategories = null;
            }
            else
            {
                _config.EnabledCategories = new HashSet<LogCategory>(categories);
            }
            return this;
        }

        /// <summary>
        /// 设置流程ID（用于流程日志）
        /// </summary>
        public LogConfigBuilder WithWorkflowId(string workflowId)
        {
            _config.WorkflowId = workflowId;
            return this;
        }

        /// <summary>
        /// 启用/禁用流程头信息写入
        /// </summary>
        public LogConfigBuilder WithWorkflowHeader(bool enable = true)
        {
            _config.WriteWorkflowHeader = enable;
            return this;
        }

        /// <summary>
        /// 设置日志根目录
        /// </summary>
        public LogConfigBuilder WithLogRootDirectory(string directory)
        {
            _config.LogRootDirectory = directory;
            return this;
        }

        /// <summary>
        /// 设置默认是否触发UI更新事件
        /// </summary>
        public LogConfigBuilder WithDefaultTriggerUIEvent(bool trigger = true)
        {
            _config.DefaultTriggerUIEvent = trigger;
            return this;
        }

        /// <summary>
        /// 设置触发UI事件的日志级别
        /// </summary>
        public LogConfigBuilder WithUIEventLevels(params LogLevel[] levels)
        {
            if (levels == null || levels.Length == 0)
            {
                _config.UIEventLevels = null;
            }
            else
            {
                _config.UIEventLevels = new HashSet<LogLevel>(levels);
            }
            return this;
        }

        /// <summary>
        /// 设置触发UI事件的日志分类
        /// </summary>
        public LogConfigBuilder WithUIEventCategories(params LogCategory[] categories)
        {
            if (categories == null || categories.Length == 0)
            {
                _config.UIEventCategories = null;
            }
            else
            {
                _config.UIEventCategories = new HashSet<LogCategory>(categories);
            }
            return this;
        }

        /// <summary>
        /// 配置为流程专用日志器
        /// </summary>
        public LogConfigBuilder ForWorkflow(string workflowId, string workflowName = null)
        {
            _config.WorkflowId = workflowId ?? throw new ArgumentNullException(nameof(workflowId));
            _config.Name = workflowName ?? $"Workflow-{workflowId}";
            _config.WriteWorkflowHeader = true;
            _config.AsyncMode = true;
            _config.DefaultTriggerUIEvent = false;
            
            // 默认启用节点和系统分类
            if (_config.EnabledCategories == null)
            {
                _config.EnabledCategories = new HashSet<LogCategory>
                {
                    LogCategory.Node,
                    LogCategory.System
                };
            }
            
            return this;
        }

        /// <summary>
        /// 快速配置（最常用的配置组合）
        /// </summary>
        public LogConfigBuilder QuickSetup(string name, LogLevel level, string filePath = null)
        {
            WithName(name);
            WithLevel(level);
            if (!string.IsNullOrEmpty(filePath))
            {
                WithFile(filePath);
            }
            return this;
        }

        /// <summary>
        /// 构建 LogConfig 实例
        /// </summary>
        public LogConfig Build()
        {
            return _config;
        }
    }
}

