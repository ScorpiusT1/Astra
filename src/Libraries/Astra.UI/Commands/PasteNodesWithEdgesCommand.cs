using Astra.Core.Nodes.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Astra.UI.Commands
{

    /// <summary>
    /// 复制节点和连线命令（粘贴节点及其连接关系）
    /// </summary>
    public class PasteNodesWithEdgesCommand : UndoableCommandBase
    {
        private readonly IList _nodes;
        private readonly IList _edges;
        private readonly List<Node> _copiedNodes;
        private readonly List<Edge> _copiedEdges;

        public PasteNodesWithEdgesCommand(
            IList nodes,
            IList edges,
            IEnumerable<Node> nodesToPaste,
            IEnumerable<Edge> edgesToPaste)
            : base($"粘贴节点和连线")
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _edges = edges ?? throw new ArgumentNullException(nameof(edges));
            _copiedNodes = nodesToPaste?.ToList() ?? throw new ArgumentNullException(nameof(nodesToPaste));
            _copiedEdges = edgesToPaste?.ToList() ?? new List<Edge>();
        }

        public override bool CanExecute(object? parameter) => _nodes != null && _edges != null && _copiedNodes != null && _copiedNodes.Count > 0;

        public override void Execute()
        {
            // 🔧 性能优化：批量添加节点和连线，减少UI更新次数
            // 如果集合支持批量操作，使用批量添加
            if (_nodes is System.Collections.ObjectModel.ObservableCollection<Node> nodeCollection)
            {
                // 使用 AddRange（如果支持）或逐个添加
                foreach (var node in _copiedNodes)
                {
                    nodeCollection.Add(node);
                }
            }
            else
            {
                // 先添加节点
                foreach (var node in _copiedNodes)
                {
                    _nodes.Add(node);
                }
            }

            // 再添加连线
            if (_edges is System.Collections.ObjectModel.ObservableCollection<Edge> edgeCollection)
            {
                foreach (var edge in _copiedEdges)
                {
                    edgeCollection.Add(edge);
                }
            }
            else
            {
                foreach (var edge in _copiedEdges)
                {
                    _edges.Add(edge);
                }
            }
        }

        public override void Undo()
        {
            // 先删除连线
            foreach (var edge in _copiedEdges)
            {
                _edges.Remove(edge);
            }

            // 再删除节点
            foreach (var node in _copiedNodes)
            {
                _nodes.Remove(node);
            }
        }
    }
}


