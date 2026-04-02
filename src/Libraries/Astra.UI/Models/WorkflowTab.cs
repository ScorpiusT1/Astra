using Astra.Core.Nodes.Models;
using Astra.UI.Commands;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Models
{
    /// <summary>
    /// 流程标签页模型
    /// 用于在UI中管理多个流程标签页
    /// 符合单一职责原则：专门负责流程标签页的数据管理
    /// </summary>
    public partial class WorkflowTab : ObservableObject
    {
        public WorkflowTab()
        {
            Id = Guid.NewGuid().ToString();
            _nodes = new ObservableCollection<Node>();
            _nodes.CollectionChanged += OnNodesCollectionChanged;
            _edges = new ObservableCollection<Edge>();
            _edges.CollectionChanged += OnEdgesCollectionChanged;
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
        }

        /// <summary>
        /// 标签页唯一标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 标签页显示名称
        /// 对于子流程，直接返回 WorkFlowNode.Name，确保与主流程节点绑定的是同一个数据源
        /// </summary>
        public string Name
        {
            get
            {
                // 如果是子流程，直接返回 WorkFlowNode.Name（与主流程节点绑定的是同一个对象）
                if (Type == WorkflowType.Sub)
                {
                    var subWorkflow = GetSubWorkflow();
                    if (subWorkflow != null)
                    {
                        return subWorkflow.Name ?? _name;
                    }
                }
                // 如果是主流程，返回存储的名称
                return _name;
            }
            set
            {
                // 获取当前值（对于子流程，会返回 WorkFlowNode.Name）
                var oldValue = Name;

                // 如果值没有改变，不处理
                if (oldValue == value)
                {
                    return;
                }

                _name = value;

                // 如果是子流程，同时更新 WorkFlowNode.Name（确保数据同步）
                if (Type == WorkflowType.Sub)
                {
                    var subWorkflow = GetSubWorkflow();
                    if (subWorkflow != null)
                    {
                        subWorkflow.Name = value;
                    }
                }
                // 如果是主流程，同时更新 MasterWorkflow.Name
                else if (Type == WorkflowType.Master)
                {
                    var masterWorkflow = GetMasterWorkflow();
                    if (masterWorkflow != null)
                    {
                        masterWorkflow.Name = value;
                    }
                }

                // 触发属性变更通知（确保UI更新）
                OnPropertyChanged(nameof(Name));
            }
        }

        private string _name;

        /// <summary>
        /// 是否处于编辑模式（用于内联重命名）
        /// </summary>
        [ObservableProperty]
        private bool _isInEditMode;

        /// <summary>
        /// 编辑中的名称（用于内联重命名）
        /// </summary>
        [ObservableProperty]
        private string _editingName;

        /// <summary>
        /// 原始名称（进入编辑模式时保存，用于取消编辑）
        /// </summary>
        private string _originalName;

        /// <summary>
        /// 流程类型（主流程或子流程）
        /// </summary>
        public WorkflowType Type { get; set; }

        /// <summary>
        /// 是否已修改（未保存）
        /// </summary>
        public bool IsModified { get; set; }

        /// <summary>
        /// 是否当前活动标签页（用于控制多标签页的内容可见性）
        /// </summary>
        [ObservableProperty]
        private bool _isActive;

        /// <summary>
        /// Tab 标题指示器状态（灰/橙/红/绿）。
        /// </summary>
        [ObservableProperty]
        private NodeExecutionState _tabExecutionState = NodeExecutionState.Idle;

        /// <summary>
        /// 文件路径（如果已保存）
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 节点集合（用于画布显示）
        /// </summary>
        public ObservableCollection<Node> Nodes
        {
            get => _nodes;
            set
            {
                if (ReferenceEquals(_nodes, value)) return;
                if (_nodes != null) _nodes.CollectionChanged -= OnNodesCollectionChanged;
                _nodes = value;
                if (_nodes != null) _nodes.CollectionChanged += OnNodesCollectionChanged;
                OnPropertyChanged(nameof(Nodes));
                ReplayExistingEdges();
            }
        }

        private ObservableCollection<Node> _nodes;

        /// <summary>
        /// 连线集合（用于画布显示）
        /// </summary>
        public ObservableCollection<Edge> Edges
        {
            get => _edges;
            set
            {
                if (ReferenceEquals(_edges, value)) return;
                if (_edges != null) _edges.CollectionChanged -= OnEdgesCollectionChanged;
                _edges = value;
                if (_edges != null) _edges.CollectionChanged += OnEdgesCollectionChanged;
                OnPropertyChanged(nameof(Edges));
                ReplayExistingEdges();
            }
        }

        private ObservableCollection<Edge> _edges;

        /// <summary>
        /// 流程数据（MasterWorkflow 或 WorkFlowNode）
        /// </summary>
        public object WorkflowData { get; set; }

        /// <summary>
        /// 命令管理器（用于撤销/重做，每个流程有独立的命令历史）
        /// </summary>
        public Commands.CommandManager CommandManager { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// 获取主流程数据
        /// </summary>
        public MasterWorkflow GetMasterWorkflow()
        {
            return WorkflowData as MasterWorkflow;
        }

        /// <summary>
        /// 获取子流程数据（WorkFlowNode）
        /// </summary>
        public WorkFlowNode GetSubWorkflow()
        {
            return WorkflowData as WorkFlowNode;
        }

        /// <summary>
        /// 将画布 <see cref="Nodes"/> 写回子流程 <see cref="WorkFlowNode.Nodes"/>，并刷新各节点上的
        /// <see cref="Node.ContainingWorkflow"/>。属性面板等依赖该引用枚举同流程内兄弟节点。
        /// </summary>
        public void SyncNodesToSubWorkflowModel()
        {
            if (Type != WorkflowType.Sub)
                return;
            var sub = GetSubWorkflow();
            if (sub == null)
                return;
            sub.Nodes = Nodes?.ToList() ?? new List<Node>();
            sub.RebindChildWorkflowReferences();
        }

        /// <summary>
        /// 标记为已修改
        /// </summary>
        public void MarkAsModified()
        {
            IsModified = true;
            ModifiedAt = DateTime.Now;
        }

        /// <summary>
        /// 标记为已保存
        /// </summary>
        public void MarkAsSaved(string filePath = null)
        {
            IsModified = false;
            if (!string.IsNullOrEmpty(filePath))
                FilePath = filePath;
        }

        private void OnEdgesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (Nodes == null || Nodes.Count == 0) return;

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (Edge edge in e.NewItems)
                    NotifyNodes(edge, attached: true);
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (Edge edge in e.OldItems)
                    NotifyNodes(edge, attached: false);
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                if (e.OldItems != null)
                    foreach (Edge edge in e.OldItems) NotifyNodes(edge, attached: false);
                if (e.NewItems != null)
                    foreach (Edge edge in e.NewItems) NotifyNodes(edge, attached: true);
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var node in Nodes)
                    node.OnConnectionDetached(null, null, null);
            }
        }

        private void OnNodesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (Node node in e.OldItems)
                    node.OnRemovedFromWorkflow();
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace && e.OldItems != null)
            {
                foreach (Node node in e.OldItems)
                    node.OnRemovedFromWorkflow();
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Reset 无法遍历旧元素；如需处理可在调用方 Clear 前遍历
            }
        }

        /// <summary>
        /// 反序列化后重放所有已有连线的 Attached 事件。
        /// Nodes/Edges 整体赋值时不触发 CollectionChanged.Add，
        /// 需要手动遍历已有的 Edge 并通知相关节点，以重建 _upstreamSources 等运行时状态。
        /// 在 Nodes 和 Edges 两个 setter 末尾均调用，保证无论哪个先赋值，
        /// 当两者都就绪时事件会被正确重放。
        /// </summary>
        private void ReplayExistingEdges()
        {
            if (_nodes == null || _nodes.Count == 0 || _edges == null || _edges.Count == 0)
                return;

            foreach (var edge in _edges)
                NotifyNodes(edge, attached: true);
        }

        private void NotifyNodes(Edge edge, bool attached)
        {
            if (edge == null) return;

            var sourceNode = string.IsNullOrEmpty(edge.SourceNodeId)
                ? null
                : Nodes.FirstOrDefault(n => n.Id == edge.SourceNodeId);
            var targetNode = string.IsNullOrEmpty(edge.TargetNodeId)
                ? null
                : Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);

            if (attached)
            {
                sourceNode?.OnConnectionAttached(edge, sourceNode, targetNode);
                targetNode?.OnConnectionAttached(edge, sourceNode, targetNode);
            }
            else
            {
                sourceNode?.OnConnectionDetached(edge, sourceNode, targetNode);
                targetNode?.OnConnectionDetached(edge, sourceNode, targetNode);
            }
        }

        /// <summary>
        /// 进入编辑模式
        /// </summary>
        public void BeginEdit()
        {
            _originalName = Name;
            EditingName = Name;
            IsInEditMode = true;

            System.Diagnostics.Debug.WriteLine($"[WorkflowTab] BeginEdit: Name={Name}, EditingName={EditingName}, IsInEditMode={IsInEditMode}");
        }

        /// <summary>
        /// 确认编辑（保存新名称）
        /// 返回新名称，如果名称未改变或为空则返回 null
        /// </summary>
        public string CommitEdit()
        {
            if (!IsInEditMode || string.IsNullOrWhiteSpace(EditingName))
            {
                CancelEdit();
                return null;
            }

            var newName = EditingName.Trim();

            // 检查名称是否改变（与原始名称比较）
            if (newName == _originalName)
            {
                CancelEdit();
                return null;
            }

            // 名称已改变，退出编辑模式并返回新名称
            IsInEditMode = false;
            return newName;
        }

        /// <summary>
        /// 取消编辑（恢复原始名称）
        /// </summary>
        public void CancelEdit()
        {
            EditingName = _originalName;
            IsInEditMode = false;
        }
    }
}
