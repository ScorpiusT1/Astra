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
        SkipIfBusy
    }
}
