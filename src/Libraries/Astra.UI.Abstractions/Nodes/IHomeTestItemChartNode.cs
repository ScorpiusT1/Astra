namespace Astra.UI.Abstractions.Nodes;

/// <summary>
/// 标记节点可在主页测试项树中提供「打开图表」能力。
/// 实现类型须公开 <see cref="ShowHomeChartButton"/>，并在属性面板上用 <see cref="System.ComponentModel.DataAnnotations.DisplayAttribute"/> 暴露，供用户配置。
/// 未实现本接口但可展示图表的节点类型可实现 <see cref="IHomeTestItemChartEligibleNode"/>，使「打开图表」在树加载后即可见。
/// 单次执行输出 <see cref="NodeUiOutputKeys.HasChartData"/> 时，即使未实现上述接口，按钮仍可出现。
/// </summary>
public interface IHomeTestItemChartNode
{
    /// <summary>为 true 时，在首页测试项树中允许显示「打开图表」按钮（默认建议为 true）。</summary>
    bool ShowHomeChartButton { get; set; }

    /// <summary>
    /// 为 true 时在属性面板显示并采用 <see cref="ChartXAxisLabel"/> 等四项坐标轴配置；
    /// 为 false 时应在实现类型中隐藏上述属性（例如实现 <c>Astra.UI.Abstractions.Interfaces.IPropertyVisibilityProvider</c>），
    /// 并在节点执行逻辑中自行写入 <see cref="NodeUiOutputKeys"/> 的轴信息。
    /// </summary>
    bool UsePropertyPanelForChartAxis { get; set; }

    /// <summary>图表横轴（底部）标题，对应 <see cref="ChartDisplayPayload.BottomAxisLabel"/>。</summary>
    string ChartXAxisLabel { get; set; }

    /// <summary>图表横轴单位，对应 <see cref="ChartDisplayPayload.BottomAxisUnit"/>。</summary>
    string ChartXAxisUnit { get; set; }

    /// <summary>图表纵轴（左侧）标题，对应 <see cref="ChartDisplayPayload.LeftAxisLabel"/>。</summary>
    string ChartYAxisLabel { get; set; }

    /// <summary>图表纵轴单位，对应 <see cref="ChartDisplayPayload.LeftAxisUnit"/>。</summary>
    string ChartYAxisUnit { get; set; }
}
