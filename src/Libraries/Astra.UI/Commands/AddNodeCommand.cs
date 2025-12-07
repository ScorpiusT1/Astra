using Astra.Core.Nodes.Models;
using System;
using System.Collections.ObjectModel;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 添加节点命令 - 支持撤销/重做
    /// 
    /// 设计原则：
    /// 1. 单一职责原则：专门负责节点的添加
    /// 2. 开闭原则：通过继承基类扩展，无需修改基类
    /// </summary>
    public class AddNodeCommand : UndoableCommandBase
    {
        private readonly Node _node;
        private readonly ObservableCollection<Node> _nodeCollection;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="node">要添加的节点</param>
        /// <param name="nodeCollection">节点集合</param>
        public AddNodeCommand(Node node, ObservableCollection<Node> nodeCollection)
            : base($"添加节点 '{node?.Name ?? "未知"}'")
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _nodeCollection = nodeCollection ?? throw new ArgumentNullException(nameof(nodeCollection));
        }

        /// <summary>
        /// 是否可以执行
        /// </summary>
        public override bool CanExecute(object? parameter)
        {
            return _node != null && 
                   _nodeCollection != null && 
                   !_nodeCollection.Contains(_node);
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public override bool CanUndo => _nodeCollection != null;

        /// <summary>
        /// 执行命令 - 添加节点到集合
        /// </summary>
        public override void Execute()
        {
            if (!CanExecute(null))
                throw new InvalidOperationException("无法执行添加命令：节点已存在于集合中");

            if (!_nodeCollection.Contains(_node))
            {
                _nodeCollection.Add(_node);
            }
        }

        /// <summary>
        /// 撤销命令 - 从集合中移除节点
        /// </summary>
        public override void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException("无法撤销添加命令：集合无效");

            if (_nodeCollection.Contains(_node))
            {
                _nodeCollection.Remove(_node);
            }
        }
    }
}

