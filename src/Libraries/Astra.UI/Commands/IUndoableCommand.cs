using System.Windows.Input;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 可撤销命令接口
    /// 符合命令模式（Command Pattern）和单一职责原则
    /// 
    /// 设计原则：
    /// 1. 单一职责原则：每个命令只负责一个操作
    /// 2. 开闭原则：通过接口扩展新命令类型，无需修改现有代码
    /// 3. 接口隔离原则：接口只包含必要的成员
    /// </summary>
    public interface IUndoableCommand : ICommand
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        void Execute();

        /// <summary>
        /// 撤销命令
        /// </summary>
        void Undo();

        /// <summary>
        /// 命令描述（用于撤销/重做菜单显示）
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 是否可以被撤销
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// 是否可以与另一个命令合并（用于优化历史记录）
        /// 例如：连续的移动操作可以合并为一个命令
        /// </summary>
        /// <param name="other">另一个命令</param>
        /// <returns>是否可以合并</returns>
        bool CanMerge(IUndoableCommand other);

        /// <summary>
        /// 合并命令（如果可能）
        /// 合并后的命令应该包含两个命令的最终状态
        /// </summary>
        /// <param name="other">要合并的命令</param>
        /// <returns>合并后的命令，如果无法合并则返回 null</returns>
        IUndoableCommand? Merge(IUndoableCommand other);
    }
}

