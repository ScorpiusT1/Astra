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
            cmd.Undo();
            _redoStack.Push(cmd);
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var cmd = _redoStack.Pop();
            cmd.Execute();
            _undoStack.Push(cmd);
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
}

