using Astra.Core.Nodes.Geometry;
using Newtonsoft.Json;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 子流程引用（在主流程画布上显示的流程节点信息）
    /// </summary>
    public class WorkflowReference
    {
        public WorkflowReference()
        {
            Id = Guid.NewGuid().ToString();
            Position = new Point2D(0, 0);
            Size = new Size2D(200, 150);
        }

        /// <summary>
        /// 引用节点唯一标识
        /// </summary>
        [JsonProperty(Order = 1)]
        public string Id { get; set; }

        /// <summary>
        /// 引用的子流程ID
        /// </summary>
        [JsonProperty(Order = 2)]
        public string SubWorkflowId { get; set; }

        /// <summary>
        /// 在主流程画布上的位置
        /// </summary>
        [JsonProperty(Order = 3)]
        public Point2D Position { get; set; }

        /// <summary>
        /// 在主流程画布上的大小
        /// </summary>
        [JsonProperty(Order = 4)]
        public Size2D Size { get; set; }

        /// <summary>
        /// 显示名称（可以覆盖子流程的名称）
        /// </summary>
        [JsonProperty(Order = 5)]
        public string DisplayName { get; set; }

        /// <summary>
        /// 参数映射（子流程输入参数 -> 主流程变量或常量）
        /// </summary>
        [JsonProperty(Order = 6)]
        public Dictionary<string, string> InputParameterMapping { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 输出参数映射（子流程输出参数 -> 主流程变量）
        /// </summary>
        [JsonProperty(Order = 7)]
        public Dictionary<string, string> OutputParameterMapping { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 单节点失败后是否继续执行其后继节点
        /// </summary>
        [JsonProperty(Order = 8)]
        public bool ContinueOnFailure { get; set; } = false;

        /// <summary>
        /// 节点是否启用
        /// </summary>
        [JsonProperty(Order = 9)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 最后执行：主流程常规拓扑跑完后执行；前方子流程失败或跳过后仍会执行（与 ContinueOnFailure 正交）。
        /// </summary>
        [JsonProperty(Order = 10)]
        public bool ExecuteLast { get; set; }

        /// <summary>
        /// 是否在首页测试项模块中展示该子流程（与 <see cref="Node.ShowInHomeTestItems"/> 配合；任一为 false 则不展示整组）。
        /// </summary>
        [JsonProperty(Order = 11)]
        public bool ShowInHomeTestItems { get; set; } = true;
    }
}

