namespace Astra.Core.Triggers.Enums
{
    /// <summary>
    /// 测试执行模式
    /// </summary>
    public enum TestExecutionMode
    {
        /// <summary>串行执行（一个接一个）</summary>
        Serial,
        /// <summary>并行执行（多个同时执行）</summary>
        Parallel,
        /// <summary>如果忙碌则跳过</summary>
        SkipIfBusy,

        /// <summary>
        /// 阻塞直至当前测试完成：触发事件链会 await 完整测试（含所有观察者），
        /// 轮询型触发器在测试结束前不会进入下一次检测；多触发器时全局互斥，同一时间仅一场测试。
        /// </summary>
        BlockUntilTestComplete
    }
}
