using Astra.Core.Nodes.Models;
using System.Collections;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 删除节点命令（同时删除相关连线，画布版本，使用 IList）
    /// </summary>
    public class DeleteNodeCommand : UndoableCommandBase
    {
        private readonly IList _nodes;
        private readonly IList _edges;
        private readonly List<object> _deletedNodes;
        private readonly List<(object edge, int index)> _deletedEdges; // 记录边和索引，用于恢复

        public DeleteNodeCommand(
            IList nodes,
            IList edges,
            IEnumerable<object> nodesToDelete)
            : base($"删除节点")
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _edges = edges; // 允许为null
            _deletedNodes = nodesToDelete != null ? new List<object>(nodesToDelete) : throw new ArgumentNullException(nameof(nodesToDelete));
            _deletedEdges = new List<(object, int)>();
        }

        public override bool CanExecute(object? parameter) => _nodes != null && _deletedNodes != null && _deletedNodes.Count > 0;

        public override void Execute()
        {
            // 先删除相关连线（并记录索引）
            if (_edges != null && _deletedNodes.Count > 0)
            {
                var nodeIds = new HashSet<string>();
                foreach (var nodeObj in _deletedNodes)
                {
                    if (nodeObj is Node node)
                    {
                        nodeIds.Add(node.Id);
                    }
                }

                if (nodeIds.Count > 0)
                {
                    // 从后往前遍历，记录边和其原始索引
                    for (int i = _edges.Count - 1; i >= 0; i--)
                    {
                        var edgeObj = _edges[i];
                        if (edgeObj is Edge edge)
                        {
                            if (nodeIds.Contains(edge.SourceNodeId) || nodeIds.Contains(edge.TargetNodeId))
                            {
                                _deletedEdges.Add((edgeObj, i));
                                _edges.RemoveAt(i);
                            }
                        }
                    }
                }
            }

            // 再删除节点
            foreach (var node in _deletedNodes)
            {
                _nodes.Remove(node);
            }
        }

        public override void Undo()
        {
            // 先恢复节点
            foreach (var node in _deletedNodes)
            {
                _nodes.Add(node);
            }

            // 再恢复连线（按原始索引恢复）
            if (_edges != null && _deletedEdges.Count > 0)
            {
                // 按索引从小到大排序，确保正确恢复顺序
                var sortedEdges = _deletedEdges.OrderBy(x => x.index).ToList();
                foreach (var (edge, index) in sortedEdges)
                {
                    // 如果索引超出当前范围，直接添加到末尾
                    if (index >= _edges.Count)
                    {
                        _edges.Add(edge);
                    }
                    else
                    {
                        _edges.Insert(index, edge);
                    }
                }
            }

            // 清空记录，为下次撤销做准备
            _deletedEdges.Clear();
        }

        public override System.Collections.IList GetRelatedNodeCollection()
        {
            return _nodes;
        }

        public override System.Collections.IList GetRelatedEdgeCollection()
        {
            return _edges;
        }
    }
}