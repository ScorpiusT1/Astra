using System;
using System.Collections.Generic;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 可撤销命令接口
    /// </summary>
    public interface IUndoableCommand
    {
        void Execute();
        void Undo();
    }

    /// <summary>
    /// 撤销/重做管理器（双栈）
    /// </summary>
    public class UndoRedoManager
    {
        private readonly Stack<IUndoableCommand> _undoStack = new();
        private readonly Stack<IUndoableCommand> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 批量操作开始回调（用于通知 InfiniteCanvas 开始批量更新）
        /// </summary>
        public Action OnBatchOperationBegin { get; set; }

        /// <summary>
        /// 批量操作结束回调（用于通知 InfiniteCanvas 结束批量更新）
        /// </summary>
        public Action OnBatchOperationEnd { get; set; }

        public void Do(IUndoableCommand command)
        {
            if (command == null) return;
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
        }

        public void Undo()
        {
            if (!CanUndo) return;
            
            var cmd = _undoStack.Pop();
            
            // 🔧 如果是批量操作命令，使用批量模式
            bool isBatchCommand = IsBatchCommand(cmd);
            
            if (isBatchCommand)
            {
                System.Diagnostics.Debug.WriteLine($"[批量撤销] 开始");
                OnBatchOperationBegin?.Invoke();
            }
            
            try
            {
                cmd.Undo();
            }
            finally
            {
                if (isBatchCommand)
                {
                    OnBatchOperationEnd?.Invoke();
                    System.Diagnostics.Debug.WriteLine($"[批量撤销] 完成");
                }
            }
            
            _redoStack.Push(cmd);
        }

        public void Redo()
        {
            if (!CanRedo) return;
            
            var cmd = _redoStack.Pop();
            
            // 🔧 如果是批量操作命令，使用批量模式
            bool isBatchCommand = IsBatchCommand(cmd);
            
            if (isBatchCommand)
            {
                System.Diagnostics.Debug.WriteLine($"[批量重做] 开始");
                OnBatchOperationBegin?.Invoke();
            }
            
            try
            {
                cmd.Execute();
            }
            finally
            {
                if (isBatchCommand)
                {
                    OnBatchOperationEnd?.Invoke();
                    System.Diagnostics.Debug.WriteLine($"[批量重做] 完成");
                }
            }
            
            _undoStack.Push(cmd);
        }

        /// <summary>
        /// 判断是否是批量操作命令（涉及多个节点或连线）
        /// </summary>
        private bool IsBatchCommand(IUndoableCommand cmd)
        {
            return cmd is DeleteNodeCommand ||
                   cmd is PasteNodesWithEdgesCommand ||
                   cmd is PasteNodesCommand ||
                   cmd is DeleteEdgeCommand ||
                   cmd is CompositeCommand ||
                   cmd is ToggleNodeEnabledCommand;
        }
    }

    /// <summary>
    /// 创建连线命令
    /// </summary>
    public class CreateEdgeCommand : IUndoableCommand
    {
        private readonly System.Collections.IList _edges;
        private readonly object _edge;

        public CreateEdgeCommand(System.Collections.IList edges, object edge)
        {
            _edges = edges ?? throw new ArgumentNullException(nameof(edges));
            _edge = edge ?? throw new ArgumentNullException(nameof(edge));
        }

        public void Execute() => _edges.Add(_edge);
        public void Undo() => _edges.Remove(_edge);
    }

    /// <summary>
    /// 删除连线命令（支持批量）
    /// </summary>
    public class DeleteEdgeCommand : IUndoableCommand
    {
        private readonly System.Collections.IList _edges;
        private readonly List<object> _deleted;

        public DeleteEdgeCommand(System.Collections.IList edges, IEnumerable<object> edgesToDelete)
        {
            _edges = edges ?? throw new ArgumentNullException(nameof(edges));
            _deleted = edgesToDelete != null ? new List<object>(edgesToDelete) : throw new ArgumentNullException(nameof(edgesToDelete));
        }

        public void Execute()
        {
            foreach (var e in _deleted)
            {
                _edges.Remove(e);
            }
        }

        public void Undo()
        {
            foreach (var e in _deleted)
            {
                _edges.Add(e);
            }
        }
    }

    /// <summary>
    /// 添加节点命令
    /// </summary>
    public class AddNodeCommand : IUndoableCommand
    {
        private readonly System.Collections.IList _nodes;
        private readonly object _node;

        public AddNodeCommand(System.Collections.IList nodes, object node)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public void Execute() => _nodes.Add(_node);
        public void Undo() => _nodes.Remove(_node);
    }

    /// <summary>
    /// 删除节点命令（同时删除相关连线）
    /// </summary>
    public class DeleteNodeCommand : IUndoableCommand
    {
        private readonly System.Collections.IList _nodes;
        private readonly System.Collections.IList _edges;
        private readonly List<object> _deletedNodes;
        private readonly List<(object edge, int index)> _deletedEdges; // 记录边和索引，用于恢复

        public DeleteNodeCommand(
            System.Collections.IList nodes, 
            System.Collections.IList edges,
            IEnumerable<object> nodesToDelete)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _edges = edges; // 允许为null
            _deletedNodes = nodesToDelete != null ? new List<object>(nodesToDelete) : throw new ArgumentNullException(nameof(nodesToDelete));
            _deletedEdges = new List<(object, int)>();
        }

        public void Execute()
        {
            // 先删除相关连线（并记录索引）
            if (_edges != null && _deletedNodes.Count > 0)
            {
                var nodeIds = new HashSet<string>();
                foreach (var nodeObj in _deletedNodes)
                {
                    if (nodeObj is Astra.Core.Nodes.Models.Node node)
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
                        if (edgeObj is Astra.Core.Nodes.Models.Edge edge)
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

        public void Undo()
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
    }

    /// <summary>
    /// 组合命令（按顺序执行多个命令）
    /// </summary>
    public class CompositeCommand : IUndoableCommand
    {
        private readonly List<IUndoableCommand> _commands;

        public CompositeCommand(IEnumerable<IUndoableCommand> commands)
        {
            _commands = commands != null ? new List<IUndoableCommand>(commands) : new List<IUndoableCommand>();
        }

        public void Execute()
        {
            foreach (var cmd in _commands)
            {
                cmd.Execute();
            }
        }

        public void Undo()
        {
            // 反向撤销
            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }
    }

    /// <summary>
    /// 启用/禁用节点命令
    /// </summary>
    public class ToggleNodeEnabledCommand : IUndoableCommand
    {
        private readonly List<Astra.Core.Nodes.Models.Node> _nodes;
        private readonly Dictionary<string, bool> _originalStates;
        private readonly bool _newState;

        public ToggleNodeEnabledCommand(IEnumerable<Astra.Core.Nodes.Models.Node> nodes, bool newState)
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

        public void Execute()
        {
            foreach (var node in _nodes)
            {
                node.IsEnabled = _newState;
            }
        }

        public void Undo()
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

    /// <summary>
    /// 复制节点命令（粘贴）
    /// </summary>
    public class PasteNodesCommand : IUndoableCommand
    {
        private readonly System.Collections.IList _nodes;
        private readonly List<Astra.Core.Nodes.Models.Node> _copiedNodes;

        public PasteNodesCommand(System.Collections.IList nodes, IEnumerable<Astra.Core.Nodes.Models.Node> nodesToPaste)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _copiedNodes = nodesToPaste?.ToList() ?? throw new ArgumentNullException(nameof(nodesToPaste));
        }

        public void Execute()
        {
            foreach (var node in _copiedNodes)
            {
                _nodes.Add(node);
            }
        }

        public void Undo()
        {
            foreach (var node in _copiedNodes)
            {
                _nodes.Remove(node);
            }
        }
    }

    /// <summary>
    /// 复制节点和连线命令（粘贴节点及其连接关系）
    /// </summary>
    public class PasteNodesWithEdgesCommand : IUndoableCommand
    {
        private readonly System.Collections.IList _nodes;
        private readonly System.Collections.IList _edges;
        private readonly List<Astra.Core.Nodes.Models.Node> _copiedNodes;
        private readonly List<Astra.Core.Nodes.Models.Edge> _copiedEdges;

        public PasteNodesWithEdgesCommand(
            System.Collections.IList nodes, 
            System.Collections.IList edges,
            IEnumerable<Astra.Core.Nodes.Models.Node> nodesToPaste,
            IEnumerable<Astra.Core.Nodes.Models.Edge> edgesToPaste)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _edges = edges ?? throw new ArgumentNullException(nameof(edges));
            _copiedNodes = nodesToPaste?.ToList() ?? throw new ArgumentNullException(nameof(nodesToPaste));
            _copiedEdges = edgesToPaste?.ToList() ?? new List<Astra.Core.Nodes.Models.Edge>();
        }

        public void Execute()
        {
            // 先添加节点
            foreach (var node in _copiedNodes)
            {
                _nodes.Add(node);
            }

            // 再添加连线
            foreach (var edge in _copiedEdges)
            {
                _edges.Add(edge);
            }
        }

        public void Undo()
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

