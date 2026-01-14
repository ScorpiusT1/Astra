using System.Collections;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 添加节点命令（画布版本，使用 IList）
    /// </summary>
    public class AddNodeCommand : UndoableCommandBase
    {
        private readonly IList _nodes;
        private readonly object _node;

        public AddNodeCommand(IList nodes, object node)
            : base($"添加节点")
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public override bool CanExecute(object? parameter) => _nodes != null && _node != null && !_nodes.Contains(_node);

        public override void Execute() => _nodes.Add(_node);

        public override void Undo() => _nodes.Remove(_node);

        public override Core.Nodes.Models.Node GetRelatedNode()
        {
            return _node as Core.Nodes.Models.Node;
        }

        public override System.Collections.IList GetRelatedNodeCollection()
        {
            return _nodes;
        }
    }
}


