using Astra.Bootstrap.Core;

namespace Astra.Bootstrap.Services
{
    #region Bootstrap Result
    /// <summary>
    /// 启动结果
    /// </summary>
    public class BootstrapResult
    {
        private readonly List<IBootstrapTask> _completedTasks = new List<IBootstrapTask>();
        private readonly List<(IBootstrapTask Task, Exception Error)> _failedTasks =
            new List<(IBootstrapTask Task, Exception Error)>();

        public bool IsSuccess { get; set; }
        public bool IsCancelled { get; set; }
        public TimeSpan TotalTime { get; set; }
        public Exception FatalException { get; set; }

        /// <summary>
        /// 已完成的任务（只读）
        /// </summary>
        public IReadOnlyList<IBootstrapTask> CompletedTasks => _completedTasks;

        /// <summary>
        /// 失败的任务（只读）
        /// </summary>
        public IReadOnlyList<(IBootstrapTask Task, Exception Error)> FailedTasks => _failedTasks;

        /// <summary>
        /// 添加已完成的任务
        /// </summary>
        internal void AddCompletedTask(IBootstrapTask task)
        {
            if (task != null)
            {
                _completedTasks.Add(task);
            }
        }

        /// <summary>
        /// 添加失败的任务
        /// </summary>
        internal void AddFailedTask(IBootstrapTask task, Exception error)
        {
            if (task != null)
            {
                _failedTasks.Add((task, error));
            }
        }

        /// <summary>
        /// 批量添加已完成的任务
        /// </summary>
        internal void AddCompletedTasks(IEnumerable<IBootstrapTask> tasks)
        {
            if (tasks != null)
            {
                _completedTasks.AddRange(tasks);
            }
        }

        /// <summary>
        /// 批量添加失败的任务
        /// </summary>
        internal void AddFailedTasks(IEnumerable<(IBootstrapTask Task, Exception Error)> tasks)
        {
            if (tasks != null)
            {
                _failedTasks.AddRange(tasks);
            }
        }

        /// <summary>
        /// 获取失败任务的摘要
        /// </summary>
        public string GetFailureSummary()
        {
            if (FailedTasks.Count == 0)
                return "无失败任务";

            return string.Join("\n", FailedTasks.Select(f =>
                $"- {f.Task.Name}: {f.Error.Message}"));
        }
    }

    #endregion
}

