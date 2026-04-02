namespace Astra.Core.Reporting
{
    /// <summary>可出现在报告「单值判定」白名单的节点（运行结果链中含单值判定相关 UI 输出，如 <c>Ui.ActualValue</c>）。</summary>
    public interface IReportWhitelistScalarNode
    {
    }

    /// <summary>可出现在报告「曲线判定」白名单的节点（运行结果中含 <c>Ui.CurveCheckPass</c> 等曲线判定输出）。</summary>
    public interface IReportWhitelistCurveNode
    {
    }

    /// <summary>可作为数据总线图表「产生者节点」参与报告图表过滤的节点。</summary>
    public interface IReportWhitelistChartProducerNode
    {
    }
}
