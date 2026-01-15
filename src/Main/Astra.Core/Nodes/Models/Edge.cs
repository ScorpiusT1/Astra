using Astra.Core.Nodes.Geometry;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 节点连线模型（纯数据对象，便于序列化和撤销/重做）
    /// </summary>
    public class Edge
    {
        public Edge()
        {
            Id = Guid.NewGuid().ToString();
            Points = new List<Point2D>();
        }

        /// <summary>
        /// 连线唯一标识
        /// </summary>
        [JsonProperty(Order = 1)]
        public string Id { get; set; }

        /// <summary>
        /// 源节点 Id（必须存在于节点集合）
        /// </summary>
        [JsonProperty(Order = 2)]
        public string SourceNodeId { get; set; }

        /// <summary>
        /// 目标节点 Id（必须存在于节点集合）
        /// </summary>
        [JsonProperty(Order = 3)]
        public string TargetNodeId { get; set; }

        /// <summary>
        /// 源端口 Id（可选，用于多端口区分）
        /// </summary>
        [JsonProperty(Order = 4)]
        public string SourcePortId { get; set; }

        /// <summary>
        /// 目标端口 Id（可选，用于多端口区分）
        /// </summary>
        [JsonProperty(Order = 5)]
        public string TargetPortId { get; set; }

        /// <summary>
        /// 预留路径点（直线可为空，折线/自定义路由可填充）
        /// 使用画布坐标系。
        /// </summary>
        [JsonProperty(Order = 6)]
        public List<Point2D> Points { get; set; }

        /// <summary>
        /// 选中状态（供 UI 高亮）
        /// </summary>
        [JsonIgnore]
        public bool IsSelected { get; set; }

        /// <summary>
        /// 是否在刷新时保留路径点，不自动重新计算
        /// 用于复制/粘贴场景，确保克隆的连线保持原始路径形状
        /// </summary>
        [JsonIgnore]
        public bool PreservePathOnRefresh { get; set; }

        /// <summary>
        /// 克隆连线（用于复制粘贴）
        /// </summary>
        public Edge Clone()
        {
            return new Edge
            {
                Id = Guid.NewGuid().ToString(), // 生成新的ID
                SourceNodeId = this.SourceNodeId,
                TargetNodeId = this.TargetNodeId,
                SourcePortId = this.SourcePortId,
                TargetPortId = this.TargetPortId,
                Points = this.Points != null ? new List<Point2D>(this.Points) : new List<Point2D>(),
                IsSelected = false // 克隆后默认不选中
            };
        }
    }
}

