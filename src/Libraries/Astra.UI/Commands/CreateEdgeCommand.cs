using System.Collections;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 创建连线命令
    /// </summary>
    public class CreateEdgeCommand : UndoableCommandBase
    {
        private readonly IList _edges;
        private readonly object _edge;

        public CreateEdgeCommand(IList edges, object edge)
            : base($"创建连线")
        {
            _edges = edges ?? throw new ArgumentNullException(nameof(edges));
            _edge = edge ?? throw new ArgumentNullException(nameof(edge));
        }

        public override bool CanExecute(object? parameter) => _edges != null && _edge != null;

        public override void Execute() => _edges.Add(_edge);

        public override void Undo() => _edges.Remove(_edge);

        public override System.Collections.IList GetRelatedEdgeCollection()
        {
            return _edges;
        }
    }
}


