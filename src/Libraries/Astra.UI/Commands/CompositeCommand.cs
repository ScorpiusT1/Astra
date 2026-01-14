namespace Astra.UI.Commands
{
    /// <summary>
    /// 组合命令（按顺序执行多个命令）
    /// </summary>
    public class CompositeCommand : UndoableCommandBase
    {
        private readonly List<IUndoableCommand> _commands;

        public CompositeCommand(IEnumerable<IUndoableCommand> commands)
            : base($"组合操作")
        {
            _commands = commands != null ? new List<IUndoableCommand>(commands) : new List<IUndoableCommand>();
            
            // 从子命令中获取 WorkflowTab（优先使用第一个有 WorkflowTab 的命令）
            if (_commands.Count > 0)
            {
                foreach (var cmd in _commands)
                {
                    if (cmd is UndoableCommandBase undoableCmd && undoableCmd.WorkflowTab != null)
                    {
                        WorkflowTab = undoableCmd.WorkflowTab;
                        break; // 找到第一个就使用
                    }
                }
            }
        }

        public override bool CanExecute(object? parameter) => _commands != null && _commands.Count > 0;

        public override void Execute()
        {
            foreach (var cmd in _commands)
            {
                cmd.Execute();
            }
        }

        public override void Undo()
        {
            // 反向撤销
            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }
    }
}


