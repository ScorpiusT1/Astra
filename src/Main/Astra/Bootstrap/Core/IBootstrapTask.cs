using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Bootstrap.Core
{
    /// <summary>
    /// 启动任务接口
    /// </summary>
    public interface IBootstrapTask
    {
        /// <summary>
        /// 任务名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 任务描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 任务权重（用于计算总进度，默认为 1）
        /// </summary>
        double Weight { get; }

        /// <summary>
        /// 任务优先级（数字越小优先级越高）
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 是否为关键任务（失败时是否中止启动）
        /// </summary>
        bool IsCritical { get; }

        /// <summary>
        /// 执行任务
        /// </summary>
        /// <param name="context">启动上下文</param>
        /// <param name="progress">进度报告</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task ExecuteAsync(
            BootstrapContext context,
            IProgress<BootstrapProgress> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// 任务失败时的回滚操作（可选）
        /// </summary>
        Task RollbackAsync(BootstrapContext context);
    }

    /// <summary>
    /// 启动进度信息
    /// </summary>
    public class BootstrapProgress
    {
        /// <summary>
        /// 当前进度（0-100）
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 详细信息（可选）
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// 是否为不确定进度
        /// </summary>
        public bool IsIndeterminate { get; set; }
    }
}
