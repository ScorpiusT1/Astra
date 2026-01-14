using Astra.UI.Models;
using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 删除流程命令 - 支持撤销/重做
    /// 
    /// 设计原则：
    /// 1. 单一职责原则：专门负责流程的删除
    /// 2. 开闭原则：通过继承基类扩展，无需修改基类
    /// </summary>
    public class RemoveWorkflowCommand : UndoableCommandBase
    {
        private readonly WorkflowTab _workflowTab;
        private readonly ObservableCollection<WorkflowTab> _workflowTabs;
        private readonly ObservableCollection<WorkflowTab> _subWorkflowTabs;
        private readonly Dictionary<string, WorkFlowNode> _subWorkflows;
        private readonly MasterWorkflow _masterWorkflow;
        private readonly WorkflowTab _masterWorkflowTab;
        private readonly Action<WorkflowTab> _onWorkflowRemoved;
        private readonly Action<WorkflowTab> _onWorkflowAdded;
        private readonly Action _refreshMasterWorkflowNodes;
        private readonly bool _isMasterWorkflowViewVisible;
        
        // 保存删除时的状态，用于撤销
        private WorkflowReferenceNode _removedNode;
        private List<Edge> _removedEdges;
        private WorkflowReference _removedReference;
        private int _removedNodeIndex;
        private WorkflowTab _previousCurrentTab;

        /// <summary>
        /// 构造函数
        /// </summary>
        public RemoveWorkflowCommand(
            WorkflowTab workflowTab,
            ObservableCollection<WorkflowTab> workflowTabs,
            ObservableCollection<WorkflowTab> subWorkflowTabs,
            Dictionary<string, WorkFlowNode> subWorkflows,
            MasterWorkflow masterWorkflow,
            WorkflowTab masterWorkflowTab,
            bool isMasterWorkflowViewVisible,
            Action<WorkflowTab> onWorkflowRemoved = null,
            Action<WorkflowTab> onWorkflowAdded = null,
            Action refreshMasterWorkflowNodes = null)
            : base($"删除流程 '{workflowTab?.Name ?? "未知"}'")
        {
            _workflowTab = workflowTab ?? throw new ArgumentNullException(nameof(workflowTab));
            _workflowTabs = workflowTabs ?? throw new ArgumentNullException(nameof(workflowTabs));
            _subWorkflowTabs = subWorkflowTabs ?? throw new ArgumentNullException(nameof(subWorkflowTabs));
            _subWorkflows = subWorkflows ?? throw new ArgumentNullException(nameof(subWorkflows));
            _masterWorkflow = masterWorkflow;
            _masterWorkflowTab = masterWorkflowTab;
            _isMasterWorkflowViewVisible = isMasterWorkflowViewVisible;
            _onWorkflowRemoved = onWorkflowRemoved;
            _onWorkflowAdded = onWorkflowAdded;
            _refreshMasterWorkflowNodes = refreshMasterWorkflowNodes;
        }

        /// <summary>
        /// 是否可以执行
        /// </summary>
        public override bool CanExecute(object? parameter)
        {
            return _workflowTab != null &&
                   _workflowTabs != null &&
                   _subWorkflowTabs != null &&
                   _subWorkflows != null &&
                   _workflowTabs.Contains(_workflowTab);
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public override bool CanUndo => _workflowTabs != null && _subWorkflowTabs != null && _subWorkflows != null;

        /// <summary>
        /// 执行命令 - 从集合中移除流程
        /// </summary>
        public override void Execute()
        {
            if (!CanExecute(null))
                throw new InvalidOperationException("无法执行删除流程命令：流程不存在于集合中");

            // 保存当前标签页（用于撤销时恢复）
            _previousCurrentTab = null; // 将在外部设置

            // 如果是子流程，处理主流程引用和节点
            if (_workflowTab.Type == WorkflowType.Sub)
            {
                var subWorkflow = _workflowTab.GetSubWorkflow();
                if (subWorkflow != null && !string.IsNullOrEmpty(subWorkflow.Id))
                {
                    var subWorkflowId = subWorkflow.Id;

                    // 保存要删除的引用（用于撤销）- 需要克隆，因为删除后引用对象会被移除
                    if (_masterWorkflow != null)
                    {
                        var referenceToRemove = _masterWorkflow.SubWorkflowReferences
                            .FirstOrDefault(r => r.SubWorkflowId == subWorkflowId);
                        if (referenceToRemove != null)
                        {
                            // 使用 JSON 序列化/反序列化来克隆引用对象
                            var json = System.Text.Json.JsonSerializer.Serialize(referenceToRemove);
                            _removedReference = System.Text.Json.JsonSerializer.Deserialize<WorkflowReference>(json);
                        }
                    }

                    // 保存要删除的节点和连线（用于撤销）
                    if (_masterWorkflowTab != null)
                    {
                        var nodeToRemove = _masterWorkflowTab.Nodes.OfType<WorkflowReferenceNode>()
                            .FirstOrDefault(n => n.SubWorkflowId == subWorkflowId);
                        
                        if (nodeToRemove != null)
                        {
                            _removedNodeIndex = _masterWorkflowTab.Nodes.IndexOf(nodeToRemove);
                            
                            // 克隆节点（因为删除后节点对象会被移除）
                            _removedNode = nodeToRemove.Clone() as WorkflowReferenceNode;
                            // 恢复原来的 ID（撤销时需要保持原来的 ID）
                            if (_removedNode != null)
                            {
                                _removedNode.Id = nodeToRemove.Id;
                            }
                            
                            // 保存要删除的连线（需要克隆，因为删除后连线对象会被移除）
                            var edgesToRemove = _masterWorkflowTab.Edges
                                .Where(e => e.SourceNodeId == nodeToRemove.Id || e.TargetNodeId == nodeToRemove.Id)
                                .ToList();
                            _removedEdges = edgesToRemove.Select(e =>
                            {
                                var clonedEdge = e.Clone();
                                // 恢复原来的 ID（撤销时需要保持原来的 ID）
                                clonedEdge.Id = e.Id;
                                return clonedEdge;
                            }).ToList();
                        }
                    }

                    // 从子流程字典中移除
                    if (_subWorkflows.ContainsKey(subWorkflowId))
                    {
                        _subWorkflows.Remove(subWorkflowId);
                    }

                    // 从主流程引用中删除
                    if (_masterWorkflow != null)
                    {
                        _masterWorkflow.RemoveSubWorkflowReference(subWorkflowId);
                    }

                    // 从主流程节点中删除
                    if (_masterWorkflowTab != null && _removedNode != null)
                    {
                        // 删除连线
                        foreach (var edge in _removedEdges ?? new List<Edge>())
                        {
                            _masterWorkflowTab.Edges.Remove(edge);
                        }

                        // 删除节点
                        _masterWorkflowTab.Nodes.Remove(_removedNode);
                    }
                }
            }

            // 从子流程标签页集合中移除
            if (_subWorkflowTabs.Contains(_workflowTab))
            {
                _subWorkflowTabs.Remove(_workflowTab);
            }

            // 从流程标签页集合中移除
            if (_workflowTabs.Contains(_workflowTab))
            {
                _workflowTabs.Remove(_workflowTab);
            }

            // 执行回调
            _onWorkflowRemoved?.Invoke(_workflowTab);
            
            // 刷新主流程节点
            if (_isMasterWorkflowViewVisible)
            {
                _refreshMasterWorkflowNodes?.Invoke();
            }
        }

        /// <summary>
        /// 撤销命令 - 恢复流程到集合
        /// </summary>
        public override void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException("无法撤销删除流程命令：集合无效");

            // 恢复流程标签页
            if (!_workflowTabs.Contains(_workflowTab))
            {
                _workflowTabs.Add(_workflowTab);
            }

            // 如果是子流程，恢复到子流程标签页集合
            if (_workflowTab.Type == WorkflowType.Sub && !_subWorkflowTabs.Contains(_workflowTab))
            {
                _subWorkflowTabs.Add(_workflowTab);
            }

            // 如果是子流程，恢复主流程引用和节点
            if (_workflowTab.Type == WorkflowType.Sub)
            {
                var subWorkflow = _workflowTab.GetSubWorkflow();
                if (subWorkflow != null && !string.IsNullOrEmpty(subWorkflow.Id))
                {
                    var subWorkflowId = subWorkflow.Id;

                    // 恢复到子流程字典
                    if (!_subWorkflows.ContainsKey(subWorkflowId))
                    {
                        _subWorkflows[subWorkflowId] = subWorkflow;
                    }

                    // 恢复主流程引用
                    if (_masterWorkflow != null && _removedReference != null)
                    {
                        if (!_masterWorkflow.SubWorkflowReferences.Any(r => r.Id == _removedReference.Id))
                        {
                            _masterWorkflow.SubWorkflowReferences.Add(_removedReference);
                        }
                    }

                    // 恢复主流程节点和连线
                    if (_masterWorkflowTab != null && _removedNode != null)
                    {
                        // 恢复节点（在原来的位置）
                        if (!_masterWorkflowTab.Nodes.Contains(_removedNode))
                        {
                            if (_removedNodeIndex >= 0 && _removedNodeIndex < _masterWorkflowTab.Nodes.Count)
                            {
                                _masterWorkflowTab.Nodes.Insert(_removedNodeIndex, _removedNode);
                            }
                            else
                            {
                                _masterWorkflowTab.Nodes.Add(_removedNode);
                            }
                        }

                        // 恢复连线
                        if (_removedEdges != null)
                        {
                            foreach (var edge in _removedEdges)
                            {
                                if (!_masterWorkflowTab.Edges.Contains(edge))
                                {
                                    _masterWorkflowTab.Edges.Add(edge);
                                }
                            }
                        }
                    }
                }
            }

            // 执行回调（传递恢复的标签页，用于切换）
            _onWorkflowAdded?.Invoke(_workflowTab);
            
            // 刷新主流程节点
            if (_isMasterWorkflowViewVisible)
            {
                _refreshMasterWorkflowNodes?.Invoke();
            }
        }
    }
}

