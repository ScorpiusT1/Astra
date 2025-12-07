using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Astra.UI.Commands
{
    /// <summary>
    /// 命令管理器 - 管理所有可撤销命令的执行、撤销和重做
    /// 
    /// 设计原则：
    /// 1. 单一职责原则：专门负责命令历史管理
    /// 2. 命令模式：封装命令执行和撤销逻辑
    /// 3. 备忘录模式：维护命令历史状态
    /// 4. 开闭原则：支持命令合并等扩展功能
    /// </summary>
    public class CommandManager : ObservableObject
    {
        private readonly Stack<IUndoableCommand> _undoStack = new Stack<IUndoableCommand>();
        private readonly Stack<IUndoableCommand> _redoStack = new Stack<IUndoableCommand>();
        private readonly int _maxHistorySize;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxHistorySize">最大历史记录数量，默认100</param>
        public CommandManager(int maxHistorySize = 100)
        {
            if (maxHistorySize < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHistorySize), "历史记录大小必须大于0");

            _maxHistorySize = maxHistorySize;
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 执行命令并添加到历史记录
        /// 支持命令合并：如果新命令可以与上一个命令合并，则合并它们
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <param name="enableMerge">是否启用命令合并，默认 true</param>
        public void Execute(IUndoableCommand command, bool enableMerge = true)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (!command.CanExecute(null))
            {
                System.Diagnostics.Debug.WriteLine($"命令无法执行: {command.Description}");
                return;
            }

            try
            {
                command.Execute();

                // 尝试与上一个命令合并（如果启用且可能）
                if (enableMerge && _undoStack.Count > 0)
                {
                    var lastCommand = _undoStack.Peek();
                    if (lastCommand.CanMerge(command))
                    {
                        // 合并命令
                        var mergedCommand = lastCommand.Merge(command);
                        if (mergedCommand != null)
                        {
                            // 移除旧命令，添加合并后的命令
                            _undoStack.Pop();
                            _undoStack.Push(mergedCommand);
                            OnPropertyChanged(nameof(CanUndo));
                            OnPropertyChanged(nameof(CanRedo));
                            return;
                        }
                    }
                }

                // 无法合并，添加为新命令
                _undoStack.Push(command);

                // 限制历史记录大小
                LimitHistorySize();

                // 执行新命令后，清空重做栈
                _redoStack.Clear();

                // 通知属性变化
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"执行命令失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 撤销最后一个命令
        /// </summary>
        public void Undo()
        {
            if (!CanUndo)
                return;

            try
            {
                var command = _undoStack.Pop();
                command.Undo();

                // 移动到重做栈
                _redoStack.Push(command);

                // 通知属性变化
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"撤销命令失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 重做最后一个撤销的命令
        /// </summary>
        public void Redo()
        {
            if (!CanRedo)
                return;

            try
            {
                var command = _redoStack.Pop();
                command.Execute();

                // 移回撤销栈
                _undoStack.Push(command);

                // 限制历史记录大小
                LimitHistorySize();

                // 通知属性变化
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"重做命令失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 清空所有历史记录
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        /// <summary>
        /// 获取撤销历史（用于显示）
        /// </summary>
        public ReadOnlyCollection<string> GetUndoHistory()
        {
            return new ReadOnlyCollection<string>(
                _undoStack.Select(cmd => cmd.Description).ToList());
        }

        /// <summary>
        /// 获取重做历史（用于显示）
        /// </summary>
        public ReadOnlyCollection<string> GetRedoHistory()
        {
            return new ReadOnlyCollection<string>(
                _redoStack.Select(cmd => cmd.Description).ToList());
        }

        /// <summary>
        /// 限制历史记录大小
        /// 使用队列方式移除最旧的命令
        /// </summary>
        private void LimitHistorySize()
        {
            if (_undoStack.Count <= _maxHistorySize)
                return;

            // 将栈转换为列表，移除最旧的项，再转换回栈
            var commands = _undoStack.ToList();
            commands.RemoveRange(0, commands.Count - _maxHistorySize);
            
            _undoStack.Clear();
            foreach (var cmd in commands.Reverse<IUndoableCommand>())
            {
                _undoStack.Push(cmd);
            }
        }
    }
}

