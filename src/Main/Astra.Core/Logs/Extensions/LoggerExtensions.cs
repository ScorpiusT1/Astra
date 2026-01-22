using Astra.Core.Nodes.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using MSILogger = Microsoft.Extensions.Logging.ILogger;

namespace Astra.Core.Logs.Extensions
{
    /// <summary>
    /// 日志器扩展方法
    /// 提供领域特定的日志功能（节点日志、流程日志等）
    /// 支持 Microsoft.Extensions.Logging.ILogger
    /// </summary>
    public static class LoggerExtensions
    {
        #region 节点日志扩展

        /// <summary>
        /// 记录节点开始执行
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="node">节点对象</param>
        public static void LogNodeStart(this MSILogger logger, Node node)
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

            logger.LogInfo($"节点开始执行: {node.Name}", LogCategory.Node, data);
        }

        /// <summary>
        /// 记录节点执行完成
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="node">节点对象</param>
        /// <param name="duration">执行耗时</param>
        /// <param name="result">执行结果</param>
        public static void LogNodeComplete(this MSILogger logger, Node node, TimeSpan duration, ExecutionResult result = null)
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

            logger.LogInfo($"节点执行完成: {node.Name} (耗时: {duration.TotalMilliseconds:F2}ms)", LogCategory.Node, data);
        }

        /// <summary>
        /// 记录节点执行失败
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="node">节点对象</param>
        /// <param name="ex">异常对象</param>
        /// <param name="duration">执行耗时</param>
        public static void LogNodeError(this MSILogger logger, Node node, Exception ex, TimeSpan? duration = null)
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

            logger.LogError($"节点执行失败: {node.Name} - {ex?.Message}", ex, LogCategory.Node, data);
        }

        /// <summary>
        /// 记录节点自定义信息
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="node">节点对象</param>
        /// <param name="message">日志消息</param>
        /// <param name="action">动作名称</param>
        /// <param name="data">附加数据</param>
        public static void LogNodeInfo(this MSILogger logger, Node node, string message, string action = "Info", Dictionary<string, object> data = null)
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

            logger.LogInfo($"[{node.Name}] {message}", LogCategory.Node, nodeData);
        }

        #endregion

        #region 流程日志扩展

        /// <summary>
        /// 记录流程开始
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="workflowName">流程名称</param>
        /// <param name="workflowId">流程ID</param>
        /// <param name="parameters">流程参数</param>
        public static void LogWorkflowStart(this MSILogger logger, string workflowName, string workflowId = null, Dictionary<string, object> parameters = null)
        {
            if (logger == null)
                return;

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

            logger.LogInfo($"流程开始: {workflowName}", LogCategory.System, data);
        }

        /// <summary>
        /// 记录流程完成
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="workflowName">流程名称</param>
        /// <param name="success">是否成功</param>
        /// <param name="workflowId">流程ID</param>
        /// <param name="duration">执行耗时</param>
        /// <param name="summary">汇总信息</param>
        public static void LogWorkflowComplete(this MSILogger logger, string workflowName, bool success, string workflowId = null, TimeSpan? duration = null, Dictionary<string, object> summary = null)
        {
            if (logger == null)
                return;

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
                logger.LogInfo($"流程完成: {workflowName}", LogCategory.System, data);
            }
            else
            {
                logger.LogError($"流程失败: {workflowName}", null, LogCategory.System, data);
            }
        }

        #endregion
    }
}

