using Astra.Core.Nodes.Models;
using System.Collections;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 复制节点命令（粘贴）
    /// </summary>
    public class PasteNodesCommand : UndoableCommandBase
    {
        private readonly IList _nodes;
        private readonly List<Node> _copiedNodes;

        public PasteNodesCommand(IList nodes, IEnumerable<Node> nodesToPaste)
            : base($"粘贴节点")
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _copiedNodes = nodesToPaste?.ToList() ?? throw new ArgumentNullException(nameof(nodesToPaste));
        }

        public override bool CanExecute(object? parameter) => _nodes != null && _copiedNodes != null && _copiedNodes.Count > 0;

        public override void Execute()
        {
            foreach (var node in _copiedNodes)
            {
                _nodes.Add(node);
            }
        }

        public override void Undo()
        {
            foreach (var node in _copiedNodes)
            {
                _nodes.Remove(node);
            }
        }
    }
}


