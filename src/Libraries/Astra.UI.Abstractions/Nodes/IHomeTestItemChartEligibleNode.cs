namespace Astra.UI.Abstractions.Nodes;

/// <summary>
/// 标记该节点类型可在主页测试项树加载时显示「打开图表」按钮，无需等待本次执行写入 <see cref="NodeUiOutputKeys.HasChartData"/>。
/// 与 <see cref="IHomeTestItemChartNode"/> 配合使用：后者提供「显示图表按钮」开关与坐标轴面板；本接口用于无该面板的图表类节点（算法、卡控、数据处理等）。
/// </summary>
public interface IHomeTestItemChartEligibleNode
{
}
