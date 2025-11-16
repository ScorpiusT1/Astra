using Astra.Core.Triggers.Configuration;
using Astra.Core.Triggers.Args;

namespace Astra.Core.Triggers
{
    /// <summary>
    /// 异步事件处理器委托
    /// </summary>
    public delegate Task AsyncEventHandler<TEventArgs>(object sender, TEventArgs e) where TEventArgs : EventArgs;


    /// <summary>
    /// 触发器接口
    /// </summary>
    public interface ITrigger
    {
        /// <summary>
        /// 触发器唯一标识ID（由 TriggerManager 自动设置）
        /// </summary>
        string TriggerId { get; set; }

        /// <summary>
        /// 触发器名称（用于显示）
        /// </summary>
        string TriggerName { get; }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 防重复触发配置
        /// </summary>
        AntiRepeatConfig AntiRepeatConfig { get; set; }

        /// <summary>
        /// 异步事件：当触发器被触发时
        /// </summary>
        event AsyncEventHandler<TriggerEventArgs> OnTriggeredAsync;

        /// <summary>
        /// 启动触发器
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止触发器
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 获取触发统计信息
        /// </summary>
        Dictionary<string, object> GetTriggerStatistics();
    }
}
