using Astra.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Serialization;
using Astra.ViewModels.HomeModules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Services.Home
{
    /// <summary>
    /// 基于当前软件配置脚本（.sol）加载流程节点，作为 Home 测试项树的数据源。
    /// </summary>
    public sealed class WorkflowNodeTestItemTreeDataProvider : ITestItemTreeDataProvider
    {
        private readonly IConfigurationManager _configurationManager;
        private readonly IMultiWorkflowSerializer _serializer;

        public WorkflowNodeTestItemTreeDataProvider(IConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _serializer = new MultiWorkflowSerializer();
        }

        public async Task<IReadOnlyList<TestTreeNodeItem>> LoadRootNodesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var allResult = await _configurationManager.GetAllAsync().ConfigureAwait(false);
            if (allResult?.Success != true || allResult.Data == null)
            {
                return Array.Empty<TestTreeNodeItem>();
            }

            var softwareConfig = allResult.Data
                .OfType<SoftwareConfig>()
                .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            var scriptPath = ResolveScriptPath(softwareConfig);
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
            {
                return Array.Empty<TestTreeNodeItem>();
            }

            var loadResult = _serializer.LoadFromFile(scriptPath);
            if (!loadResult.Success || loadResult.Data == null)
            {
                throw new InvalidOperationException($"加载流程脚本失败: {loadResult.ErrorMessage}");
            }

            var roots = new List<TestTreeNodeItem>();
            var subWorkflows = loadResult.Data.SubWorkflows?.Values?.ToList() ?? new List<WorkFlowNode>();

            // 以“子流程”为根节点，子流程中的每个节点作为测试项叶子节点。
            foreach (var workflow in subWorkflows.OrderBy(x => x?.Name))
            {
                if (workflow == null)
                {
                    continue;
                }

                var root = new TestTreeNodeItem
                {
                    Name = string.IsNullOrWhiteSpace(workflow.Name) ? "未命名流程" : workflow.Name,
                    Status = "Ready",
                    IsRoot = true,
                    TestTime = DateTime.Now
                };

                foreach (var node in (workflow.Nodes ?? new List<Node>()).OrderBy(x => x?.Name))
                {
                    if (node == null)
                    {
                        continue;
                    }

                    root.Children.Add(new TestTreeNodeItem
                    {
                        NodeId = node.Id,
                        Name = string.IsNullOrWhiteSpace(node.Name) ? node.NodeType ?? "未命名节点" : node.Name,
                        Status = MapStatus(node.ExecutionState),
                        TestTime = DateTime.Now,
                        ActualValue = 0,
                        LowerLimit = 0,
                        UpperLimit = 0,
                        IsRoot = false
                    });
                }

                roots.Add(root);
            }

            return roots;
        }

        private static string? ResolveScriptPath(SoftwareConfig? config)
        {
            if (config == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(config.CurrentWorkflowId))
            {
                return config.CurrentWorkflowId;
            }

            return (config.Duts ?? [])
                .Select(d => d?.WorkflowId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        private static string MapStatus(NodeExecutionState state)
        {
            return state switch
            {
                NodeExecutionState.Success => "Pass",
                NodeExecutionState.Failed => "Fail",
                NodeExecutionState.Running => "Running",
                _ => "Ready"
            };
        }
    }
}
