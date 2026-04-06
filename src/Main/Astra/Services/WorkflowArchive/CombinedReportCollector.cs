using System.Collections.Generic;
using System.IO;
using System.Linq;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;

namespace Astra.Services.WorkflowArchive
{
    /// <summary>
    /// 线程安全的合并归档收集器（单例）；由 Home 编排层 Begin/End，由归档服务 Add。
    /// </summary>
    public sealed class CombinedReportCollector : ICombinedReportCollector
    {
        private readonly object _gate = new();
        private readonly List<(int Order, TestReportData Data)> _sections = new();
        private readonly List<(int Order, WorkFlowRunRecord Record, string Condition)> _runRecords = new();

        public bool IsActive { get; private set; }
        public string? SharedOutputDirectory { get; private set; }
        public string? SharedFileTimestamp { get; private set; }
        public string? SharedRunLogFilePath { get; private set; }

        public void BeginBatch()
        {
            lock (_gate)
            {
                _sections.Clear();
                _runRecords.Clear();
                SharedOutputDirectory = null;
                SharedFileTimestamp = null;
                SharedRunLogFilePath = null;
                IsActive = true;
            }
        }

        public void SetSharedOutputDirectory(string directory, string fileTimestamp)
        {
            lock (_gate)
            {
                if (!IsActive) return;
                SharedOutputDirectory ??= directory;
                SharedFileTimestamp ??= fileTimestamp;
            }
        }

        public void AddSection(TestReportData data)
        {
            if (data == null) return;
            lock (_gate)
            {
                if (!IsActive) return;
                _sections.Add((data.SectionSequenceOrder, data));
            }
        }

        public void AddRunRecord(WorkFlowRunRecord record, string condition, int reportSequenceOrder = 0)
        {
            if (record == null) return;
            lock (_gate)
            {
                if (!IsActive) return;
                _runRecords.Add((reportSequenceOrder, record, condition));
            }
        }

        /// <inheritdoc />
        public string? TryAllocateSharedRunLogFilePath(string directory, string fileTimestamp, string? serialNumberFilePart = null)
        {
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileTimestamp))
                return null;

            lock (_gate)
            {
                if (!IsActive)
                    return null;
                if (SharedRunLogFilePath != null)
                    return SharedRunLogFilePath;

                try
                {
                    var dir = Path.GetFullPath(directory.Trim());
                    Directory.CreateDirectory(dir);
                    var snSeg = string.IsNullOrWhiteSpace(serialNumberFilePart)
                        ? "SN_PENDING"
                        : serialNumberFilePart.Trim();
                    if (snSeg.Length > 80)
                        snSeg = snSeg[..80];
                    var name = $"{snSeg}_{fileTimestamp.Trim()}_batch_workflow_run.log";
                    SharedRunLogFilePath = Path.Combine(dir, name);
                    return SharedRunLogFilePath;
                }
                catch
                {
                    return null;
                }
            }
        }

        public CombinedReportBatch EndBatch()
        {
            lock (_gate)
            {
                IsActive = false;
                var batch = new CombinedReportBatch
                {
                    Sections = _sections
                        .OrderBy(x => x.Order)
                        .ThenBy(x => x.Data.StartTime)
                        .Select(x => x.Data)
                        .ToList(),
                    SharedOutputDirectory = SharedOutputDirectory,
                    SharedFileTimestamp = SharedFileTimestamp,
                    RunRecords = _runRecords
                        .OrderBy(x => x.Order)
                        .ThenBy(x => x.Record.StartTime)
                        .Select(x => (x.Record, x.Condition))
                        .ToList()
                };
                _sections.Clear();
                _runRecords.Clear();
                SharedOutputDirectory = null;
                SharedFileTimestamp = null;
                SharedRunLogFilePath = null;
                return batch;
            }
        }
    }
}
