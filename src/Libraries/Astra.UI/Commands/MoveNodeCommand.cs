using Astra.Core.Nodes.Geometry;
using Astra.Core.Nodes.Models;
using System;
using System.Collections.ObjectModel;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 移动节点命令 - 支持撤销/重做和命令合并
    /// 
    /// 设计原则：
    /// 1. 单一职责原则：专门负责节点位置变更
    /// 2. 开闭原则：通过继承基类扩展，无需修改基类
    /// 3. 命令合并：连续的移动操作可以合并，减少历史记录数量
    /// </summary>
    public class MoveNodeCommand : UndoableCommandBase
    {
        private readonly Node _node;
        private readonly ObservableCollection<Node> _nodeCollection;
        private Point2D _oldPosition;
        private Point2D _newPosition;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="node">要移动的节点</param>
        /// <param name="nodeCollection">节点集合</param>
        /// <param name="oldPosition">旧位置</param>
        /// <param name="newPosition">新位置</param>
        public MoveNodeCommand(Node node, ObservableCollection<Node> nodeCollection, Point2D oldPosition, Point2D newPosition)
            : base($"移动节点 '{node?.Name ?? "未知"}'")
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _nodeCollection = nodeCollection ?? throw new ArgumentNullException(nameof(nodeCollection));
            _oldPosition = oldPosition;
            _newPosition = newPosition;
        }

        /// <summary>
        /// 是否可以执行
        /// </summary>
        public override bool CanExecute(object? parameter)
        {
            return _node != null && 
                   _nodeCollection != null && 
                   _nodeCollection.Contains(_node);
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public override bool CanUndo => CanExecute(null);

        /// <summary>
        /// 执行命令 - 移动节点到新位置
        /// </summary>
        public override void Execute()
        {
            if (!CanExecute(null))
                throw new InvalidOperationException("无法执行移动命令：节点不在集合中");

            _node.Position = _newPosition;
        }

        /// <summary>
        /// 撤销命令 - 恢复节点到旧位置
        /// </summary>
        public override void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException("无法撤销移动命令：节点状态无效");

            _node.Position = _oldPosition;
        }

        /// <summary>
        /// 检查是否可以与另一个命令合并
        /// 连续移动同一节点可以合并
        /// </summary>
        public override bool CanMerge(IUndoableCommand other)
        {
            if (other is not MoveNodeCommand otherMove)
                return false;

            // 只有移动同一个节点时才能合并
            return ReferenceEquals(_node, otherMove._node) &&
                   ReferenceEquals(_nodeCollection, otherMove._nodeCollection);
        }

        /// <summary>
        /// 合并命令
        /// 合并连续的移动操作，保留初始位置和最终位置
        /// </summary>
        public override IUndoableCommand? Merge(IUndoableCommand other)
        {
            if (!CanMerge(other))
                return null;

            var otherMove = (MoveNodeCommand)other;
            
            // 合并逻辑：保留原始旧位置，使用新命令的新位置
            // 这样可以将多个连续的移动操作合并为一个命令
            return new MoveNodeCommand(
                _node,
                _nodeCollection,
                _oldPosition,  // 保留最初的起始位置
                otherMove._newPosition  // 使用最新的目标位置
            );
        }
    }
}

