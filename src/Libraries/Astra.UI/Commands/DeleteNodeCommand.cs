using Astra.Core.Nodes.Geometry;
using Astra.Core.Nodes.Models;
using System;
using System.Collections.ObjectModel;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 删除节点命令 - 支持撤销/重做
    /// 
    /// 设计原则：
    /// 1. 单一职责原则：专门负责节点的删除
    /// 2. 开闭原则：通过继承基类扩展，无需修改基类
    /// 3. 备忘录模式：保存节点状态以便撤销时恢复
    /// </summary>
    public class DeleteNodeCommand : UndoableCommandBase
    {
        private readonly Node _node;
        private readonly ObservableCollection<Node> _nodeCollection;
        private readonly int _originalIndex;
        private readonly Point2D _originalPosition;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="node">要删除的节点</param>
        /// <param name="nodeCollection">节点集合</param>
        public DeleteNodeCommand(Node node, ObservableCollection<Node> nodeCollection)
            : base($"删除节点 '{node?.Name ?? "未知"}'")
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _nodeCollection = nodeCollection ?? throw new ArgumentNullException(nameof(nodeCollection));
            
            // 保存原始信息以便撤销（备忘录模式）
            _originalIndex = _nodeCollection.IndexOf(_node);
            // Point2D 是值类型，直接使用即可（如果未设置，默认值为 (0, 0)）
            _originalPosition = _node.Position;
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
        public override bool CanUndo => _node != null && _nodeCollection != null;

        /// <summary>
        /// 执行命令 - 从集合中删除节点
        /// </summary>
        public override void Execute()
        {
            if (!CanExecute(null))
                throw new InvalidOperationException("无法执行删除命令：节点不在集合中");

            if (_nodeCollection.Contains(_node))
            {
                _nodeCollection.Remove(_node);
            }
        }

        /// <summary>
        /// 撤销命令 - 恢复节点到集合中的原位置
        /// </summary>
        public override void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException("无法撤销删除命令：节点或集合无效");

            // 恢复节点位置
            _node.Position = _originalPosition;

            // 恢复到原索引位置（如果可能）
            if (_originalIndex >= 0 && _originalIndex < _nodeCollection.Count)
            {
                _nodeCollection.Insert(_originalIndex, _node);
            }
            else if (!_nodeCollection.Contains(_node))
            {
                // 如果原索引无效，添加到末尾
                _nodeCollection.Add(_node);
            }
        }
    }
}

