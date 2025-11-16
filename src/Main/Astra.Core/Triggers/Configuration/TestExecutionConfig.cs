using Astra.Core.Triggers.Enums;

namespace Astra.Core.Triggers.Configuration
{
    /// <summary>
    /// 测试执行配置
    /// </summary>
    public class TestExecutionConfig
    {
        /// <summary>执行模式</summary>
        public TestExecutionMode ExecutionMode { get; set; } = TestExecutionMode.Serial;

        /// <summary>最大并发数（并行模式）</summary>
        public int MaxConcurrency { get; set; } = 3;

        /// <summary>最大队列长度（串行模式）</summary>
        public int MaxQueueLength { get; set; } = 100;

        /// <summary>测试超时时间（毫秒）</summary>
        public int TestTimeoutMs { get; set; } = 300000;
    }
}
