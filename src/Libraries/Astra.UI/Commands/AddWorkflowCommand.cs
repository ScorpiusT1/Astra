using Astra.UI.Models;
using Astra.Core.Nodes.Models;
using System;
using System.Collections.ObjectModel;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 添加流程命令 - 支持撤销/重做
    /// 
    /// 设计原则：
    /// 1. 单一职责原则：专门负责流程的添加
    /// 2. 开闭原则：通过继承基类扩展，无需修改基类
    /// </summary>
    public class AddWorkflowCommand : UndoableCommandBase
    {
        private readonly WorkflowTab _workflowTab;
        private readonly ObservableCollection<WorkflowTab> _workflowTabs;
        private readonly ObservableCollection<WorkflowTab> _subWorkflowTabs;
        private readonly Dictionary<string, WorkFlowNode> _subWorkflows;
        private readonly Action<WorkflowTab> _onWorkflowAdded;
        private readonly Action<WorkflowTab> _onWorkflowRemoved;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="workflowTab">要添加的流程标签页</param>
        /// <param name="workflowTabs">流程标签页集合</param>
        /// <param name="subWorkflowTabs">子流程标签页集合</param>
        /// <param name="subWorkflows">子流程字典</param>
        /// <param name="onWorkflowAdded">流程添加后的回调（用于设置当前标签页等）</param>
        /// <param name="onWorkflowRemoved">流程移除后的回调（用于刷新主流程节点等）</param>
        public AddWorkflowCommand(
            WorkflowTab workflowTab,
            ObservableCollection<WorkflowTab> workflowTabs,
            ObservableCollection<WorkflowTab> subWorkflowTabs,
            Dictionary<string, WorkFlowNode> subWorkflows,
            Action<WorkflowTab> onWorkflowAdded = null,
            Action<WorkflowTab> onWorkflowRemoved = null)
            : base($"添加流程 '{workflowTab?.Name ?? "未知"}'")
        {
            _workflowTab = workflowTab ?? throw new ArgumentNullException(nameof(workflowTab));
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
            return _workflowTab != null &&
                   _workflowTabs != null &&
                   _subWorkflowTabs != null &&
                   _subWorkflows != null &&
                   !_workflowTabs.Contains(_workflowTab);
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public override bool CanUndo => _workflowTabs != null && _subWorkflowTabs != null && _subWorkflows != null;

        /// <summary>
        /// 执行命令 - 添加流程到集合
        /// </summary>
        public override void Execute()
        {
            if (!CanExecute(null))
                throw new InvalidOperationException("无法执行添加流程命令：流程已存在于集合中");

            // 获取子流程数据
            var subWorkflow = _workflowTab.GetSubWorkflow();
            if (subWorkflow != null && !string.IsNullOrEmpty(subWorkflow.Id))
            {
                // 添加到子流程字典
                if (!_subWorkflows.ContainsKey(subWorkflow.Id))
                {
                    _subWorkflows[subWorkflow.Id] = subWorkflow;
                }
            }

            // 添加到流程标签页集合
            if (!_workflowTabs.Contains(_workflowTab))
            {
                _workflowTabs.Add(_workflowTab);
            }

            // 如果是子流程，添加到子流程标签页集合
            if (_workflowTab.Type == WorkflowType.Sub && !_subWorkflowTabs.Contains(_workflowTab))
            {
                _subWorkflowTabs.Add(_workflowTab);
            }

            // 执行回调
            _onWorkflowAdded?.Invoke(_workflowTab);
        }

        /// <summary>
        /// 撤销命令 - 从集合中移除流程
        /// </summary>
        public override void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException("无法撤销添加流程命令：集合无效");

            // 获取子流程数据
            var subWorkflow = _workflowTab.GetSubWorkflow();
            if (subWorkflow != null && !string.IsNullOrEmpty(subWorkflow.Id))
            {
                // 从子流程字典中移除
                if (_subWorkflows.ContainsKey(subWorkflow.Id))
                {
                    _subWorkflows.Remove(subWorkflow.Id);
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

            // 执行回调（传递被删除的标签页，用于切换）
            _onWorkflowRemoved?.Invoke(_workflowTab);
        }
    }
}

