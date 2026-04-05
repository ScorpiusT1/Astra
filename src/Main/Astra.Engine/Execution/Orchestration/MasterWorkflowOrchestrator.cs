using Astra.Core.Archiving;
using Astra.Core.Logs;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Orchestration;
using Astra.Core.Reporting;
using Astra.Engine.Execution.NodeExecutor;
using Astra.UI.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Orchestration
{
    public sealed class MasterWorkflowOrchestrator : IMasterWorkflowOrchestrator
    {
        private readonly IWorkFlowManager _workFlowManager;
        private readonly ICombinedReportCollector? _combinedReportCollector;
        private readonly ITestReportGenerator? _testReportGenerator;
        private readonly IWorkflowArchiveService? _workflowArchiveService;
        private readonly IWorkflowEngineProvider? _engineProvider;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IExecutionLogSink? _executionLogSink;
        private readonly ILogger<MasterWorkflowOrchestrator>? _logger;
        private readonly IReportStationLineSource? _reportStationLineSource;

        /// <summary>混合主阶段执行期间填充，供 <see cref="RaiseMasterPluginProgress"/> 写入 <see cref="SubWorkflowProgressEventArgs.ScopeWorkflowKey"/>。</summary>
        private string? _hybridScopeWorkflowKey;

        public event EventHandler<SubWorkflowProgressEventArgs>? SubWorkflowProgressChanged;

        public MasterWorkflowOrchestrator(
            IWorkFlowManager workFlowManager,
            ICombinedReportCollector? combinedReportCollector = null,
            ITestReportGenerator? testReportGenerator = null,
            IWorkflowArchiveService? workflowArchiveService = null,
            IWorkflowEngineProvider? engineProvider = null,
            IServiceProvider? serviceProvider = null,
            IExecutionLogSink? executionLogSink = null,
            ILogger<MasterWorkflowOrchestrator>? logger = null,
            IReportStationLineSource? reportStationLineSource = null)
        {
            _workFlowManager = workFlowManager ?? throw new ArgumentNullException(nameof(workFlowManager));
            _combinedReportCollector = combinedReportCollector;
            _testReportGenerator = testReportGenerator;
            _workflowArchiveService = workflowArchiveService;
            _engineProvider = engineProvider;
            _serviceProvider = serviceProvider;
            _executionLogSink = executionLogSink;
            _logger = logger;
            _reportStationLineSource = reportStationLineSource;
        }

        public async Task<MasterExecutionResult> ExecuteAsync(
            MasterExecutionPlan plan,
            MasterExecutionOptions options,
            CancellationToken cancellationToken)
        {
            if (plan == null)
                return new MasterExecutionResult { SuccessCount = 0, FailedCount = 0, SkippedCount = 0 };

            var useHybrid = ShouldUseHybridMasterCanvas(plan, options);
            if ((plan.Entries == null || plan.Entries.Count == 0) && !useHybrid)
                return new MasterExecutionResult { SuccessCount = 0, FailedCount = 0, SkippedCount = 0 };

            var useBatch = _combinedReportCollector != null && _testReportGenerator != null;
            if (useBatch)
            {
                _combinedReportCollector!.BeginBatch();
                _workflowArchiveService?.OnBatchArchiveStarted();
            }

            try
            {
                if (useHybrid)
                    return await ExecuteWithHybridMasterCanvasAsync(plan, options, cancellationToken).ConfigureAwait(false);

                var mainPlan = FilterPlan(plan, executeLast: false);
                var finallyPlan = FilterPlan(plan, executeLast: true);
                var sectionOrderByRefId = BuildSectionOrderMap(mainPlan, finallyPlan);

                int successCount = 0, failedCount = 0, skippedCount = 0;

                if (!plan.HasDependencies)
                {
                    var p1 = await RunParallelBatchAsync(mainPlan.OrderedEntries, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                    var p2 = await RunParallelBatchAsync(finallyPlan.OrderedEntries, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                    successCount = p1.success + p2.success;
                    failedCount = p1.failed + p2.failed;
                    skippedCount = p1.skipped + p2.skipped;
                }
                else
                {
                    var d1 = await RunDependencyPhaseAsync(mainPlan, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                    var d2 = await RunDependencyPhaseAsync(finallyPlan, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                    successCount = d1.success + d2.success;
                    failedCount = d1.failed + d2.failed;
                    skippedCount = d1.skipped + d2.skipped;
                }

                return new MasterExecutionResult
                {
                    SuccessCount = successCount,
                    FailedCount = failedCount,
                    SkippedCount = skippedCount
                };
            }
            finally
            {
                if (useBatch)
                {
                    await FinalizeBatchAsync(options?.Sn, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static bool ShouldUseHybridMasterCanvas(MasterExecutionPlan plan, MasterExecutionOptions? options)
        {
            var g = options?.MasterCanvasRuntime;
            if (g?.Nodes == null || g.Nodes.Count == 0)
                return false;
            return g.Nodes.Any(n => n != null && n is not WorkflowReferenceNode);
        }

        private async Task<MasterExecutionResult> ExecuteWithHybridMasterCanvasAsync(
            MasterExecutionPlan plan,
            MasterExecutionOptions options,
            CancellationToken cancellationToken)
        {
            var mainPlan = FilterPlan(plan, executeLast: false);
            var finallyPlan = FilterPlan(plan, executeLast: true);
            var sectionOrderByRefId = BuildSectionOrderMap(mainPlan, finallyPlan);

            var scopeKey = options?.MasterCanvasRuntime?.MasterWorkflowId;
            _hybridScopeWorkflowKey = string.IsNullOrWhiteSpace(scopeKey) ? null : scopeKey;
            try
            {
                var h = await RunHybridMainPhaseAsync(mainPlan, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                int successCount = h.success, failedCount = h.failed, skippedCount = h.skipped;

                if (finallyPlan.Entries.Count > 0)
                {
                    if (!finallyPlan.HasDependencies)
                    {
                        var p2 = await RunParallelBatchAsync(finallyPlan.OrderedEntries, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                        successCount += p2.success;
                        failedCount += p2.failed;
                        skippedCount += p2.skipped;
                    }
                    else
                    {
                        var d2 = await RunDependencyPhaseAsync(finallyPlan, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                        successCount += d2.success;
                        failedCount += d2.failed;
                        skippedCount += d2.skipped;
                    }
                }

                return new MasterExecutionResult
                {
                    SuccessCount = successCount,
                    FailedCount = failedCount,
                    SkippedCount = skippedCount
                };
            }
            finally
            {
                _hybridScopeWorkflowKey = null;
            }
        }

        /// <summary>
        /// 主阶段：按主画布连线拓扑执行「非 ExecuteLast 引用」与插件节点；子流程仍走 <see cref="ExecuteSubWorkflowCoreAsync"/>。
        /// </summary>
        private async Task<(int success, int failed, int skipped)> RunHybridMainPhaseAsync(
            MasterExecutionPlan mainPlan,
            MasterExecutionOptions options,
            IReadOnlyDictionary<string, int> sectionOrderByRefId,
            CancellationToken cancellationToken)
        {
            var g = options?.MasterCanvasRuntime;
            if (g?.Nodes == null || g.Nodes.Count == 0)
            {
                if (mainPlan.Entries.Count == 0)
                    return (0, 0, 0);
                if (!mainPlan.HasDependencies)
                    return await RunParallelBatchAsync(mainPlan.OrderedEntries, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                return await RunDependencyPhaseAsync(mainPlan, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
            }

            var phase1Ids = BuildHybridPhase1NodeIds(g, mainPlan);
            if (phase1Ids.Count == 0)
            {
                if (mainPlan.Entries.Count == 0)
                    return (0, 0, 0);
                if (!mainPlan.HasDependencies)
                    return await RunParallelBatchAsync(mainPlan.OrderedEntries, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                return await RunDependencyPhaseAsync(mainPlan, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
            }

            var shell = BuildMasterHybridShell(g, phase1Ids);
            if (shell == null)
            {
                if (mainPlan.Entries.Count == 0)
                    return (0, 0, 0);
                if (!mainPlan.HasDependencies)
                    return await RunParallelBatchAsync(mainPlan.OrderedEntries, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
                return await RunDependencyPhaseAsync(mainPlan, options, sectionOrderByRefId, cancellationToken).ConfigureAwait(false);
            }

            var predecessors = BuildPredecessorMap(g, phase1Ids);
            var successors = BuildSuccessorMap(g, phase1Ids);

            var inDegree = phase1Ids.ToDictionary(
                id => id,
                id => predecessors.TryGetValue(id, out var ps) ? ps.Count : 0,
                StringComparer.Ordinal);

            var queue = new Queue<string>(inDegree.Where(x => x.Value == 0).Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal));
            var completed = new HashSet<string>(StringComparer.Ordinal);
            var canContinue = new Dictionary<string, bool>(StringComparer.Ordinal);

            int successCount = 0, failedCount = 0, skippedCount = 0;

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = new List<string>();
                while (queue.Count > 0)
                    batch.Add(queue.Dequeue());
                batch.Sort(StringComparer.Ordinal);

                // 同一拓扑层（入度同时归零）的节点彼此无先后依赖，应并行执行；原先 foreach+await 会把「两子流程汇入逻辑节点」误判为串行。
                var refTasks = new List<Task<(string NodeId, bool Ok, SubWorkflowEntry Entry)>>();
                var pluginTasks = new List<Task<(string NodeId, Node Node, ExecutionResult? ExecResult, bool ThrowCancelled)>>();
                var refSpecs = new List<(string Nid, SubWorkflowEntry Entry)>();
                var pluginSpecs = new List<(string Nid, Node Node)>();

                foreach (var nodeId in batch)
                {
                    if (!phase1Ids.Contains(nodeId))
                    {
                        completed.Add(nodeId);
                        canContinue[nodeId] = false;
                        continue;
                    }

                    var node = shell.GetNode(nodeId);
                    if (node == null)
                    {
                        completed.Add(nodeId);
                        canContinue[nodeId] = false;
                        continue;
                    }

                    if (!node.IsEnabled)
                    {
                        completed.Add(nodeId);
                        canContinue[nodeId] = true;
                        skippedCount++;
                        if (node is WorkflowReferenceNode)
                        {
                            if (mainPlan.Entries.TryGetValue(nodeId, out var entDis))
                                RaiseProgress(entDis, SubWorkflowState.Skipped);
                        }
                        else
                            RaiseMasterPluginProgress(nodeId, node.Name ?? nodeId, SubWorkflowState.Skipped);

                        continue;
                    }

                    if (predecessors.TryGetValue(nodeId, out var preds) && preds.Count > 0)
                    {
                        var blocked = preds.Any(preId =>
                            canContinue.TryGetValue(preId, out var c) && !c);
                        if (blocked)
                        {
                            completed.Add(nodeId);
                            canContinue[nodeId] = false;
                            skippedCount++;
                            if (node is WorkflowReferenceNode && mainPlan.Entries.TryGetValue(nodeId, out var entB))
                                RaiseProgress(entB, SubWorkflowState.Skipped);
                            else
                                RaiseMasterPluginProgress(nodeId, node.Name ?? nodeId, SubWorkflowState.Skipped);
                            continue;
                        }
                    }

                    if (node is WorkflowReferenceNode)
                    {
                        if (!mainPlan.Entries.TryGetValue(nodeId, out var entry))
                        {
                            completed.Add(nodeId);
                            canContinue[nodeId] = true;
                            skippedCount++;
                            continue;
                        }

                        refSpecs.Add((nodeId, entry));
                    }
                    else
                    {
                        pluginSpecs.Add((nodeId, node));
                    }
                }

                var parallelWave = new List<SubWorkflowParallelStartItem>(refSpecs.Count + pluginSpecs.Count);
                foreach (var (_, ent) in refSpecs)
                {
                    parallelWave.Add(new SubWorkflowParallelStartItem
                    {
                        RefId = ent.RefId,
                        DisplayName = ent.DisplayName,
                        IsHybridMasterPlugin = false
                    });
                }

                foreach (var (nid, n) in pluginSpecs)
                {
                    parallelWave.Add(new SubWorkflowParallelStartItem
                    {
                        RefId = nid,
                        DisplayName = n.Name ?? nid,
                        IsHybridMasterPlugin = true
                    });
                }

                var announceEachHybridRunning = parallelWave.Count <= 1;
                if (!announceEachHybridRunning)
                    RaiseParallelRunningGroup(parallelWave, _hybridScopeWorkflowKey);

                foreach (var (nid, ent) in refSpecs)
                {
                    refTasks.Add(RunHybridRefTaskAsync(nid, ent, options, sectionOrderByRefId, cancellationToken, announceEachHybridRunning));
                }

                foreach (var (nid, n) in pluginSpecs)
                {
                    pluginTasks.Add(RunHybridPluginTaskAsync(nid, n, options, shell, sectionOrderByRefId, cancellationToken, announceEachHybridRunning));
                }

                var waveTasks = new List<Task>(refTasks.Count + pluginTasks.Count);
                waveTasks.AddRange(refTasks);
                waveTasks.AddRange(pluginTasks);
                if (waveTasks.Count > 0)
                    await Task.WhenAll(waveTasks).ConfigureAwait(false);

                foreach (var t in refTasks)
                {
                    var (nid, ok, entry) = await t.ConfigureAwait(false);
                    completed.Add(nid);
                    if (ok)
                    {
                        successCount++;
                        canContinue[nid] = true;
                    }
                    else
                    {
                        failedCount++;
                        canContinue[nid] = entry.ContinueOnFailure;
                    }
                }

                foreach (var t in pluginTasks)
                {
                    var (nid, n, execResult, throwCancelled) = await t.ConfigureAwait(false);
                    if (throwCancelled)
                    {
                        completed.Add(nid);
                        canContinue[nid] = false;
                        RaiseMasterPluginProgress(nid, n.Name ?? nid, SubWorkflowState.Cancelled);
                        throw new OperationCanceledException(cancellationToken);
                    }

                    n.LastExecutionResult = execResult;
                    completed.Add(nid);
                    var success = execResult != null && execResult.Success;
                    if (execResult?.ResultType == ExecutionResultType.Cancelled)
                    {
                        canContinue[nid] = false;
                        RaiseMasterPluginProgress(nid, n.Name ?? nid, SubWorkflowState.Cancelled, execResult);
                    }
                    else if (execResult != null && (execResult.IsSkipped || execResult.ResultType == ExecutionResultType.Skipped))
                    {
                        skippedCount++;
                        canContinue[nid] = true;
                        RaiseMasterPluginProgress(nid, n.Name ?? nid, SubWorkflowState.Skipped, execResult);
                    }
                    else if (success)
                    {
                        successCount++;
                        canContinue[nid] = true;
                        RaiseMasterPluginProgress(nid, n.Name ?? nid, SubWorkflowState.Success, execResult);
                    }
                    else
                    {
                        failedCount++;
                        canContinue[nid] = n.ContinueOnFailure;
                        RaiseMasterPluginProgress(nid, n.Name ?? nid, SubWorkflowState.Failed, execResult);
                    }
                }

                foreach (var refId in batch)
                {
                    if (!successors.TryGetValue(refId, out var outs))
                        continue;
                    foreach (var next in outs.OrderBy(x => x, StringComparer.Ordinal))
                    {
                        if (!inDegree.ContainsKey(next)) continue;
                        inDegree[next]--;
                        if (inDegree[next] == 0 && !completed.Contains(next))
                            queue.Enqueue(next);
                    }
                }
            }

            if (completed.Count != phase1Ids.Count)
                _logger?.LogWarning("主流程混合执行：存在环或未调度节点（已完成 {Done}/{Total}）。", completed.Count, phase1Ids.Count);

            return (successCount, failedCount, skippedCount);
        }

        private async Task<(string NodeId, bool Ok, SubWorkflowEntry Entry)> RunHybridRefTaskAsync(
            string nodeId,
            SubWorkflowEntry entry,
            MasterExecutionOptions options,
            IReadOnlyDictionary<string, int> sectionOrderByRefId,
            CancellationToken cancellationToken,
            bool announceRunning = true)
        {
            var ok = await ExecuteSubWorkflowCoreAsync(entry, options, sectionOrderByRefId, cancellationToken, announceRunning).ConfigureAwait(false);
            return (nodeId, ok, entry);
        }

        private async Task<(string NodeId, Node Node, ExecutionResult? ExecResult, bool ThrowCancelled)> RunHybridPluginTaskAsync(
            string nodeId,
            Node node,
            MasterExecutionOptions options,
            WorkFlowNode shell,
            IReadOnlyDictionary<string, int> sectionOrderByRefId,
            CancellationToken cancellationToken,
            bool announceRunning = true)
        {
            if (announceRunning)
                RaiseMasterPluginProgress(nodeId, node.Name ?? nodeId, SubWorkflowState.Running);
            try
            {
                var ctx = CreateHybridPluginContext(options, shell, sectionOrderByRefId, node.Name ?? nodeId);
                var execResult = await node.ExecuteAsync(ctx, cancellationToken).ConfigureAwait(false);
                return (nodeId, node, execResult, false);
            }
            catch (OperationCanceledException)
            {
                return (nodeId, node, null, true);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "主流程插件节点执行异常: {Node}", node.Name);
                return (nodeId, node, ExecutionResult.Failed(ex.Message), false);
            }
        }

        private static HashSet<string> BuildHybridPhase1NodeIds(MasterCanvasRuntimeGraph g, MasterExecutionPlan mainPlan)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in g.Nodes)
            {
                if (n == null || string.IsNullOrWhiteSpace(n.Id))
                    continue;
                if (n is WorkflowReferenceNode)
                {
                    if (mainPlan.Entries.ContainsKey(n.Id))
                        ids.Add(n.Id);
                }
                else
                {
                    ids.Add(n.Id);
                }
            }

            return ids;
        }

        private static WorkFlowNode? BuildMasterHybridShell(MasterCanvasRuntimeGraph g, HashSet<string> phase1Ids)
        {
            var shellNodes = g.Nodes.Where(n => n != null && phase1Ids.Contains(n.Id)).ToList();
            if (shellNodes.Count == 0)
                return null;

            var shell = new WorkFlowNode
            {
                Id = "__MasterCanvasHybrid__",
                Name = "主流程混合执行",
                Nodes = shellNodes,
                Connections = new List<Connection>()
            };

            foreach (var e in g.Edges ?? Array.Empty<Edge>())
            {
                if (e == null || string.IsNullOrWhiteSpace(e.SourceNodeId) || string.IsNullOrWhiteSpace(e.TargetNodeId))
                    continue;
                if (!phase1Ids.Contains(e.SourceNodeId) || !phase1Ids.Contains(e.TargetNodeId))
                    continue;

                shell.Connections.Add(new Connection
                {
                    SourceNodeId = e.SourceNodeId,
                    TargetNodeId = e.TargetNodeId,
                    SourcePortId = e.SourcePortId ?? string.Empty,
                    TargetPortId = e.TargetPortId ?? string.Empty,
                    Type = ConnectionType.Flow
                });
            }

            shell.RebindChildWorkflowReferences();
            shell.RebuildRelationships();
            return shell;
        }

        private static Dictionary<string, List<string>> BuildPredecessorMap(MasterCanvasRuntimeGraph g, HashSet<string> phase1Ids)
        {
            var map = phase1Ids.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            foreach (var e in g.Edges ?? Array.Empty<Edge>())
            {
                if (e == null) continue;
                if (!phase1Ids.Contains(e.SourceNodeId) || !phase1Ids.Contains(e.TargetNodeId))
                    continue;
                if (!map.TryGetValue(e.TargetNodeId, out var list))
                    continue;
                list.Add(e.SourceNodeId);
            }

            return map;
        }

        private static Dictionary<string, List<string>> BuildSuccessorMap(MasterCanvasRuntimeGraph g, HashSet<string> phase1Ids)
        {
            var map = phase1Ids.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            foreach (var e in g.Edges ?? Array.Empty<Edge>())
            {
                if (e == null) continue;
                if (!phase1Ids.Contains(e.SourceNodeId) || !phase1Ids.Contains(e.TargetNodeId))
                    continue;
                if (!map.TryGetValue(e.SourceNodeId, out var list))
                    continue;
                list.Add(e.TargetNodeId);
            }

            return map;
        }

        private NodeContext CreateHybridPluginContext(
            MasterExecutionOptions options,
            WorkFlowNode shell,
            IReadOnlyDictionary<string, int> sectionOrderByRefId,
            string conditionLabel)
        {
            var context = new NodeContext();
            if (_serviceProvider != null)
                context.ServiceProvider = _serviceProvider;
            if (_executionLogSink != null)
            {
                context.SetMetadata(ExecutionContextMetadataKeys.UiLogWriter,
                    (Action<string, string>)((level, message) => _executionLogSink.Write(level, message)));
            }

            if (!string.IsNullOrWhiteSpace(options?.Sn))
                context.SetGlobalVariable("SN", options.Sn!);
            if (options?.InitialGlobalVariables != null)
            {
                foreach (var kv in options.InitialGlobalVariables)
                    context.SetGlobalVariable(kv.Key, kv.Value);
            }

            context.SetGlobalVariable("工况", conditionLabel);
            if (sectionOrderByRefId.Count > 0)
                context.SetGlobalVariable(ReportContextKeys.SectionSequenceOrder, 0);

            context.ParentWorkFlow = shell;
            return context;
        }

        private void RaiseMasterPluginProgress(string nodeId, string displayName, SubWorkflowState state, ExecutionResult? result = null)
        {
            SubWorkflowProgressChanged?.Invoke(this, new SubWorkflowProgressEventArgs
            {
                RefId = nodeId,
                DisplayName = displayName,
                State = state,
                Result = result,
                ScopeWorkflowKey = _hybridScopeWorkflowKey
            });
        }

        // ── 并行批量执行 ──────────────────────────────────────

        private async Task<(int success, int failed, int skipped)> RunParallelBatchAsync(
            IReadOnlyList<SubWorkflowEntry> entries,
            MasterExecutionOptions options,
            IReadOnlyDictionary<string, int> sectionOrderByRefId,
            CancellationToken cancellationToken)
        {
            if (entries == null || entries.Count == 0)
                return (0, 0, 0);

            var enabledEntries = entries.Where(e => e.IsEnabled).ToList();
            var announceEachRunning = enabledEntries.Count <= 1;
            if (enabledEntries.Count > 1)
            {
                RaiseParallelRunningGroup(
                    enabledEntries.Select(e => new SubWorkflowParallelStartItem
                    {
                        RefId = e.RefId,
                        DisplayName = e.DisplayName,
                        IsHybridMasterPlugin = false
                    }).ToList(),
                    scopeWorkflowKey: null);
            }

            var results = await Task.WhenAll(entries.Select(async entry =>
            {
                if (!entry.IsEnabled)
                {
                    RaiseProgress(entry, SubWorkflowState.Skipped);
                    return (ok: false, skipped: true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var ok = await ExecuteSubWorkflowCoreAsync(entry, options, sectionOrderByRefId, cancellationToken, announceEachRunning).ConfigureAwait(false);
                return (ok, skipped: false);
            })).ConfigureAwait(false);

            int success = 0, failed = 0, skipped = 0;
            foreach (var r in results)
            {
                if (r.skipped) skipped++;
                else if (r.ok) success++;
                else failed++;
            }

            return (success, failed, skipped);
        }

        // ── 依赖拓扑执行 ──────────────────────────────────────

        private async Task<(int success, int failed, int skipped)> RunDependencyPhaseAsync(
            MasterExecutionPlan plan,
            MasterExecutionOptions options,
            IReadOnlyDictionary<string, int> sectionOrderByRefId,
            CancellationToken cancellationToken)
        {
            if (plan.Entries.Count == 0)
                return (0, 0, 0);

            int successCount = 0, failedCount = 0, skippedCount = 0;

            var inDegree = plan.Entries.Values.ToDictionary(
                e => e.RefId, e => e.PredecessorIds.Count, StringComparer.Ordinal);
            var queue = new Queue<string>(inDegree.Where(x => x.Value == 0).Select(x => x.Key));
            var completed = new HashSet<string>(StringComparer.Ordinal);
            var canContinue = new Dictionary<string, bool>(StringComparer.Ordinal);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = new List<string>();
                while (queue.Count > 0)
                    batch.Add(queue.Dequeue());

                var runnable = new List<SubWorkflowEntry>();
                foreach (var refId in batch)
                {
                    if (!plan.Entries.TryGetValue(refId, out var entry))
                    {
                        completed.Add(refId);
                        canContinue[refId] = false;
                        continue;
                    }

                    if (!entry.IsEnabled)
                    {
                        completed.Add(refId);
                        canContinue[refId] = true;
                        skippedCount++;
                        RaiseProgress(entry, SubWorkflowState.Skipped);
                        continue;
                    }

                    var blocked = entry.PredecessorIds.Any(preId =>
                        canContinue.TryGetValue(preId, out var c) && !c);
                    if (blocked)
                    {
                        completed.Add(refId);
                        canContinue[refId] = false;
                        skippedCount++;
                        RaiseProgress(entry, SubWorkflowState.Skipped);
                        continue;
                    }

                    runnable.Add(entry);
                }

                var announceEachRunning = runnable.Count <= 1;
                if (runnable.Count > 1)
                {
                    RaiseParallelRunningGroup(
                        runnable.Select(e => new SubWorkflowParallelStartItem
                        {
                            RefId = e.RefId,
                            DisplayName = e.DisplayName,
                            IsHybridMasterPlugin = false
                        }).ToList(),
                        scopeWorkflowKey: null);
                }

                var runTasks = runnable.Select(async entry =>
                {
                    var ok = await ExecuteSubWorkflowCoreAsync(entry, options, sectionOrderByRefId, cancellationToken, announceEachRunning).ConfigureAwait(false);
                    return (entry, ok);
                }).ToList();

                var runResults = await Task.WhenAll(runTasks).ConfigureAwait(false);
                foreach (var (entry, ok) in runResults)
                {
                    completed.Add(entry.RefId);
                    if (ok)
                    {
                        successCount++;
                        canContinue[entry.RefId] = true;
                    }
                    else
                    {
                        failedCount++;
                        canContinue[entry.RefId] = entry.ContinueOnFailure;
                    }
                }

                foreach (var refId in batch)
                {
                    if (!plan.Successors.TryGetValue(refId, out var successors))
                        continue;
                    foreach (var next in successors)
                    {
                        if (!inDegree.ContainsKey(next)) continue;
                        inDegree[next]--;
                        if (inDegree[next] == 0 && !completed.Contains(next))
                            queue.Enqueue(next);
                    }
                }
            }

            return (successCount, failedCount, skippedCount);
        }

        // ── 单子流程执行核心 ──────────────────────────────────

        private async Task<bool> ExecuteSubWorkflowCoreAsync(
            SubWorkflowEntry entry,
            MasterExecutionOptions options,
            IReadOnlyDictionary<string, int> sectionOrderByRefId,
            CancellationToken cancellationToken,
            bool announceRunning = true)
        {
            var context = new NodeContext();
            if (_serviceProvider != null)
                context.ServiceProvider = _serviceProvider;
            if (_executionLogSink != null)
            {
                context.SetMetadata(ExecutionContextMetadataKeys.UiLogWriter,
                    (Action<string, string>)((level, message) => _executionLogSink.Write(level, message)));
            }

            if (!string.IsNullOrWhiteSpace(options?.Sn))
                context.SetGlobalVariable("SN", options.Sn!);
            if (options?.InitialGlobalVariables != null)
            {
                foreach (var kv in options.InitialGlobalVariables)
                    context.SetGlobalVariable(kv.Key, kv.Value);
            }
            context.SetGlobalVariable("工况", entry.DisplayName);

            if (sectionOrderByRefId.Count > 0 && !string.IsNullOrWhiteSpace(entry.RefId))
            {
                var rid = entry.RefId.Trim();
                if (sectionOrderByRefId.TryGetValue(rid, out var seq))
                    context.SetGlobalVariable(ReportContextKeys.SectionSequenceOrder, seq);
            }

            if (announceRunning)
                RaiseProgress(entry, SubWorkflowState.Running);

            try
            {
                // 用主流程引用节点 RefId 作为管理器键，避免多子流程共用同一 WorkFlowNode.Id 时 Unregister/Register 并行互踩导致无法并行；
                // 节点事件与 UI 仍使用子流程 Id（workFlowKeyForContextMetadata）。
                var registrationKey = string.IsNullOrWhiteSpace(entry.RefId)
                    ? entry.Workflow.Id
                    : entry.RefId;
                if (string.IsNullOrWhiteSpace(registrationKey))
                {
                    _logger?.LogWarning("子流程缺少 RefId 与 Id，跳过: {DisplayName}", entry.DisplayName);
                    RaiseProgress(entry, SubWorkflowState.Failed);
                    return false;
                }

                var contextWorkFlowKey = string.IsNullOrWhiteSpace(entry.Workflow.Id)
                    ? registrationKey
                    : entry.Workflow.Id;

                _workFlowManager.RegisterOrReplaceWorkFlow(registrationKey, entry.Workflow);

                Core.Foundation.Common.OperationResult<ExecutionResult> result;

                var engine = _engineProvider?.CreateWithNodeEventBridge();
                if (engine != null)
                {
                    result = await _workFlowManager.ExecuteWorkFlowAsync(
                        registrationKey, engine, context, cancellationToken, contextWorkFlowKey).ConfigureAwait(false);
                }
                else
                {
                    result = await _workFlowManager.ExecuteWorkFlowAsync(
                        registrationKey, context, cancellationToken, contextWorkFlowKey).ConfigureAwait(false);
                }

                var success = result.Success && result.Data?.Success == true;
                TryAssignMasterWorkflowReferenceLastResult(options, entry, result.Data);
                RaiseProgress(entry,
                    result.Data?.ResultType == ExecutionResultType.Cancelled
                        ? SubWorkflowState.Cancelled
                        : success ? SubWorkflowState.Success : SubWorkflowState.Failed,
                    result.Data);
                return success;
            }
            catch (OperationCanceledException)
            {
                RaiseProgress(entry, SubWorkflowState.Cancelled);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "子流程执行异常: {DisplayName}", entry.DisplayName);
                RaiseProgress(entry, SubWorkflowState.Failed);
                return false;
            }
        }

        // ── 批次合并归档 ──────────────────────────────────────

        private async Task FinalizeBatchAsync(string? sn, CancellationToken cancellationToken)
        {
            try
            {
                var batch = _combinedReportCollector!.EndBatch();

                var outputDir = batch.SharedOutputDirectory;
                if (string.IsNullOrWhiteSpace(outputDir))
                {
                    _logger?.LogWarning("合并归档：无法确定输出目录。");
                    return;
                }

                var safeSn = string.IsNullOrWhiteSpace(sn) ? "AUTO" : sn;
                var fileStamp = batch.SharedFileTimestamp
                                ?? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var (stationName, lineName) = _reportStationLineSource?.GetStationAndLine() ?? (string.Empty, string.Empty);
                var okNg = batch.Sections.Count > 0
                    ? (batch.Sections.All(s => string.Equals(s.OverallResult, "OK", StringComparison.Ordinal)) ? "OK" : "NG")
                    : batch.RunRecords.Count > 0
                        ? (batch.RunRecords.All(r => r.Record.FinalResult?.Success == true) ? "OK" : "NG")
                        : "UNK";
                var filePrefix = ReportArchiveFileNaming.BuildFilePrefix(safeSn, stationName, lineName, okNg, fileStamp);

                Directory.CreateDirectory(outputDir);
                _workflowArchiveService?.FlushBatchCombinedRawTdms(outputDir, filePrefix);

                if (batch.Sections.Count == 0)
                {
                    _logger?.LogWarning("合并归档：无可用子流程报告数据。");
                    return;
                }

                WriteCombinedRunRecordJson(batch.RunRecords, safeSn, outputDir, filePrefix);

                var result = await _testReportGenerator!.GenerateCombinedAsync(new CombinedTestReportRequest
                {
                    Sections = batch.Sections,
                    OutputDirectory = outputDir,
                    FilePrefix = filePrefix,
                    Formats = ReportExportFormats.Html | ReportExportFormats.Pdf
                }, cancellationToken).ConfigureAwait(false);

                if (result.Success)
                    _logger?.LogInformation("合并报告已生成: {Path}", result.HtmlPath ?? result.PdfPath);
                else
                    _logger?.LogWarning("合并报告生成失败。");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "合并归档异常");
            }
        }

        /// <summary>
        /// 主段 <see cref="MasterExecutionPlan.OrderedEntries"/> 次序后接 ExecuteLast 段次序，供合并报告按编排顺序排列工况。
        /// </summary>
        private static Dictionary<string, int> BuildSectionOrderMap(
            MasterExecutionPlan mainPlan,
            MasterExecutionPlan finallyPlan)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            var i = 0;
            foreach (var e in mainPlan.OrderedEntries)
            {
                var id = e.RefId?.Trim();
                if (string.IsNullOrEmpty(id)) continue;
                map[id] = i++;
            }

            foreach (var e in finallyPlan.OrderedEntries)
            {
                var id = e.RefId?.Trim();
                if (string.IsNullOrEmpty(id)) continue;
                map[id] = i++;
            }

            return map;
        }

        // ── 执行计划过滤 ──────────────────────────────────────

        private static MasterExecutionPlan FilterPlan(MasterExecutionPlan source, bool executeLast)
        {
            var entries = source.Entries.Values
                .Where(e => e.ExecuteLast == executeLast)
                .ToDictionary(
                    e => e.RefId,
                    e => new SubWorkflowEntry
                    {
                        RefId = e.RefId,
                        DisplayName = e.DisplayName,
                        Workflow = e.Workflow,
                        IsEnabled = e.IsEnabled,
                        ContinueOnFailure = e.ContinueOnFailure,
                        ExecuteLast = e.ExecuteLast
                    },
                    StringComparer.Ordinal);

            var ordered = source.OrderedEntries
                .Where(e => entries.ContainsKey(e.RefId))
                .Select(e => entries[e.RefId])
                .ToList();

            var successors = entries.Keys.ToDictionary(
                id => id, _ => new List<string>(), StringComparer.Ordinal);

            foreach (var srcId in entries.Keys)
            {
                if (!source.Successors.TryGetValue(srcId, out var outs))
                    continue;
                foreach (var tgtId in outs)
                {
                    if (!entries.ContainsKey(tgtId)) continue;
                    successors[srcId].Add(tgtId);
                    entries[tgtId].PredecessorIds.Add(srcId);
                }
            }

            return new MasterExecutionPlan
            {
                Entries = entries,
                OrderedEntries = ordered,
                Successors = successors,
                HasDependencies = successors.Values.Sum(l => l.Count) > 0
            };
        }

        // ── 进度事件 ──────────────────────────────────────────

        private void RaiseProgress(SubWorkflowEntry entry, SubWorkflowState state, ExecutionResult? result = null)
        {
            SubWorkflowProgressChanged?.Invoke(this, new SubWorkflowProgressEventArgs
            {
                RefId = entry.RefId,
                DisplayName = entry.DisplayName,
                State = state,
                Result = result
            });
        }

        private void RaiseParallelRunningGroup(IReadOnlyList<SubWorkflowParallelStartItem> items, string? scopeWorkflowKey)
        {
            if (items == null || items.Count == 0)
                return;

            SubWorkflowProgressChanged?.Invoke(this, new SubWorkflowProgressEventArgs
            {
                RefId = items[0].RefId,
                DisplayName = items[0].DisplayName,
                State = SubWorkflowState.Running,
                ScopeWorkflowKey = scopeWorkflowKey,
                ParallelRunningGroup = items
            });
        }

        private static void TryAssignMasterWorkflowReferenceLastResult(
            MasterExecutionOptions? options,
            SubWorkflowEntry entry,
            ExecutionResult? workflowResult)
        {
            var nodes = options?.MasterCanvasRuntime?.Nodes;
            if (nodes == null || workflowResult == null || string.IsNullOrWhiteSpace(entry.RefId))
                return;

            foreach (var n in nodes)
            {
                if (n is WorkflowReferenceNode wfn && string.Equals(wfn.Id, entry.RefId, StringComparison.Ordinal))
                {
                    wfn.LastExecutionResult = workflowResult;
                    return;
                }
            }
        }

        // ── 合并归档文件写入工具 ──────────────────────────────

        private static void WriteCombinedRunRecordJson(
            IReadOnlyList<(WorkFlowRunRecord Record, string Condition)> records,
            string sn, string outputDir, string filePrefix)
        {
            if (records.Count == 0) return;

            var combined = new
            {
                SN = sn,
                StartTime = records.Min(r => r.Record.StartTime),
                EndTime = records.Max(r => r.Record.EndTime),
                OverallResult = records.All(r => r.Record.FinalResult?.Success == true) ? "OK" : "NG",
                SubWorkflows = records.Select(r => new
                {
                    r.Condition,
                    r.Record.WorkFlowName,
                    r.Record.Strategy,
                    r.Record.StartTime,
                    r.Record.EndTime,
                    r.Record.Status,
                    FinalResult = r.Record.FinalResult?.Success == true ? "OK" : "NG",
                    NodeRuns = r.Record.NodeRuns
                }).ToList()
            };

            var json = JsonSerializer.Serialize(combined, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            });
            var path = Path.Combine(outputDir, $"{filePrefix}_run_record.json");
            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
    }
}
