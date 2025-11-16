using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Bootstrap.Core
{
    /// <summary>
    /// 启动任务基类
    /// </summary>
    public abstract class BootstrapTaskBase : IBootstrapTask
    {
        public abstract string Name { get; }
        public virtual string Description => Name;
        public virtual double Weight => 1.0;
        public virtual int Priority => 100;
        public virtual bool IsCritical => true;

        protected BootstrapTaskBase()
        {
            TaskId = Guid.NewGuid();
        }

        /// <summary>
        /// 任务唯一标识
        /// </summary>
        public Guid TaskId { get; }

        /// <summary>
        /// 执行任务
        /// </summary>
        public async Task ExecuteAsync(
            BootstrapContext context,
            IProgress<BootstrapProgress> progress,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                context.Logger?.LogInfo($"[{Name}] 开始执行");

                // 报告开始进度
                progress?.Report(new BootstrapProgress
                {
                    Percentage = 0,
                    Message = Description
                });

                // 执行实际任务
                await ExecuteCoreAsync(context, progress, cancellationToken);

                // 报告完成进度
                progress?.Report(new BootstrapProgress
                {
                    Percentage = 100,
                    Message = $"{Description} - 完成"
                });

                stopwatch.Stop();
                context.Logger?.LogInfo($"[{Name}] 完成，耗时：{stopwatch.ElapsedMilliseconds}ms");
            }
            catch (OperationCanceledException)
            {
                context.Logger?.LogWarning($"[{Name}] 已取消");
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                context.Logger?.LogError($"[{Name}] 失败：{ex.Message}", ex);
                throw new BootstrapException($"任务 '{Name}' 执行失败", ex, this);
            }
        }

        /// <summary>
        /// 子类实现具体的执行逻辑
        /// </summary>
        protected abstract Task ExecuteCoreAsync(
            BootstrapContext context,
            IProgress<BootstrapProgress> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// 回滚操作（默认为空实现）
        /// </summary>
        public virtual Task RollbackAsync(BootstrapContext context)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 辅助方法：报告进度
        /// </summary>
        protected void ReportProgress(IProgress<BootstrapProgress> progress, double percentage, string message, string details = null)
        {
            progress?.Report(new BootstrapProgress
            {
                Percentage = percentage,
                Message = message,
                Details = details
            });
        }
    }
}
