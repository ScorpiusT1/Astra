namespace Astra.UI.Abstractions.Nodes;

/// <summary>
/// 节点输出到主页/图表 UI 的 <c>ExecutionResult.OutputData</c> 键名（集中封装，禁止散落字符串）。
/// </summary>
public static class NodeUiOutputKeys
{
    public const string ActualValue = "Ui.ActualValue";
    public const string LowerLimit = "Ui.LowerLimit";
    public const string UpperLimit = "Ui.UpperLimit";
    public const string ValueCheckPass = "Ui.ValueCheckPass";
    public const string CurveCheckPass = "Ui.CurveCheckPass";
    public const string HasChartData = "Ui.HasChartData";
    public const string ChartArtifactKey = "Ui.ChartArtifactKey";

    /// <summary>主页图表横轴标题（与 <see cref="ChartDisplayPayload.BottomAxisLabel"/> 一致）。</summary>
    public const string ChartXAxisLabel = "Ui.ChartXAxisLabel";

    /// <summary>主页图表横轴单位（与 <see cref="ChartDisplayPayload.BottomAxisUnit"/> 一致）。</summary>
    public const string ChartXAxisUnit = "Ui.ChartXAxisUnit";

    /// <summary>主页图表纵轴标题（与 <see cref="ChartDisplayPayload.LeftAxisLabel"/> 一致）。</summary>
    public const string ChartYAxisLabel = "Ui.ChartYAxisLabel";

    /// <summary>主页图表纵轴单位（与 <see cref="ChartDisplayPayload.LeftAxisUnit"/> 一致）。</summary>
    public const string ChartYAxisUnit = "Ui.ChartYAxisUnit";

    /// <summary>
    /// 内联图表快照（仅适合较小数据；大数据请用 Raw 存储挂载 <see cref="ChartDisplayPayload"/>）。
    /// </summary>
    public const string ChartPayloadSnapshot = "Ui.ChartPayloadSnapshot";

    public const string Summary = "Ui.Summary";
    public const string FailReason = "Ui.FailReason";
    public const string CurveFailDetail = "Ui.CurveFailDetail";

    /// <summary>所有供事件/UI 同步的键（用于从 OutputData 过滤）。</summary>
    public static readonly string[] All =
    {
        ActualValue,
        LowerLimit,
        UpperLimit,
        ValueCheckPass,
        CurveCheckPass,
        HasChartData,
        ChartArtifactKey,
        ChartXAxisLabel,
        ChartXAxisUnit,
        ChartYAxisLabel,
        ChartYAxisUnit,
        Summary,
        FailReason,
        CurveFailDetail
    };
}
