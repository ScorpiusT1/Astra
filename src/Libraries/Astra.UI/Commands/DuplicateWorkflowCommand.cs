using Astra.UI.Models;
using Astra.Core.Nodes.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 复制子流程命令 - 支持撤销/重做
    /// 
    /// 功能：
    /// 1. 克隆源子流程的所有节点和连线
    /// 2. 自动生成新的子流程名称（附加序号）
    /// 3. 创建新的 WorkflowTab 并添加到集合
    /// 4. 支持撤销（移除新创建的子流程）
    /// </summary>
    public class DuplicateWorkflowCommand : UndoableCommandBase
    {
        private readonly WorkflowTab _sourceWorkflowTab;
        private readonly ObservableCollection<WorkflowTab> _workflowTabs;
        private readonly ObservableCollection<WorkflowTab> _subWorkflowTabs;
        private readonly Dictionary<string, WorkFlowNode> _subWorkflows;
        private readonly Action<WorkflowTab> _onWorkflowAdded;
        private readonly Action<WorkflowTab> _onWorkflowRemoved;

        private WorkflowTab _duplicatedWorkflowTab;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="sourceWorkflowTab">要复制的源流程标签页</param>
        /// <param name="workflowTabs">流程标签页集合</param>
        /// <param name="subWorkflowTabs">子流程标签页集合</param>
        /// <param name="subWorkflows">子流程字典</param>
        /// <param name="onWorkflowAdded">流程添加后的回调</param>
        /// <param name="onWorkflowRemoved">流程移除后的回调</param>
        public DuplicateWorkflowCommand(
            WorkflowTab sourceWorkflowTab,
            ObservableCollection<WorkflowTab> workflowTabs,
            ObservableCollection<WorkflowTab> subWorkflowTabs,
            Dictionary<string, WorkFlowNode> subWorkflows,
            Action<WorkflowTab> onWorkflowAdded = null,
            Action<WorkflowTab> onWorkflowRemoved = null)
            : base($"复制子流程 '{sourceWorkflowTab?.Name ?? "未知"}'")
        {
            _sourceWorkflowTab = sourceWorkflowTab ?? throw new ArgumentNullException(nameof(sourceWorkflowTab));
            _workflowTabs = workflowTabs ?? throw new ArgumentNullException(nameof(workflowTabs));
            _subWorkflowTabs = subWorkflowTabs ?? throw new ArgumentNullException(nameof(subWorkflowTabs));
            _subWorkflows = subWorkflows ?? throw new ArgumentNullException(nameof(subWorkflows));
            _onWorkflowAdded = onWorkflowAdded;
            _onWorkflowRemoved = onWorkflowRemoved;
        }

        /// <summary>
        /// 是否可以执行
        /// </summary>
        public override bool CanExecute(object? parameter)
        {
            return _sourceWorkflowTab != null &&
                   _workflowTabs != null &&
                   _subWorkflowTabs != null &&
                   _subWorkflows != null;
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public override bool CanUndo => _duplicatedWorkflowTab != null && _workflowTabs != null && _subWorkflowTabs != null && _subWorkflows != null;

        /// <summary>
        /// 执行命令 - 复制子流程
        /// </summary>
        public override void Execute()
        {
            if (!CanExecute(null))
                throw new InvalidOperationException("无法执行复制子流程命令：参数无效");

            // 获取源子流程数据
            var sourceSubWorkflow = _sourceWorkflowTab.GetSubWorkflow();
            if (sourceSubWorkflow == null)
                throw new InvalidOperationException("无法获取源子流程数据");

            // 克隆子流程数据（包括所有节点和连线）
            var clonedNode = sourceSubWorkflow.Clone();
            
            // 显式转换为 WorkFlowNode
            var duplicatedSubWorkflow = clonedNode as WorkFlowNode;
            if (duplicatedSubWorkflow == null)
                throw new InvalidOperationException("克隆的节点不是 WorkFlowNode 类型");

            // 复制后的子流程必须使用新ID，避免覆盖原流程导致执行计划丢流程。
            duplicatedSubWorkflow.Id = Guid.NewGuid().ToString();

            // 重建关系（确保连线引用正确）
            duplicatedSubWorkflow.RebuildRelationships();

            // 调试：打印连线信息
            System.Diagnostics.Debug.WriteLine($"[DuplicateWorkflowCommand] 源子流程连线数: {sourceSubWorkflow.Connections.Count}");
            System.Diagnostics.Debug.WriteLine($"[DuplicateWorkflowCommand] 克隆后连线数: {duplicatedSubWorkflow.Connections.Count}");
            foreach (var conn in duplicatedSubWorkflow.Connections)
            {
                System.Diagnostics.Debug.WriteLine($"[DuplicateWorkflowCommand] 连线: {conn.SourceNodeId}:{conn.SourcePortId} -> {conn.TargetNodeId}:{conn.TargetPortId}");
            }

            // 生成新的子流程名称（避免重复）
            var newName = GenerateUniqueName(_sourceWorkflowTab.Name);

            // 更新克隆的子流程信息
            duplicatedSubWorkflow.Name = newName;

            // 创建新的 WorkflowTab
            _duplicatedWorkflowTab = new WorkflowTab
            {
                Name = newName,
                Type = WorkflowType.Sub,
                IsActive = false,
                WorkflowData = duplicatedSubWorkflow
            };

            // 将节点同步到 WorkflowTab 的 Nodes 集合（用于 UI 显示）
            _duplicatedWorkflowTab.Nodes.Clear();
            foreach (var node in duplicatedSubWorkflow.Nodes)
            {
                _duplicatedWorkflowTab.Nodes.Add(node);
            }

            // 克隆源 WorkflowTab 的 Edges 集合（保持完整的路径点和所有属性）
            _duplicatedWorkflowTab.Edges.Clear();
            
            System.Diagnostics.Debug.WriteLine($"\n[DuplicateWorkflowCommand] ========== 开始复制连线 ==========");
            System.Diagnostics.Debug.WriteLine($"源 WorkflowTab.Edges 数量: {_sourceWorkflowTab.Edges.Count}");
            System.Diagnostics.Debug.WriteLine($"源 Connections 数量: {sourceSubWorkflow.Connections.Count}");
            System.Diagnostics.Debug.WriteLine($"克隆后 Connections 数量: {duplicatedSubWorkflow.Connections.Count}");
            
            // 建立旧节点ID到新节点ID的映射
            // 使用节点索引来匹配，因为 Clone() 使用 JSON 序列化，保持节点顺序
            var nodeIdMap = new Dictionary<string, string>();
            
            // 确保源节点和克隆节点数量一致
            if (sourceSubWorkflow.Nodes.Count == duplicatedSubWorkflow.Nodes.Count)
            {
                System.Diagnostics.Debug.WriteLine($"\n[DuplicateWorkflowCommand] === 建立节点ID映射 ===");
                for (int i = 0; i < sourceSubWorkflow.Nodes.Count; i++)
                {
                    var oldNodeId = sourceSubWorkflow.Nodes[i].Id;
                    var newNodeId = duplicatedSubWorkflow.Nodes[i].Id;
                    nodeIdMap[oldNodeId] = newNodeId;
                    
                    System.Diagnostics.Debug.WriteLine($"  [{i}] {sourceSubWorkflow.Nodes[i].Name}: {oldNodeId} -> {newNodeId}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DuplicateWorkflowCommand] ❌ 错误：源节点数({sourceSubWorkflow.Nodes.Count})与克隆节点数({duplicatedSubWorkflow.Nodes.Count})不一致");
            }
            
            System.Diagnostics.Debug.WriteLine($"\n[DuplicateWorkflowCommand] === 克隆 Edges ===");
            
            // 克隆源 WorkflowTab 的每一条 Edge，并更新节点ID引用
            int edgeIndex = 0;
            foreach (var sourceEdge in _sourceWorkflowTab.Edges)
            {
                System.Diagnostics.Debug.WriteLine($"[DuplicateWorkflowCommand] === 处理连线 [{edgeIndex}] ===");
                System.Diagnostics.Debug.WriteLine($"  源连线: {sourceEdge.SourceNodeId}:{sourceEdge.SourcePortId} -> {sourceEdge.TargetNodeId}:{sourceEdge.TargetPortId}");
                System.Diagnostics.Debug.WriteLine($"  路径点数: {sourceEdge.Points?.Count ?? 0}");
                
                var clonedEdge = sourceEdge.Clone(); // 使用 Edge.Clone() 方法，保留所有属性包括 Points
                
                // 更新节点ID引用（映射到克隆后的节点）
                bool sourceMapped = nodeIdMap.TryGetValue(sourceEdge.SourceNodeId, out var newSourceId);
                bool targetMapped = nodeIdMap.TryGetValue(sourceEdge.TargetNodeId, out var newTargetId);
                
                System.Diagnostics.Debug.WriteLine($"  源节点映射: {sourceMapped} ({sourceEdge.SourceNodeId} -> {newSourceId})");
                System.Diagnostics.Debug.WriteLine($"  目标节点映射: {targetMapped} ({sourceEdge.TargetNodeId} -> {newTargetId})");
                
                if (sourceMapped)
                {
                    clonedEdge.SourceNodeId = newSourceId;
                    
                    // 更新源端口ID（端口ID格式通常是 {NodeId}:{PortDirection}）
                    if (!string.IsNullOrEmpty(sourceEdge.SourcePortId) && sourceEdge.SourcePortId.Contains(":"))
                    {
                        var portParts = sourceEdge.SourcePortId.Split(':');
                        if (portParts.Length >= 2)
                        {
                            var oldPortId = clonedEdge.SourcePortId;
                            clonedEdge.SourcePortId = $"{newSourceId}:{portParts[1]}";
                            System.Diagnostics.Debug.WriteLine($"  源端口ID更新: {oldPortId} -> {clonedEdge.SourcePortId}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  源端口ID格式不符合预期: {sourceEdge.SourcePortId}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  ⚠️ 警告：源节点ID未找到映射！");
                }
                
                if (targetMapped)
                {
                    clonedEdge.TargetNodeId = newTargetId;
                    
                    // 更新目标端口ID
                    if (!string.IsNullOrEmpty(sourceEdge.TargetPortId) && sourceEdge.TargetPortId.Contains(":"))
                    {
                        var portParts = sourceEdge.TargetPortId.Split(':');
                        if (portParts.Length >= 2)
                        {
                            var oldPortId = clonedEdge.TargetPortId;
                            clonedEdge.TargetPortId = $"{newTargetId}:{portParts[1]}";
                            System.Diagnostics.Debug.WriteLine($"  目标端口ID更新: {oldPortId} -> {clonedEdge.TargetPortId}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  目标端口ID格式不符合预期: {sourceEdge.TargetPortId}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  ⚠️ 警告：目标节点ID未找到映射！");
                }
                
                // 🔧 关键：设置标志，告诉连线系统不要重新计算路径
                // 这样可以完全保留克隆的路径形状，避免 A* 算法生成不同的路径
                clonedEdge.PreservePathOnRefresh = true;
                
                _duplicatedWorkflowTab.Edges.Add(clonedEdge);
                System.Diagnostics.Debug.WriteLine($"  ✅ 克隆连线完成: {clonedEdge.SourceNodeId}:{clonedEdge.SourcePortId} -> {clonedEdge.TargetNodeId}:{clonedEdge.TargetPortId}");
                
                edgeIndex++;
            }
            
            System.Diagnostics.Debug.WriteLine($"[DuplicateWorkflowCommand] 连线克隆完成，源Edges: {_sourceWorkflowTab.Edges.Count}, 克隆Edges: {_duplicatedWorkflowTab.Edges.Count}");

            // 添加到子流程字典
            if (!_subWorkflows.ContainsKey(duplicatedSubWorkflow.Id))
            {
                _subWorkflows[duplicatedSubWorkflow.Id] = duplicatedSubWorkflow;
            }

            // 添加到流程标签页集合
            _workflowTabs.Add(_duplicatedWorkflowTab);

            // 添加到子流程标签页集合
            _subWorkflowTabs.Add(_duplicatedWorkflowTab);

            // 执行回调（自动切换到新创建的子流程）
            _onWorkflowAdded?.Invoke(_duplicatedWorkflowTab);

            System.Diagnostics.Debug.WriteLine($"[DuplicateWorkflowCommand] 复制子流程成功: {newName}, 节点数: {duplicatedSubWorkflow.Nodes.Count}, 连线数: {duplicatedSubWorkflow.Connections.Count}");
        }

        /// <summary>
        /// 撤销命令 - 移除复制的子流程
        /// </summary>
        public override void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException("无法撤销复制子流程命令：复制的流程未创建");

            // 获取子流程数据
            var subWorkflow = _duplicatedWorkflowTab.GetSubWorkflow();
            if (subWorkflow != null && !string.IsNullOrEmpty(subWorkflow.Id))
            {
                // 从子流程字典中移除
                if (_subWorkflows.ContainsKey(subWorkflow.Id))
                {
                    _subWorkflows.Remove(subWorkflow.Id);
                }
            }

            // 从子流程标签页集合中移除
            if (_subWorkflowTabs.Contains(_duplicatedWorkflowTab))
            {
                _subWorkflowTabs.Remove(_duplicatedWorkflowTab);
            }

            // 从流程标签页集合中移除
            if (_workflowTabs.Contains(_duplicatedWorkflowTab))
            {
                _workflowTabs.Remove(_duplicatedWorkflowTab);
            }

            // 执行回调
            _onWorkflowRemoved?.Invoke(_duplicatedWorkflowTab);

            System.Diagnostics.Debug.WriteLine($"[DuplicateWorkflowCommand] 撤销复制子流程: {_duplicatedWorkflowTab.Name}");
        }

        /// <summary>
        /// 生成唯一的流程名称
        /// </summary>
        private string GenerateUniqueName(string baseName)
        {
            // 移除基础名称中已有的 " - 副本" 或 " - 副本(N)" 后缀
            var cleanBaseName = System.Text.RegularExpressions.Regex.Replace(baseName, @" - 副本(\(\d+\))?$", "");

            // 查找所有以 cleanBaseName 开头的流程
            var existingNames = _workflowTabs
                .Select(t => t.Name)
                .Where(n => n.StartsWith(cleanBaseName))
                .ToHashSet();

            // 如果基础名称本身就不存在，直接使用 " - 副本"
            if (!existingNames.Contains($"{cleanBaseName} - 副本"))
            {
                return $"{cleanBaseName} - 副本";
            }

            // 否则，查找下一个可用的序号
            int counter = 2;
            string candidateName;
            do
            {
                candidateName = $"{cleanBaseName} - 副本({counter})";
                counter++;
            }
            while (existingNames.Contains(candidateName));

            return candidateName;
        }
    }
}

