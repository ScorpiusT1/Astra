// ⭐ 已移除 using Astra.Bootstrap.Core;（不再需要 IBootstrapTask）

namespace Astra.Bootstrap.Services
{
    #region Bootstrap Result
    /// <summary>
    /// 启动结果
    /// ⭐ 已移除任务相关属性（任务系统已移除）
    /// </summary>
    public class BootstrapResult
    {
        public bool IsSuccess { get; set; }
        public bool IsCancelled { get; set; }
        public TimeSpan TotalTime { get; set; }
        public Exception FatalException { get; set; }

        // ⭐ 已移除任务相关属性和方法（任务系统已移除）
    }

    #endregion
}

