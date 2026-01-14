using System.Collections;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 删除连线命令（支持批量）
    /// </summary>
    public class DeleteEdgeCommand : UndoableCommandBase
    {
        private readonly IList _edges;
        private readonly List<object> _deleted;

        public DeleteEdgeCommand(IList edges, IEnumerable<object> edgesToDelete)
            : base($"删除连线")
        {
            _edges = edges ?? throw new ArgumentNullException(nameof(edges));
            _deleted = edgesToDelete != null ? new List<object>(edgesToDelete) : throw new ArgumentNullException(nameof(edgesToDelete));
        }

        public override bool CanExecute(object? parameter) => _edges != null && _deleted != null && _deleted.Count > 0;

        public override void Execute()
        {
            foreach (var e in _deleted)
            {
                _edges.Remove(e);
            }
        }

        public override void Undo()
        {
            foreach (var e in _deleted)
            {
                _edges.Add(e);
            }
        }
    }
}


