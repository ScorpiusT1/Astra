namespace Astra.Plugins.Algorithms.Helpers
{
    /// <summary>
    /// 算法多通道计算的统一并行入口：显式并行度、与 <see cref="CancellationToken"/> 协作取消。
    /// </summary>
    internal static class AlgorithmParallel
    {
        /// <summary>默认并行度上限（逻辑处理器数，至少为 1）。</summary>
        public static int EffectiveMaxDegree => Math.Max(1, Environment.ProcessorCount);

        /// <summary>
        /// 对索引区间 <c>[fromInclusive, toExclusive)</c> 执行 <paramref name="body"/>，与裸 <see cref="Parallel.For(int, int, Action{int})"/> 等价但附带取消与并行度策略。
        /// </summary>
        public static void For(int fromInclusive, int toExclusive, CancellationToken cancellationToken, Action<int> body)
        {
            if (toExclusive <= fromInclusive)
                return;

            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = EffectiveMaxDegree
            };

            Parallel.For(fromInclusive, toExclusive, options, body);
        }
    }
}
