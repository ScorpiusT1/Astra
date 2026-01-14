using Astra.Core.Nodes.Models;
using Astra.UI.Models;
using System;
using System.Linq;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 重命名流程命令 - 支持撤销/重做
    /// </summary>
    public class RenameWorkflowCommand : UndoableCommandBase
    {
        private readonly WorkflowTab _tab;
        private readonly string _oldName;
        private readonly string _newName;
        private readonly System.Collections.ObjectModel.ObservableCollection<WorkflowTab> _workflowTabs;
        private readonly Action<string, string> _updateMasterWorkflowNodeName;
        private readonly Action<WorkflowTab> _markAsModified;

        public RenameWorkflowCommand(
            WorkflowTab tab,
            string oldName,
            string newName,
            System.Collections.ObjectModel.ObservableCollection<WorkflowTab> workflowTabs,
            Action<string, string> updateMasterWorkflowNodeName,
            Action<WorkflowTab> markAsModified)
            : base($"重命名流程 '{oldName}' -> '{newName}'")
        {
            _tab = tab ?? throw new ArgumentNullException(nameof(tab));
            _oldName = oldName ?? throw new ArgumentNullException(nameof(oldName));
            _newName = newName ?? throw new ArgumentNullException(nameof(newName));
            _workflowTabs = workflowTabs ?? throw new ArgumentNullException(nameof(workflowTabs));
            _updateMasterWorkflowNodeName = updateMasterWorkflowNodeName ?? throw new ArgumentNullException(nameof(updateMasterWorkflowNodeName));
            _markAsModified = markAsModified ?? throw new ArgumentNullException(nameof(markAsModified));
        }

        public override bool CanExecute(object? parameter)
        {
            if (_tab == null || string.IsNullOrWhiteSpace(_newName))
                return false;

            // 检查名称是否已存在（排除当前标签页）
            var existingTab = _workflowTabs?.FirstOrDefault(t => t != _tab && t.Name == _newName);
            return existingTab == null;
        }

        public override void Execute()
        {
            if (!CanExecute(null))
                throw new InvalidOperationException($"无法执行重命名命令：流程名称 '{_newName}' 已存在");

            Apply(_newName);
        }

        public override void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException("无法撤销重命名命令：标签页无效");

            Apply(_oldName);
        }

        private void Apply(string name)
        {
            if (_tab == null)
                return;

            // 更新流程名称
            _tab.Name = name;

            // 如果是子流程，同步更新主流程中对应的 WorkflowReferenceNode 的名称
            if (_tab.Type == WorkflowType.Sub)
            {
                var subWorkflow = _tab.GetSubWorkflow();
                if (subWorkflow != null)
                {
                    // 更新主流程中对应的 WorkflowReferenceNode 的名称
                    _updateMasterWorkflowNodeName?.Invoke(subWorkflow.Id, name);
                }
            }

            // 标记为已修改
            _markAsModified?.Invoke(_tab);
        }
    }
}

