using Astra.Configuration;
using Astra.Core.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Serialization;
using Astra.Services.Logging;
using Astra.UI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Services.Home
{
    public sealed class HomeWorkflowExecutionService : IHomeWorkflowExecutionService
    {
        private readonly IConfigurationManager _configurationManager;
        private readonly IWorkflowExecutionSessionService _workflowExecutionSessionService;
        private readonly IUiLogService _uiLogService;
        private readonly IMultiWorkflowSerializer _multiWorkflowSerializer = new MultiWorkflowSerializer();

        public HomeWorkflowExecutionService(
            IConfigurationManager configurationManager,
            IWorkflowExecutionSessionService workflowExecutionSessionService,
            IUiLogService uiLogService)
        {
            _configurationManager = configurationManager;
            _workflowExecutionSessionService = workflowExecutionSessionService;
            _uiLogService = uiLogService;
        }

        public async Task ExecuteCurrentConfiguredMasterAsync(CancellationToken cancellationToken, string? manualBarcode = null)
        {
            var scriptPath = await ResolveCurrentWorkflowPathAsync().ConfigureAwait(false);
            await ExecuteConfiguredMasterAtPathAsync(scriptPath, cancellationToken, manualBarcode).ConfigureAwait(false);
        }

        public async Task ExecuteConfiguredMasterAtPathAsync(string? masterWorkflowFilePath, CancellationToken cancellationToken, string? snForGlobalVariable = null)
        {
            var scriptPath = masterWorkflowFilePath?.Trim();
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
            {
                _uiLogService.Error("Home 独立执行失败：未找到主流程脚本文件或路径无效。");
                return;
            }

            var loadResult = _multiWorkflowSerializer.LoadFromFile(scriptPath);
            if (!loadResult.Success || loadResult.Data == null)
            {
                _uiLogService.Error($"Home 独立执行失败：脚本加载失败，{loadResult.ErrorMessage}");
                return;
            }

            var plan = BuildExecutionPlan(loadResult.Data);
            if (plan.Nodes.Count == 0)
            {
                _uiLogService.Warn("Home 独立执行：当前脚本没有可执行子流程。");
                return;
            }

            var mainPlan = BuildFilteredStandaloneExecutionPlan(plan, executeLast: false);
            var finallyPlan = BuildFilteredStandaloneExecutionPlan(plan, executeLast: true);

            if (!plan.HasDependencies)
            {
                await RunHomeParallelBatchAsync(mainPlan.OrderedNodes, cancellationToken, snForGlobalVariable).ConfigureAwait(false);
                await RunHomeParallelBatchAsync(finallyPlan.OrderedNodes, cancellationToken, snForGlobalVariable).ConfigureAwait(false);
                return;
            }

            await RunHomeDependencyPhaseAsync(mainPlan, cancellationToken, snForGlobalVariable).ConfigureAwait(false);
            await RunHomeDependencyPhaseAsync(finallyPlan, cancellationToken, snForGlobalVariable).ConfigureAwait(false);
        }

        private async Task RunHomeParallelBatchAsync(
            IReadOnlyList<StandaloneExecutionNode> nodes,
            CancellationToken cancellationToken,
            string? snForGlobalVariable)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            var tasks = nodes.Select(async node =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!node.IsEnabled)
                {
                    _uiLogService.Info($"Home 独立执行：跳过禁用子流程 {node.DisplayName}");
                    return;
                }

                await ExecuteSubWorkflowAsync(node.Workflow, cancellationToken, snForGlobalVariable).ConfigureAwait(false);
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task RunHomeDependencyPhaseAsync(
            StandaloneExecutionPlan plan,
            CancellationToken cancellationToken,
            string? snForGlobalVariable)
        {
            if (plan.Nodes.Count == 0)
                return;

            var canContinue = new Dictionary<string, bool>(StringComparer.Ordinal);
            var completed = new HashSet<string>(StringComparer.Ordinal);

            var inDegree = plan.Nodes.Values.ToDictionary(n => n.RefId, n => n.PredecessorIds.Count, StringComparer.Ordinal);
            var queue = new Queue<string>(inDegree.Where(x => x.Value == 0).Select(x => x.Key));

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = new List<string>();
                while (queue.Count > 0)
                {
                    batch.Add(queue.Dequeue());
                }

                var runnable = new List<StandaloneExecutionNode>();
                foreach (var refId in batch)
                {
                    if (!plan.Nodes.TryGetValue(refId, out var node))
                    {
                        completed.Add(refId);
                        canContinue[refId] = false;
                        continue;
                    }

                    if (!node.IsEnabled)
                    {
                        completed.Add(refId);
                        canContinue[refId] = true;
                        _uiLogService.Info($"Home 独立执行：跳过禁用子流程 {node.DisplayName}");
                        continue;
                    }

                    var blocked = node.PredecessorIds.Any(preId =>
                        canContinue.TryGetValue(preId, out var preCanContinue) && !preCanContinue);
                    if (blocked)
                    {
                        completed.Add(refId);
                        canContinue[refId] = false;
                        _uiLogService.Warn($"Home 独立执行：前置流程失败且不继续，已跳过 {node.DisplayName}");
                        continue;
                    }

                    runnable.Add(node);
                }

                var runTasks = runnable.Select(async node =>
                {
                    var ok = await ExecuteSubWorkflowAsync(node.Workflow, cancellationToken, snForGlobalVariable).ConfigureAwait(false);
                    return (node, ok);
                }).ToList();

                var runResults = await Task.WhenAll(runTasks).ConfigureAwait(false);
                foreach (var (node, ok) in runResults)
                {
                    completed.Add(node.RefId);
                    canContinue[node.RefId] = ok || node.ContinueOnFailure;
                }

                foreach (var refId in batch)
                {
                    if (!plan.Successors.TryGetValue(refId, out var successors))
                        continue;

                    foreach (var next in successors)
                    {
                        if (!inDegree.ContainsKey(next))
                            continue;

                        inDegree[next]--;
                        if (inDegree[next] == 0 && !completed.Contains(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }
            }
        }

        private static StandaloneExecutionPlan BuildFilteredStandaloneExecutionPlan(StandaloneExecutionPlan source, bool executeLast)
        {
            var nodes = source.Nodes.Values
                .Where(n => n.ExecuteLast == executeLast)
                .ToDictionary(
                    n => n.RefId,
                    n => new StandaloneExecutionNode
                    {
                        RefId = n.RefId,
                        DisplayName = n.DisplayName,
                        Workflow = n.Workflow,
                        IsEnabled = n.IsEnabled,
                        ContinueOnFailure = n.ContinueOnFailure,
                        ExecuteLast = n.ExecuteLast
                    },
                    StringComparer.Ordinal);

            var orderedNodes = source.OrderedNodes.Where(n => nodes.ContainsKey(n.RefId)).ToList();

            var successors = nodes.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            foreach (var srcId in nodes.Keys)
            {
                if (!source.Successors.TryGetValue(srcId, out var outs))
                    continue;
                foreach (var tgtId in outs)
                {
                    if (!nodes.ContainsKey(tgtId))
                        continue;
                    successors[srcId].Add(tgtId);
                    nodes[tgtId].PredecessorIds.Add(srcId);
                }
            }

            var depCount = successors.Values.Sum(l => l.Count);
            return new StandaloneExecutionPlan
            {
                Nodes = nodes,
                OrderedNodes = orderedNodes,
                Successors = successors,
                HasDependencies = depCount > 0
            };
        }

        private async Task<bool> ExecuteSubWorkflowAsync(WorkFlowNode workflow, CancellationToken cancellationToken, string? manualBarcode)
        {
            var context = new NodeContext();
            if (!string.IsNullOrWhiteSpace(manualBarcode))
                context.SetGlobalVariable("SN", manualBarcode);

            var start = await _workflowExecutionSessionService
                .StartAsync(workflow.Id, workflow, context, cancellationToken)
                .ConfigureAwait(false);

            if (!start.Success || start.ExecutionTask == null)
            {
                _uiLogService.Error($"Home 独立执行：启动流程失败 {workflow.Name}，{start.Message}");
                return false;
            }

            var result = await start.ExecutionTask.ConfigureAwait(false);
            if (!result.Success || result.Data == null || !result.Data.Success)
            {
                _uiLogService.Error($"Home 独立执行：流程失败 {workflow.Name}");
                return false;
            }

            return true;
        }

        private async Task<string?> ResolveCurrentWorkflowPathAsync()
        {
            var all = await _configurationManager.GetAllAsync().ConfigureAwait(false);
            if (all?.Success != true || all.Data == null)
                return null;

            var latest = all.Data
                .OfType<SoftwareConfig>()
                .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (latest == null)
                return null;

            if (!string.IsNullOrWhiteSpace(latest.CurrentWorkflowId))
                return latest.CurrentWorkflowId;

            return (latest.Duts ?? [])
                .Select(d => d?.WorkflowId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        private static StandaloneExecutionPlan BuildExecutionPlan(MultiWorkflowData data)
        {
            var nodes = new Dictionary<string, StandaloneExecutionNode>(StringComparer.Ordinal);
            var orderedNodes = new List<StandaloneExecutionNode>();
            var successors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            var refs = data.MasterWorkflow?.SubWorkflowReferences ?? new List<WorkflowReference>();
            foreach (var reference in refs)
            {
                if (reference == null || string.IsNullOrWhiteSpace(reference.Id) || string.IsNullOrWhiteSpace(reference.SubWorkflowId))
                    continue;

                if (!data.SubWorkflows.TryGetValue(reference.SubWorkflowId, out var workflow) || workflow == null)
                    continue;

                var node = new StandaloneExecutionNode
                {
                    RefId = reference.Id,
                    DisplayName = string.IsNullOrWhiteSpace(reference.DisplayName) ? (workflow.Name ?? "未命名子流程") : reference.DisplayName,
                    Workflow = workflow,
                    IsEnabled = reference.IsEnabled,
                    ContinueOnFailure = reference.ContinueOnFailure,
                    ExecuteLast = reference.ExecuteLast
                };

                nodes[reference.Id] = node;
                orderedNodes.Add(node);
            }

            if (nodes.Count == 0)
            {
                foreach (var workflow in data.SubWorkflows?.Values?.Where(w => w != null) ?? Enumerable.Empty<WorkFlowNode>())
                {
                    orderedNodes.Add(new StandaloneExecutionNode
                    {
                        RefId = workflow.Id,
                        DisplayName = workflow.Name ?? "未命名子流程",
                        Workflow = workflow,
                        IsEnabled = true,
                        ContinueOnFailure = true,
                        ExecuteLast = false
                    });
                }

                return new StandaloneExecutionPlan
                {
                    Nodes = orderedNodes.ToDictionary(x => x.RefId, x => x, StringComparer.Ordinal),
                    OrderedNodes = orderedNodes,
                    Successors = orderedNodes.ToDictionary(x => x.RefId, _ => new List<string>(), StringComparer.Ordinal),
                    HasDependencies = false
                };
            }

            foreach (var nodeId in nodes.Keys)
            {
                successors[nodeId] = new List<string>();
            }

            var dependencyCount = 0;
            foreach (var edge in data.MasterWorkflow?.Edges ?? new List<Edge>())
            {
                if (edge == null || !nodes.ContainsKey(edge.SourceNodeId) || !nodes.ContainsKey(edge.TargetNodeId))
                    continue;

                successors[edge.SourceNodeId].Add(edge.TargetNodeId);
                nodes[edge.TargetNodeId].PredecessorIds.Add(edge.SourceNodeId);
                dependencyCount++;
            }

            return new StandaloneExecutionPlan
            {
                Nodes = nodes,
                OrderedNodes = orderedNodes,
                Successors = successors,
                HasDependencies = dependencyCount > 0
            };
        }

        private sealed class StandaloneExecutionNode
        {
            public string RefId { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
            public WorkFlowNode Workflow { get; init; } = default!;
            public bool IsEnabled { get; init; } = true;
            public bool ContinueOnFailure { get; init; }
            public bool ExecuteLast { get; init; }
            public List<string> PredecessorIds { get; } = new();
        }

        private sealed class StandaloneExecutionPlan
        {
            public Dictionary<string, StandaloneExecutionNode> Nodes { get; init; } = new(StringComparer.Ordinal);
            public List<StandaloneExecutionNode> OrderedNodes { get; init; } = new();
            public Dictionary<string, List<string>> Successors { get; init; } = new(StringComparer.Ordinal);
            public bool HasDependencies { get; init; }
        }
    }
}
