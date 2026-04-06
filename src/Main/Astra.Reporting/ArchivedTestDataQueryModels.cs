using System;

namespace Astra.Reporting
{
    /// <summary>归档文件类型：原始 TDMS、音频 WAV、算法 TDMS、报告相关文件、运行日志。</summary>
    [Flags]
    public enum ArchivedDataCategoryFlags
    {
        None = 0,
        Raw = 1,
        Algorithm = 2,
        Report = 4,
        Audio = 8,
        RunLog = 16,
        All = Raw | Algorithm | Report | Audio | RunLog
    }

    /// <summary>查询条件：SN 模糊、日期范围、类型掩码。</summary>
    public sealed class ArchivedTestDataQueryCriteria
    {
        public string? SnContains { get; init; }

        /// <summary>本地日期的起（含）；仅日期部分有效。</summary>
        public DateTime? FromLocalDateInclusive { get; init; }

        /// <summary>本地日期的止（含）；仅日期部分有效。</summary>
        public DateTime? ToLocalDateInclusive { get; init; }

        public ArchivedDataCategoryFlags Categories { get; init; } = ArchivedDataCategoryFlags.All;
    }

    /// <summary>单条归档文件记录。</summary>
    public sealed class ArchivedDataFileRow
    {
        public string FullPath { get; init; } = string.Empty;

        public ArchivedDataCategoryFlags Category { get; init; }

        public string CategoryDisplayName { get; init; } = string.Empty;

        public string DateFolder { get; init; } = string.Empty;

        public string SnFolder { get; init; } = string.Empty;

        public string RunFolder { get; init; } = string.Empty;

        /// <summary>界面用：当前结果表格（当前页）中的行序号，从 1 起；由查询视图在分页切片时写入。</summary>
        public int TableSequence { get; set; }

        public string FileName { get; init; } = string.Empty;

        public long LengthBytes { get; init; }

        public DateTime LastWriteTimeUtc { get; init; }
    }

    /// <summary>查询结果。</summary>
    public sealed class ArchivedTestDataQueryResult
    {
        public string ArchiveRoot { get; init; } = string.Empty;

        public IReadOnlyList<ArchivedDataFileRow> Items { get; init; } = Array.Empty<ArchivedDataFileRow>();

        public string? ErrorMessage { get; init; }
    }
}
