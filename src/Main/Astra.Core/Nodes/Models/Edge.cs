using Astra.Core.Nodes.Geometry;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        public string Id { get; set; }

        /// <summary>
        /// 源节点 Id（必须存在于节点集合）
        /// </summary>
        public string SourceNodeId { get; set; }

        /// <summary>
        /// 目标节点 Id（必须存在于节点集合）
        /// </summary>
        public string TargetNodeId { get; set; }

        /// <summary>
        /// 源端口 Id（可选，用于多端口区分）
        /// </summary>
        public string SourcePortId { get; set; }

        /// <summary>
        /// 目标端口 Id（可选，用于多端口区分）
        /// </summary>
        public string TargetPortId { get; set; }

        /// <summary>
        /// 预留路径点（直线可为空，折线/自定义路由可填充）
        /// 使用画布坐标系。
        /// </summary>
        public List<Point2D> Points { get; set; }

        /// <summary>
        /// 选中状态（供 UI 高亮）
        /// </summary>
        [JsonIgnore]
        public bool IsSelected { get; set; }
    }
}

