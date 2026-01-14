using Astra.Core.Nodes.Models;
using Astra.UI.Controls;
using System;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 重命名节点命令 - 支持撤销/重做
    /// </summary>
    public class RenameNodeCommand : UndoableCommandBase
    {
        private readonly Node _node;
        private readonly NodeControl _control;
        private readonly string _oldName;
        private readonly string _newName;

        public RenameNodeCommand(Node node, NodeControl control, string oldName, string newName)
            : base($"重命名节点 '{oldName}' -> '{newName}'")
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _control = control ?? throw new ArgumentNullException(nameof(control));
            _oldName = oldName ?? throw new ArgumentNullException(nameof(oldName));
            _newName = newName ?? throw new ArgumentNullException(nameof(newName));
        }

        public override bool CanExecute(object? parameter) => _node != null && _control != null;

        public override void Execute() => Apply(_newName);

        public override void Undo() => Apply(_oldName);

        public override Core.Nodes.Models.Node GetRelatedNode()
        {
            return _node;
        }

        private void Apply(string value)
        {
            if (_node != null)
            {
                _node.Name = value;
            }
            if (_control != null)
            {
                _control.Title = value;
            }
        }
    }
}

