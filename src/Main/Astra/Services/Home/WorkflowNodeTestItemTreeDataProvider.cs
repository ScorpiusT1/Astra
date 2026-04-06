using Astra.Configuration;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Serialization;
using Astra.UI.Abstractions.Nodes;
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

            var subWorkflows = loadResult.Data.SubWorkflows?.Values?.ToList() ?? new List<WorkFlowNode>();
            var master = loadResult.Data.MasterWorkflow;

            // 根节点顺序：按主流程画布 Flow/Data 边拓扑排序（与编排器依赖一致）；未出现在主流程上的子流程仍排在后面。
            return BuildHomeTestTreeRootsOrderedByMasterTopology(master, subWorkflows, loadResult.Data);
        }

        /// <summary>
        /// 构建首页测试树根列表：主流程上可见的「引用块 + 插件节点」按 <see cref="MasterWorkflow.Edges"/> 拓扑排序；
        /// 每个引用块一行根；多引用指向同一子流程定义时仍各占一行，且 <see cref="TestTreeNodeItem.SubWorkflowId"/> 取引用 <see cref="WorkflowReference.Id"/> 以便与并行执行事件对齐。
        /// </summary>
        private static List<TestTreeNodeItem> BuildHomeTestTreeRootsOrderedByMasterTopology(
            MasterWorkflow? master,
            List<WorkFlowNode> subWorkflows,
            MultiWorkflowData data)
        {
            var roots = new List<TestTreeNodeItem>();
            if (master == null || string.IsNullOrWhiteSpace(master.Id))
            {
                AppendSubWorkflowRootsLegacyOrder(roots, master, subWorkflows, data);
                return roots;
            }

            var masterKey = master.Id;
            var declarationOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = 0;

            // 并列（同层入度为 0）时：先按主流程引用块在脚本中的顺序，再按插件在脚本中的顺序，避免与画布拓扑无关的插件抢到子流程前面。
            var pluginRootByCanvasId = new Dictionary<string, TestTreeNodeItem>(StringComparer.Ordinal);
            // 每个主流程「引用块」一行根，键为主画布节点 Id（与编排器 SubWorkflowEntry.RefId、执行上下文 WorkFlowKey 一致）
            var subRootByRefCanvasId = new Dictionary<string, TestTreeNodeItem>(StringComparer.Ordinal);
            var refCanvasIdToSubWorkflowId = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var reference in master.SubWorkflowReferences ?? new List<WorkflowReference>())
            {
                if (reference == null || string.IsNullOrWhiteSpace(reference.Id) || string.IsNullOrWhiteSpace(reference.SubWorkflowId))
                    continue;
                if (!data.SubWorkflows.TryGetValue(reference.SubWorkflowId, out var workflow) || workflow == null)
                    continue;

                var showFromWorkflow = workflow.ShowInHomeTestItems;
                var showFromReference = reference.ShowInHomeTestItems;
                if (!showFromWorkflow || !showFromReference)
                    continue;

                refCanvasIdToSubWorkflowId[reference.Id] = reference.SubWorkflowId;
                if (!declarationOrder.ContainsKey(reference.Id))
                    declarationOrder[reference.Id] = order++;

                subRootByRefCanvasId[reference.Id] = CreateSubWorkflowGroupRoot(workflow, reference.Id);
            }

            foreach (var plugin in master.MasterPluginNodes ?? new List<Node>())
            {
                if (plugin == null || string.IsNullOrWhiteSpace(plugin.Id) || !plugin.ShowInHomeTestItems)
                    continue;
                if (pluginRootByCanvasId.ContainsKey(plugin.Id))
                    continue;

                pluginRootByCanvasId[plugin.Id] = new TestTreeNodeItem
                {
                    NodeId = plugin.Id,
                    SubWorkflowId = masterKey,
                    Name = string.IsNullOrWhiteSpace(plugin.Name) ? plugin.NodeType ?? "主流程节点" : plugin.Name,
                    Status = "Ready",
                    TestTime = DateTime.Now,
                    ActualValue = 0,
                    LowerLimit = 0,
                    UpperLimit = 0,
                    IsRoot = true,
                    SupportsHomeChartButton = ComputeSupportsHomeChartButton(plugin)
                };
                if (!declarationOrder.ContainsKey(plugin.Id))
                    declarationOrder[plugin.Id] = order++;
            }

            var vertices = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in pluginRootByCanvasId.Keys)
                vertices.Add(id);
            foreach (var id in refCanvasIdToSubWorkflowId.Keys)
                vertices.Add(id);

            if (vertices.Count == 0)
            {
                AppendSubWorkflowRootsLegacyOrder(roots, master, subWorkflows, data);
                return roots;
            }

            var sortedCanvasIds = SortMasterCanvasNodeIdsTopologically(vertices, master.Edges, declarationOrder);
            var emittedSubWorkflowIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var canvasId in sortedCanvasIds)
            {
                if (pluginRootByCanvasId.TryGetValue(canvasId, out var pluginRoot))
                {
                    roots.Add(pluginRoot);
                    continue;
                }

                if (!refCanvasIdToSubWorkflowId.TryGetValue(canvasId, out var subId))
                    continue;
                if (!subRootByRefCanvasId.TryGetValue(canvasId, out var subRoot))
                    continue;
                roots.Add(subRoot);
                emittedSubWorkflowIds.Add(subId);
            }

            AppendOrphanSubWorkflowRoots(roots, master, subWorkflows, data, emittedSubWorkflowIds);
            return roots;
        }

        /// <param name="homeExecutionScopeId">
        /// 主页执行与引擎事件对齐的作用域键：有主流程引用时为引用节点 <see cref="WorkflowReference.Id"/>，否则为子流程 <see cref="WorkFlowNode.Id"/>。
        /// </param>
        private static TestTreeNodeItem CreateSubWorkflowGroupRoot(WorkFlowNode workflow, string homeExecutionScopeId)
        {
            var scope = string.IsNullOrWhiteSpace(homeExecutionScopeId)
                ? workflow.Id ?? string.Empty
                : homeExecutionScopeId.Trim();

            var root = new TestTreeNodeItem
            {
                Name = string.IsNullOrWhiteSpace(workflow.Name) ? "未命名流程" : workflow.Name,
                Status = "Ready",
                IsRoot = true,
                TestTime = DateTime.Now,
                SubWorkflowId = scope,
                // 与主流程引用 RefId 一致，便于编排器并行波次（ParallelRunningGroup）更新根行状态
                NodeId = string.IsNullOrWhiteSpace(scope) ? string.Empty : scope
            };

            var orderedNodes = SortNodesByTopology(workflow);
            foreach (var node in orderedNodes)
            {
                if (node == null || !node.ShowInHomeTestItems)
                    continue;

                root.Children.Add(new TestTreeNodeItem
                {
                    NodeId = node.Id,
                    SubWorkflowId = scope,
                    Name = string.IsNullOrWhiteSpace(node.Name) ? node.NodeType ?? "未命名节点" : node.Name,
                    Status = MapStatus(node.ExecutionState),
                    TestTime = DateTime.Now,
                    ActualValue = 0,
                    LowerLimit = 0,
                    UpperLimit = 0,
                    IsRoot = false,
                    SupportsHomeChartButton = ComputeSupportsHomeChartButton(node)
                });
            }

            return root;
        }

        /// <summary>主流程无拓扑顶点时的回退：优先按主流程引用块各建一行（与同脚本拓扑模式一致）。</summary>
        private static void AppendSubWorkflowRootsLegacyOrder(
            List<TestTreeNodeItem> roots,
            MasterWorkflow? master,
            List<WorkFlowNode> subWorkflows,
            MultiWorkflowData? data)
        {
            if (master?.SubWorkflowReferences != null && data?.SubWorkflows != null)
            {
                foreach (var reference in master.SubWorkflowReferences)
                {
                    if (reference == null ||
                        string.IsNullOrWhiteSpace(reference.Id) ||
                        string.IsNullOrWhiteSpace(reference.SubWorkflowId))
                        continue;
                    if (!data.SubWorkflows.TryGetValue(reference.SubWorkflowId, out var wf) || wf == null)
                        continue;
                    var showW = wf.ShowInHomeTestItems;
                    var showR = reference.ShowInHomeTestItems;
                    if (!showW || !showR)
                        continue;
                    roots.Add(CreateSubWorkflowGroupRoot(wf, reference.Id));
                }

                if (roots.Count > 0)
                    return;
            }

            var refBySubId = (master?.SubWorkflowReferences ?? Enumerable.Empty<WorkflowReference>())
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.SubWorkflowId))
                .ToDictionary(r => r.SubWorkflowId!, r => r, StringComparer.Ordinal);

            foreach (var workflow in subWorkflows)
            {
                if (workflow == null)
                    continue;
                var showFromWorkflow = workflow.ShowInHomeTestItems;
                var showFromReference = true;
                if (refBySubId.TryGetValue(workflow.Id, out var reference))
                    showFromReference = reference.ShowInHomeTestItems;
                if (!showFromWorkflow || !showFromReference)
                    continue;
                roots.Add(CreateSubWorkflowGroupRoot(workflow, workflow.Id ?? string.Empty));
            }
        }

        /// <summary>已在主流程引用中输出过的子流程不再重复；其余可见子流程按原列表顺序追加。</summary>
        private static void AppendOrphanSubWorkflowRoots(
            List<TestTreeNodeItem> roots,
            MasterWorkflow master,
            List<WorkFlowNode> subWorkflows,
            MultiWorkflowData data,
            HashSet<string> emittedSubWorkflowIds)
        {
            var refBySubId = (master.SubWorkflowReferences ?? Enumerable.Empty<WorkflowReference>())
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.SubWorkflowId))
                .ToDictionary(r => r.SubWorkflowId!, r => r, StringComparer.Ordinal);

            foreach (var workflow in subWorkflows)
            {
                if (workflow == null || string.IsNullOrWhiteSpace(workflow.Id))
                    continue;
                if (emittedSubWorkflowIds.Contains(workflow.Id))
                    continue;

                var showFromWorkflow = workflow.ShowInHomeTestItems;
                var showFromReference = true;
                if (refBySubId.TryGetValue(workflow.Id, out var reference))
                    showFromReference = reference.ShowInHomeTestItems;
                if (!showFromWorkflow || !showFromReference)
                    continue;

                if (!data.SubWorkflows.ContainsKey(workflow.Id))
                    continue;

                roots.Add(CreateSubWorkflowGroupRoot(workflow, workflow.Id ?? string.Empty));
            }
        }

        /// <summary>与 <see cref="SortNodesByTopology"/> 相同策略：全边依赖 + Kahn，同层按清单声明序再按 Id。</summary>
        private static List<string> SortMasterCanvasNodeIdsTopologically(
            HashSet<string> vertices,
            IEnumerable<Edge>? edges,
            IReadOnlyDictionary<string, int> declarationOrder)
        {
            var indegree = vertices.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
            var adjacency = vertices.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);

            foreach (var e in edges ?? Enumerable.Empty<Edge>())
            {
                if (e == null ||
                    string.IsNullOrWhiteSpace(e.SourceNodeId) ||
                    string.IsNullOrWhiteSpace(e.TargetNodeId))
                    continue;
                if (string.Equals(e.SourceNodeId, e.TargetNodeId, StringComparison.Ordinal))
                    continue;
                if (!vertices.Contains(e.SourceNodeId) || !vertices.Contains(e.TargetNodeId))
                    continue;

                adjacency[e.SourceNodeId].Add(e.TargetNodeId);
                indegree[e.TargetNodeId]++;
            }

            int OrderKey(string id) =>
                declarationOrder.TryGetValue(id, out var o) ? o : int.MaxValue;

            var ordered = new List<string>(vertices.Count);
            var queue = vertices
                .Where(id => indegree[id] == 0)
                .OrderBy(OrderKey)
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList();

            while (queue.Count > 0)
            {
                var current = queue[0];
                queue.RemoveAt(0);
                ordered.Add(current);

                foreach (var targetId in adjacency[current])
                {
                    indegree[targetId]--;
                    if (indegree[targetId] == 0)
                        queue.Add(targetId);
                }

                queue = queue.OrderBy(OrderKey).ThenBy(id => id, StringComparer.Ordinal).ToList();
            }

            if (ordered.Count >= vertices.Count)
                return ordered;

            var inOrdered = new HashSet<string>(ordered, StringComparer.Ordinal);
            foreach (var id in vertices.OrderBy(OrderKey).ThenBy(id => id, StringComparer.Ordinal))
            {
                if (!inOrdered.Contains(id))
                    ordered.Add(id);
            }

            return ordered;
        }

        /// <summary>
        /// 主页「打开图表」：优先尊重 <see cref="IHomeTestItemChartNode.ShowHomeChartButton"/>；否则凡标记为 <see cref="IHomeTestItemChartEligibleNode"/> 的节点类型在树加载后即可显示按钮。
        /// </summary>
        private static bool ComputeSupportsHomeChartButton(Node node)
        {
            if (node is IHomeTestItemChartNode cap)
                return cap.ShowHomeChartButton;

            return node is IHomeTestItemChartEligibleNode;
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

        private static IReadOnlyList<Node> SortNodesByTopology(WorkFlowNode workflow)
        {
            var nodes = (workflow.Nodes ?? new List<Node>())
                .Where(n => n != null && !string.IsNullOrWhiteSpace(n.Id))
                .ToList();
            if (nodes.Count <= 1)
                return nodes;

            var nodeById = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            var originalIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < nodes.Count; i++)
            {
                originalIndex[nodes[i].Id] = i;
            }

            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                indegree[node.Id] = 0;
                adjacency[node.Id] = new List<string>();
            }

            // 与 DefaultStrategyDetector / GraphAnalyzer / ComplexGraphExecutionStrategy 一致：
            // 调度依赖包含 Flow 与 Data 等全部连线，否则仅存在数据连线时拓扑退化为「节点创建顺序」。
            var dependencyConnections = (workflow.Connections ?? new List<Connection>())
                .Where(c =>
                    c != null &&
                    !string.IsNullOrWhiteSpace(c.SourceNodeId) &&
                    !string.IsNullOrWhiteSpace(c.TargetNodeId) &&
                    !string.Equals(c.SourceNodeId, c.TargetNodeId, StringComparison.Ordinal) &&
                    nodeById.ContainsKey(c.SourceNodeId) &&
                    nodeById.ContainsKey(c.TargetNodeId))
                .ToList();

            foreach (var connection in dependencyConnections)
            {
                var source = connection.SourceNodeId;
                var target = connection.TargetNodeId;
                adjacency[source].Add(target);
                indegree[target]++;
            }

            var ordered = new List<Node>(nodes.Count);
            var queue = nodes
                .Where(n => indegree[n.Id] == 0)
                .OrderBy(n => originalIndex[n.Id])
                .ToList();

            while (queue.Count > 0)
            {
                var current = queue[0];
                queue.RemoveAt(0);
                ordered.Add(current);

                foreach (var targetId in adjacency[current.Id])
                {
                    indegree[targetId]--;
                    if (indegree[targetId] == 0)
                    {
                        queue.Add(nodeById[targetId]);
                    }
                }

                queue = queue
                    .OrderBy(n => originalIndex[n.Id])
                    .ToList();
            }

            if (ordered.Count == nodes.Count)
                return ordered;

            // 图中存在环时，先保留已排好的 DAG 部分，再按原始顺序补齐未入队节点。
            var orderedIds = new HashSet<string>(ordered.Select(n => n.Id), StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (!orderedIds.Contains(node.Id))
                {
                    ordered.Add(node);
                }
            }

            return ordered;
        }
    }
}
