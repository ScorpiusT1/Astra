using Astra.Core.Nodes.Models;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 启用/禁用节点命令
    /// </summary>
    public class ToggleNodeEnabledCommand : UndoableCommandBase
    {
        private readonly List<Node> _nodes;
        private readonly Dictionary<string, bool> _originalStates;
        private readonly bool _newState;

        public ToggleNodeEnabledCommand(IEnumerable<Node> nodes, bool newState)
            : base($"{(newState ? "启用" : "禁用")}节点")
        {
            _nodes = nodes?.ToList() ?? throw new ArgumentNullException(nameof(nodes));
            _newState = newState;
            _originalStates = new Dictionary<string, bool>();

            // 记录原始状态
            foreach (var node in _nodes)
            {
                _originalStates[node.Id] = node.IsEnabled;
            }
        }

        public override bool CanExecute(object? parameter) => _nodes != null && _nodes.Count > 0;

        public override void Execute()
        {
            foreach (var node in _nodes)
            {
                node.IsEnabled = _newState;
            }
        }

        public override void Undo()
        {
            foreach (var node in _nodes)
            {
                if (_originalStates.TryGetValue(node.Id, out var originalState))
                {
                    node.IsEnabled = originalState;
                }
            }
        }
    }
}


