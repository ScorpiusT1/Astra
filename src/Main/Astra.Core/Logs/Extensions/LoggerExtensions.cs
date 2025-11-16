using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;

namespace Astra.Core.Logs.Extensions
{
    /// <summary>
    /// 日志器扩展方法
    /// 提供领域特定的日志功能（节点日志、流程日志等）
    /// </summary>
    public static class LoggerExtensions
    {
        #region 节点日志扩展

        /// <summary>
        /// 记录节点开始执行
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="node">节点对象</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        public static void LogNodeStart(this ILogger logger, Node node, bool? triggerUIEvent = null)
        {
            if (logger == null || node == null)
                return;

            var nodeInfo = new NodeLogInfo
            {
                NodeId = node.Id,
                NodeType = node.NodeType,
                NodeName = node.Name,
                Action = "Started",
                Parameters = node.Parameters
            };

            var data = new Dictionary<string, object>
            {
                { "node_id", node.Id },
                { "node_type", node.NodeType },
                { "node_name", node.Name }
            };

            logger.Info($"节点开始执行: {node.Name}", LogCategory.Node, data, triggerUIEvent);
        }

        /// <summary>
        /// 记录节点执行完成
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="node">节点对象</param>
        /// <param name="duration">执行耗时</param>
        /// <param name="result">执行结果</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        public static void LogNodeComplete(this ILogger logger, Node node, TimeSpan duration, ExecutionResult result = null, bool? triggerUIEvent = null)
        {
            if (logger == null || node == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "node_id", node.Id },
                { "node_type", node.NodeType },
                { "node_name", node.Name },
                { "duration_ms", duration.TotalMilliseconds }
            };

            if (result != null)
            {
                data["success"] = result.Success;
            }

            logger.Info($"节点执行完成: {node.Name} (耗时: {duration.TotalMilliseconds:F2}ms)", LogCategory.Node, data, triggerUIEvent);
        }

        /// <summary>
        /// 记录节点执行失败
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="node">节点对象</param>
        /// <param name="ex">异常对象</param>
        /// <param name="duration">执行耗时</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        public static void LogNodeError(this ILogger logger, Node node, Exception ex, TimeSpan? duration = null, bool? triggerUIEvent = null)
        {
            if (logger == null || node == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "node_id", node.Id },
                { "node_type", node.NodeType },
                { "node_name", node.Name },
                { "error_type", ex?.GetType().Name }
            };

            if (duration.HasValue)
            {
                data["duration_ms"] = duration.Value.TotalMilliseconds;
            }

            logger.Error($"节点执行失败: {node.Name} - {ex?.Message}", ex, LogCategory.Node, data, triggerUIEvent);
        }

        /// <summary>
        /// 记录节点自定义信息
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="node">节点对象</param>
        /// <param name="message">日志消息</param>
        /// <param name="action">动作名称</param>
        /// <param name="data">附加数据</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        public static void LogNodeInfo(this ILogger logger, Node node, string message, string action = "Info", Dictionary<string, object> data = null, bool? triggerUIEvent = null)
        {
            if (logger == null || node == null)
                return;

            var nodeData = new Dictionary<string, object>
            {
                { "node_id", node.Id },
                { "node_type", node.NodeType },
                { "node_name", node.Name },
                { "action", action }
            };

            if (data != null)
            {
                foreach (var kv in data)
                {
                    nodeData[kv.Key] = kv.Value;
                }
            }

            logger.Info($"[{node.Name}] {message}", LogCategory.Node, nodeData, triggerUIEvent);
        }

        #endregion

        #region 流程日志扩展

        /// <summary>
        /// 记录流程开始
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="workflowName">流程名称</param>
        /// <param name="workflowId">流程ID（如果为null，且logger是Logger类型，则从配置中获取）</param>
        /// <param name="parameters">流程参数</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        public static void LogWorkflowStart(this ILogger logger, string workflowName, string workflowId = null, Dictionary<string, object> parameters = null, bool? triggerUIEvent = null)
        {
            if (logger == null)
                return;

            // 如果未提供 workflowId，尝试从 Logger 内部配置获取
            if (string.IsNullOrEmpty(workflowId) && logger is Logger concreteLogger)
            {
                workflowId = concreteLogger.Config?.WorkflowId;
            }

            var data = new Dictionary<string, object>();
            
            if (!string.IsNullOrEmpty(workflowId))
            {
                data["workflow_id"] = workflowId;
            }

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    data[kv.Key] = kv.Value;
                }
            }

            logger.Info($"流程开始: {workflowName}", LogCategory.System, data, triggerUIEvent);
        }

        /// <summary>
        /// 记录流程完成
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="workflowName">流程名称</param>
        /// <param name="success">是否成功</param>
        /// <param name="workflowId">流程ID（如果为null，且logger是Logger类型，则从配置中获取）</param>
        /// <param name="duration">执行耗时（如果为null，且logger是Logger类型，则从创建时间计算）</param>
        /// <param name="summary">汇总信息</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件（null则使用配置默认值）</param>
        public static void LogWorkflowComplete(this ILogger logger, string workflowName, bool success, string workflowId = null, TimeSpan? duration = null, Dictionary<string, object> summary = null, bool? triggerUIEvent = null)
        {
            if (logger == null)
                return;

            // 如果未提供 workflowId，尝试从 Logger 内部配置获取
            if (string.IsNullOrEmpty(workflowId) && logger is Logger concreteLogger)
            {
                workflowId = concreteLogger.Config?.WorkflowId;
            }

            // 如果未提供 duration，尝试从 Logger 创建时间计算
            if (!duration.HasValue && logger is Logger concreteLogger2)
            {
                duration = DateTime.Now - concreteLogger2.CreatedTime;
            }

            var data = new Dictionary<string, object>
            {
                { "success", success }
            };

            if (!string.IsNullOrEmpty(workflowId))
            {
                data["workflow_id"] = workflowId;
            }

            if (duration.HasValue)
            {
                data["duration_seconds"] = duration.Value.TotalSeconds;
            }

            if (summary != null)
            {
                foreach (var kv in summary)
                {
                    data[kv.Key] = kv.Value;
                }
            }

            if (success)
            {
                logger.Info($"流程完成: {workflowName}", LogCategory.System, data, triggerUIEvent);
            }
            else
            {
                logger.Error($"流程失败: {workflowName}", null, LogCategory.System, data, triggerUIEvent);
            }
        }

        #endregion
    }
}

