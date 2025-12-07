using System;
using System.Windows.Input;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 可撤销命令抽象基类
    /// 符合模板方法模式（Template Method Pattern）和开闭原则
    /// 
    /// 设计原则：
    /// 1. 模板方法模式：定义命令执行的骨架，子类实现具体步骤
    /// 2. 开闭原则：通过继承扩展新命令，无需修改基类
    /// 3. DRY 原则：减少子类中的重复代码
    /// </summary>
    public abstract class UndoableCommandBase : IUndoableCommand
    {
        private readonly string _description;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="description">命令描述</param>
        protected UndoableCommandBase(string description)
        {
            _description = description ?? throw new ArgumentNullException(nameof(description));
        }

        /// <summary>
        /// 命令描述
        /// </summary>
        public virtual string Description => _description;

        /// <summary>
        /// 是否可以撤销（默认返回 true，子类可以重写）
        /// </summary>
        public virtual bool CanUndo => true;

        /// <summary>
        /// 是否可以执行（由子类实现）
        /// </summary>
        public abstract bool CanExecute(object? parameter);

        /// <summary>
        /// 执行命令（由子类实现具体逻辑）
        /// </summary>
        public abstract void Execute();

        /// <summary>
        /// 撤销命令（由子类实现具体逻辑）
        /// </summary>
        public abstract void Undo();

        /// <summary>
        /// 是否可以与另一个命令合并（默认返回 false，子类可以重写）
        /// </summary>
        /// <param name="other">另一个命令</param>
        /// <returns>是否可以合并</returns>
        public virtual bool CanMerge(IUndoableCommand other)
        {
            return false;
        }

        /// <summary>
        /// 合并命令（默认返回 null，子类可以重写）
        /// </summary>
        /// <param name="other">要合并的命令</param>
        /// <returns>合并后的命令，如果无法合并则返回 null</returns>
        public virtual IUndoableCommand? Merge(IUndoableCommand other)
        {
            return null;
        }

        /// <summary>
        /// ICommand.Execute 实现
        /// </summary>
        void ICommand.Execute(object? parameter)
        {
            Execute();
        }

        /// <summary>
        /// ICommand.CanExecuteChanged 事件（默认实现）
        /// </summary>
        public virtual event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        /// <summary>
        /// 验证命令参数（辅助方法）
        /// </summary>
        protected static void ValidateParameter(object? parameter, string paramName)
        {
            if (parameter == null)
                throw new ArgumentNullException(paramName);
        }
    }
}

