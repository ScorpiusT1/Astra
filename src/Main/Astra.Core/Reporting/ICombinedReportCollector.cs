using Astra.Core.Nodes.Models;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 多子流程合并归档收集器：批次执行期间所有子流程共享同一输出目录，
    /// 累积 <see cref="TestReportData"/>、<see cref="WorkFlowRunRecord"/>，
    /// 全部完成后由调用方统一生成一份报告 + 合并 RunRecord。
    /// <para>线程安全：多个子流程可能并行归档。</para>
    /// </summary>
    public interface ICombinedReportCollector
    {
        /// <summary>是否正在收集（批次已 Begin 但未 End）。</summary>
        bool IsActive { get; }

        /// <summary>批次共享的输出目录（首次由归档服务设置）。</summary>
        string? SharedOutputDirectory { get; }

        /// <summary>批次共享的文件时间戳（首次由归档服务设置）。</summary>
        string? SharedFileTimestamp { get; }

        /// <summary>开启一轮合并归档收集；会清除上一轮残留数据。</summary>
        void BeginBatch();

        /// <summary>
        /// 设置共享输出目录和时间戳（线程安全，仅首次调用生效）。
        /// 由归档服务在处理第一个子流程时调用。
        /// </summary>
        void SetSharedOutputDirectory(string directory, string fileTimestamp);

        /// <summary>添加一个子流程的报告数据段。</summary>
        void AddSection(TestReportData data);

        /// <summary>添加一个子流程的 RunRecord（含工况标签）。</summary>
        /// <param name="reportSequenceOrder">与 <see cref="TestReportData.SectionSequenceOrder"/> 一致，用于合并结果排序。</param>
        void AddRunRecord(WorkFlowRunRecord record, string condition, int reportSequenceOrder = 0);

        /// <summary>
        /// 结束本轮收集，返回全部已收集的数据。
        /// 调用后 <see cref="IsActive"/> 变为 false。
        /// </summary>
        CombinedReportBatch EndBatch();
    }

    /// <summary>
    /// 一轮合并归档收集的完整结果。
    /// </summary>
    public sealed class CombinedReportBatch
    {
        public IReadOnlyList<TestReportData> Sections { get; init; } = [];

        /// <summary>共享输出目录。</summary>
        public string? SharedOutputDirectory { get; init; }

        /// <summary>共享文件时间戳。</summary>
        public string? SharedFileTimestamp { get; init; }

        /// <summary>各子流程的 RunRecord + 工况。</summary>
        public IReadOnlyList<(WorkFlowRunRecord Record, string Condition)> RunRecords { get; init; } = [];
    }
}
