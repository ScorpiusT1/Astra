using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Geometry;
using Astra.UI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 在主流程中添加子流程节点命令 - 支持撤销/重做
    /// 如果子流程不存在，会自动创建子流程（统一处理添加流程和添加节点）
    /// 
    /// 设计原则：
    /// 1. 单一职责原则：专门负责在主流程中添加子流程节点（包含创建子流程）
    /// 2. 开闭原则：通过继承基类扩展，无需修改基类
    /// </summary>
    public class AddSubWorkflowNodeCommand : UndoableCommandBase
    {
        private readonly string _subWorkflowId;
        private readonly string _subWorkflowName;
        private readonly MasterWorkflow _masterWorkflow;
        private readonly ObservableCollection<Node> _nodes;
        private readonly ObservableCollection<WorkflowTab> _workflowTabs;
        private readonly ObservableCollection<WorkflowTab> _subWorkflowTabs;
        private readonly Dictionary<string, WorkFlowNode> _subWorkflows;
        private readonly Point2D _position;
        private readonly Action<WorkflowTab> _onWorkflowCreated;
        private readonly Action _onNodeAdded;
        private readonly Action _onNodeRemoved;
        private readonly Action<WorkflowTab> _onWorkflowRemoved;

        private WorkflowReference _reference;
        private WorkflowReferenceNode _workflowNode;
        private WorkflowTab _createdWorkflowTab;
        private bool _workflowWasCreated;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="subWorkflowId">子流程ID</param>
        /// <param name="subWorkflowName">子流程名称</param>
        /// <param name="masterWorkflow">主流程数据</param>
        /// <param name="nodes">节点集合</param>
        /// <param name="workflowTabs">流程标签页集合</param>
        /// <param name="subWorkflowTabs">子流程标签页集合</param>
        /// <param name="subWorkflows">子流程字典</param>
        /// <param name="position">节点位置</param>
        /// <param name="onWorkflowCreated">子流程创建后的回调（如果子流程不存在）</param>
        /// <param name="onNodeAdded">节点添加后的回调</param>
        /// <param name="onNodeRemoved">节点移除后的回调</param>
        /// <param name="onWorkflowRemoved">子流程移除后的回调（如果子流程是创建的）</param>
        public AddSubWorkflowNodeCommand(
            string subWorkflowId,
            string subWorkflowName,
            MasterWorkflow masterWorkflow,
            ObservableCollection<Node> nodes,
            ObservableCollection<WorkflowTab> workflowTabs,
            ObservableCollection<WorkflowTab> subWorkflowTabs,
            Dictionary<string, WorkFlowNode> subWorkflows,
            Point2D position,
            Action<WorkflowTab> onWorkflowCreated = null,
            Action onNodeAdded = null,
            Action onNodeRemoved = null,
            Action<WorkflowTab> onWorkflowRemoved = null)
            : base($"添加子流程节点 '{subWorkflowName}'")
        {
            _subWorkflowId = subWorkflowId ?? throw new ArgumentNullException(nameof(subWorkflowId));
            _subWorkflowName = subWorkflowName ?? "未命名流程";
            _masterWorkflow = masterWorkflow ?? throw new ArgumentNullException(nameof(masterWorkflow));
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _workflowTabs = workflowTabs ?? throw new ArgumentNullException(nameof(workflowTabs));
            _subWorkflowTabs = subWorkflowTabs ?? throw new ArgumentNullException(nameof(subWorkflowTabs));
            _subWorkflows = subWorkflows ?? throw new ArgumentNullException(nameof(subWorkflows));
            _position = position;
            _onWorkflowCreated = onWorkflowCreated;
            _onNodeAdded = onNodeAdded;
            _onNodeRemoved = onNodeRemoved;
            _onWorkflowRemoved = onWorkflowRemoved;
        }

        /// <summary>
        /// 是否可以执行
        /// </summary>
        public override bool CanExecute(object? parameter)
        {
            return !string.IsNullOrEmpty(_subWorkflowId) &&
                   _masterWorkflow != null &&
                   _nodes != null &&
                   _workflowTabs != null &&
                   _subWorkflowTabs != null &&
                   _subWorkflows != null &&
                   _masterWorkflow.GetSubWorkflowReference(_subWorkflowId) == null; // 检查是否已存在
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public override bool CanUndo => _masterWorkflow != null && _nodes != null && _workflowTabs != null && _subWorkflowTabs != null && _subWorkflows != null;

        /// <summary>
        /// 执行命令 - 添加子流程节点到主流程（如果子流程不存在，先创建）
        /// </summary>
        public override void Execute()
        {
            if (!CanExecute(null))
                throw new InvalidOperationException("无法执行添加子流程节点命令：节点已存在或参数无效");

            // 检查子流程是否存在，如果不存在则创建
            if (!_subWorkflows.ContainsKey(_subWorkflowId))
            {
                // 创建新的子流程
                var subWorkflow = new WorkFlowNode
                {
                    Id = _subWorkflowId,
                    Name = _subWorkflowName,
                    Description = $"子流程: {_subWorkflowName}"
                };

                // 创建流程标签页
                _createdWorkflowTab = new WorkflowTab
                {
                    Name = _subWorkflowName,
                    Type = WorkflowType.Sub,
                    WorkflowData = subWorkflow,
                    Nodes = new ObservableCollection<Node>(subWorkflow.Nodes ?? new List<Node>()),
                    Edges = new ObservableCollection<Edge>()
                };

                // 添加到子流程字典
                _subWorkflows[_subWorkflowId] = subWorkflow;

                // 添加到流程标签页集合
                if (!_workflowTabs.Contains(_createdWorkflowTab))
                {
                    _workflowTabs.Add(_createdWorkflowTab);
                }

                // 添加到子流程标签页集合
                if (!_subWorkflowTabs.Contains(_createdWorkflowTab))
                {
                    _subWorkflowTabs.Add(_createdWorkflowTab);
                }

                _workflowWasCreated = true;

                // 执行创建回调
                _onWorkflowCreated?.Invoke(_createdWorkflowTab);
            }

            // 创建流程引用
            _reference = new WorkflowReference
            {
                SubWorkflowId = _subWorkflowId,
                DisplayName = _subWorkflowName,
                Position = _position
            };

            // 创建流程引用节点
            _workflowNode = new WorkflowReferenceNode
            {
                SubWorkflowId = _subWorkflowId,
                SubWorkflowName = _subWorkflowName,
                Name = _subWorkflowName,
                Position = _position,
                Size = _reference.Size
            };

            // 添加到主流程引用
            _masterWorkflow.AddSubWorkflowReference(_reference);

            // 添加到节点集合
            if (!_nodes.Contains(_workflowNode))
            {
                _nodes.Add(_workflowNode);
            }

            // 执行回调
            _onNodeAdded?.Invoke();
        }

        /// <summary>
        /// 撤销命令 - 从主流程中移除子流程节点（如果子流程是创建的，也一并移除）
        /// </summary>
        public override void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException("无法撤销添加子流程节点命令：参数无效");

            // 从节点集合中移除
            if (_workflowNode != null && _nodes.Contains(_workflowNode))
            {
                _nodes.Remove(_workflowNode);
            }

            // 从主流程引用中移除
            _masterWorkflow.RemoveSubWorkflowReference(_subWorkflowId);

            // 如果子流程是创建的，也需要移除
            if (_workflowWasCreated && _createdWorkflowTab != null)
            {
                // 从子流程字典中移除
                if (_subWorkflows.ContainsKey(_subWorkflowId))
                {
                    _subWorkflows.Remove(_subWorkflowId);
                }

                // 从子流程标签页集合中移除
                if (_subWorkflowTabs.Contains(_createdWorkflowTab))
                {
                    _subWorkflowTabs.Remove(_createdWorkflowTab);
                }

                // 从流程标签页集合中移除
                if (_workflowTabs.Contains(_createdWorkflowTab))
                {
                    _workflowTabs.Remove(_createdWorkflowTab);
                }

                // 执行移除回调（传递被删除的标签页，用于切换）
                _onWorkflowRemoved?.Invoke(_createdWorkflowTab);
            }

            // 执行回调
            _onNodeRemoved?.Invoke();
        }
    }
}

