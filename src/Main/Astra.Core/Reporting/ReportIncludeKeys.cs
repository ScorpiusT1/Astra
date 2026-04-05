using System;
using System.Collections.Generic;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 测试报告纳入标记：由引擎写入 <see cref="NodeRunRecord.OutputSnapshot"/>，
    /// 由 <see cref="Data.TestDataBus"/> 写入产物 <see cref="DataArtifactReference.Preview"/>。
    /// 缺省视为纳入（兼容旧流程与未走统一发布路径的产物）。
    /// </summary>
    public static class ReportIncludeKeys
    {
        public const string IncludeInReport = "__IncludeInReport";

        public static bool SnapshotIncludesInReport(IReadOnlyDictionary<string, object>? snapshot)
        {
            if (snapshot == null || !snapshot.TryGetValue(IncludeInReport, out var v))
                return true;
            return CoerceToBool(v, defaultIfUnrecognized: true);
        }

        public static bool PreviewIncludesInReport(IReadOnlyDictionary<string, object>? preview)
        {
            if (preview == null || !preview.TryGetValue(IncludeInReport, out var v))
                return true;
            return CoerceToBool(v, defaultIfUnrecognized: true);
        }

        public static bool NodeRunIncludesInReport(NodeRunRecord? nr) =>
            nr == null || SnapshotIncludesInReport(nr.OutputSnapshot);

        private static bool CoerceToBool(object v, bool defaultIfUnrecognized)
        {
            return v switch
            {
                bool b => b,
                string s when bool.TryParse(s, out var bs) => bs,
                int i => i != 0,
                long l => l != 0,
                _ => defaultIfUnrecognized
            };
        }
    }
}
