namespace Astra.Core.Triggers.Interlock
{
    /// <summary>
    /// 安全联锁 IO 规则在沿/电平满足时执行的动作。
    /// </summary>
    public enum InterlockRuleAction
    {
        /// <summary>不执行任何动作（可用于仅监控一侧沿）。</summary>
        None = 0,

        /// <summary>暂停当前由会话服务跟踪的全部运行中测试。</summary>
        PauseAllTests = 1,

        /// <summary>恢复当前已暂停的、由会话服务跟踪的全部测试。</summary>
        ResumeAllTests = 2,

        /// <summary>停止（取消）当前由会话服务跟踪的全部运行中测试。</summary>
        StopAllTests = 3,
    }
}
