using System;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Reporting
{
    [Flags]
    public enum ReportWhitelistCategories : byte
    {
        None = 0,
        Scalar = 1,
        Curve = 2,
        ChartProducer = 4,

        /// <summary>反序列化或未分类时的兼容值：三个列表均显示（与早期「全部节点」行为一致）。</summary>
        LegacyShowInAll = Scalar | Curve | ChartProducer
    }

    public static class ReportWhitelistNodeCategories
    {
        public static ReportWhitelistCategories FromNode(Node? n)
        {
            if (n == null)
                return ReportWhitelistCategories.None;

            var c = ReportWhitelistCategories.None;
            if (n is IReportWhitelistScalarNode)
                c |= ReportWhitelistCategories.Scalar;
            if (n is IReportWhitelistCurveNode)
                c |= ReportWhitelistCategories.Curve;
            if (n is IReportWhitelistChartProducerNode)
                c |= ReportWhitelistCategories.ChartProducer;

            return c == ReportWhitelistCategories.None ? ReportWhitelistCategories.None : c;
        }
    }
}
