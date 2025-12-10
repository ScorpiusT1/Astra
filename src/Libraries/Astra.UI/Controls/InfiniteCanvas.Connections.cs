using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Astra.Core.Nodes.Geometry;
using Astra.Core.Nodes.Models;
using Astra.UI.Commands;

namespace Astra.UI.Controls
{
    /// <summary>
    /// InfiniteCanvas 的连线功能部分类
    /// 包含：连线绘制、A*寻路、端口检测、正交路径规划
    /// </summary>
    public partial class InfiniteCanvas
    {
        #region 连线算法常量

        /// <summary>
        /// 闭区间碰撞检测的误差容限（防止擦边误判）
        /// </summary>
        private const double EPS = 0.0001;

        /// <summary>
        /// 障碍物外扩距离（确保路径不会紧贴节点）
        /// </summary>
        private const double ObstacleMargin = 10.0;

        /// <summary>
        /// 端口外延距离（连接点到外延点的距离）
        /// </summary>
        private const double PortExtensionDistance = 18.0;

        /// <summary>
        /// RVG 网格生成时的额外间隙（确保网格线不在障碍物边界上）
        /// </summary>
        private const double GridDelta = 2.0;

        /// <summary>
        /// 外延点强制离开障碍物的安全距离
        /// </summary>
        private const double SafetyOffset = 0.5;

        #endregion

        #region 连线依赖属性

        /// <summary>
        /// 连线数据源（Edge 集合）
        /// </summary>
        public static readonly DependencyProperty EdgeItemsSourceProperty =
            DependencyProperty.Register(nameof(EdgeItemsSource), typeof(IEnumerable), typeof(InfiniteCanvas),
                new PropertyMetadata(null, OnEdgeItemsSourceChanged));

        public IEnumerable EdgeItemsSource
        {
            get => (IEnumerable)GetValue(EdgeItemsSourceProperty);
            set => SetValue(EdgeItemsSourceProperty, value);
        }

        private static void OnEdgeItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            if (canvas._edgeCollectionNotify != null)
            {
                canvas._edgeCollectionNotify.CollectionChanged -= canvas.OnEdgeCollectionChanged;
                canvas._edgeCollectionNotify = null;
            }

            canvas._edgeCollectionNotify = e.NewValue as INotifyCollectionChanged;
            if (canvas._edgeCollectionNotify != null)
            {
                canvas._edgeCollectionNotify.CollectionChanged += canvas.OnEdgeCollectionChanged;
            }

            // 如果连线层还未创建，延迟到模板应用后刷新
            if (canvas._edgeLayer == null)
            {
                System.Diagnostics.Debug.WriteLine("[连线] EdgeItemsSource 变化但连线层未创建，延迟刷新");
                canvas.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (canvas._edgeLayer != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[连线] 延迟刷新连线");
                        canvas.RefreshEdgesImmediate();
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[连线] EdgeItemsSource 变化且连线层已创建，立即刷新");
                canvas.RefreshEdgesImmediate();
            }
        }

        #endregion

        #region 连线私有字段

        private Canvas _edgeLayer;                // 连线层（在节点下方）
        private Canvas _connectionPreviewLayer;   // 临时连线层
        private Polyline _connectionPreviewLine;  // 临时连线（正交路径）
        private bool _isConnecting;
        private Node _connectionSourceNode;
        private FrameworkElement _connectionSourcePortElement;  // 保存源端口元素，用于获取端口ID
        private Point _connectionStartPoint;
        private INotifyCollectionChanged _edgeCollectionNotify;
        private FrameworkElement _hoveredPort;  // 当前悬停的端口
        private const double PortSnapDistance = 30.0;  // 端口吸附距离（像素）

        #endregion

        #region 连线层初始化

        /// <summary>
        /// 创建连线层和临时连线层（插入到内容画布最前，保证在节点下方）
        /// </summary>
        private void EnsureEdgeLayer()
        {
            if (_contentCanvas == null)
                return;

            if (_edgeLayer == null)
            {
                _edgeLayer = new Canvas
                {
                    IsHitTestVisible = false
                };
                _contentCanvas.Children.Insert(0, _edgeLayer);
            }

            if (_connectionPreviewLayer == null)
            {
                _connectionPreviewLayer = new Canvas
                {
                    IsHitTestVisible = false
                };
                // 放在连线层上方，仍在节点下方
                _contentCanvas.Children.Insert(1, _connectionPreviewLayer);
            }

            // 首次创建图层后立即刷新，避免首次连线不显示
            RefreshEdgesImmediate();
        }

        #endregion

        #region 连线绘制与交互

        private void OnEdgeCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[连线] 集合变化 - Action: {e.Action}");
            
            // 如果正在批量操作，跳过自动刷新
            if (_isBatchUpdating)
            {
                _needsRefreshAfterBatch = true;
                return;
            }
            
            // 集合变化时立即刷新（Add/Remove 操作应该立即可见）
            RefreshEdgesImmediate();
        }

        /// <summary>
        /// 刷新连线层（默认节流），拖动多节点时提升实时性
        /// </summary>
        public void RefreshEdges() => RefreshEdgesInternal(force: false);

        /// <summary>
        /// 强制立即刷新连线（忽略节流）
        /// </summary>
        public void RefreshEdgesImmediate() => RefreshEdgesInternal(force: true);
        
        /// <summary>
        /// 开始批量更新（暂停自动刷新连线，用于批量添加节点/连线时提升性能）
        /// </summary>
        public void BeginBatchUpdate()
        {
            _isBatchUpdating = true;
            _needsRefreshAfterBatch = false;
            System.Diagnostics.Debug.WriteLine($"[批量操作] 开始 - 暂停自动刷新");
        }
        
        /// <summary>
        /// 结束批量更新（恢复自动刷新，并执行一次完整刷新）
        /// </summary>
        public void EndBatchUpdate()
        {
            _isBatchUpdating = false;
            System.Diagnostics.Debug.WriteLine($"[批量操作] 结束 - 恢复自动刷新");
            
            // 如果批量操作期间有变化，执行一次完整刷新
            if (_needsRefreshAfterBatch)
            {
                _needsRefreshAfterBatch = false;
                System.Diagnostics.Debug.WriteLine($"[批量操作] 执行延迟刷新");
                RefreshEdgesImmediate();
            }
        }

        /// <summary>
        /// 启用智能连线更新（批量拖动时使用，避免重复计算A*）
        /// </summary>
        public void EnableSmartEdgeUpdate(HashSet<string> movingNodeIds)
        {
            _smartEdgeUpdateEnabled = true;
            _movingNodeIds = movingNodeIds;
            
            // 记录移动节点的初始位置
            _nodeInitialPositions = new Dictionary<string, Point2D>();
            if (ItemsSource != null && movingNodeIds != null)
            {
                foreach (var item in ItemsSource)
                {
                    if (item is Node node && movingNodeIds.Contains(node.Id))
                    {
                        _nodeInitialPositions[node.Id] = node.Position;
                        System.Diagnostics.Debug.WriteLine($"[智能连线] 记录节点初始位置: {node.Name} = ({node.Position.X:F2}, {node.Position.Y:F2})");
                    }
                }
            }
            
            // 🔧 保存原始连线路径，避免累积偏移
            _edgeOriginalPaths = new Dictionary<string, List<Point2D>>();
            if (EdgeItemsSource is System.Collections.IEnumerable edgesEnumerable && movingNodeIds != null)
            {
                foreach (var item in edgesEnumerable)
                {
                    if (item is Edge edge && edge.Points != null)
                    {
                        // 如果连线的任意一端在移动集合中，保存其原始路径
                        if (movingNodeIds.Contains(edge.SourceNodeId) || movingNodeIds.Contains(edge.TargetNodeId))
                        {
                            _edgeOriginalPaths[edge.Id] = new List<Point2D>(edge.Points);
                        }
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[智能连线] 启用 - 拖动节点数: {movingNodeIds?.Count ?? 0}, 记录位置数: {_nodeInitialPositions.Count}, 保存路径数: {_edgeOriginalPaths.Count}");
        }
        
        /// <summary>
        /// 禁用智能连线更新
        /// </summary>
        public void DisableSmartEdgeUpdate()
        {
            // 🔧 智能模式下，edge.Points 已在拖动过程中实时更新为平移后的路径
            // 所以不需要清空重算，直接保持当前形状即可
            _smartEdgeUpdateEnabled = false;
            _movingNodeIds = null;
            _nodeInitialPositions = null;
            _edgeOriginalPaths = null; // 清理原始路径
            
            System.Diagnostics.Debug.WriteLine($"[智能连线] 禁用 - 保持平移后的路径形状");
        }

        private DateTime _lastEdgeRefresh = DateTime.MinValue;
        private const int EdgeRefreshThrottleMs = 16; // 约60fps

        private bool _edgeRefreshPendingDueToMissingPorts;
        
        // 批量操作标志（用于批量添加节点/连线时避免多次刷新）
        private bool _isBatchUpdating = false;
        private bool _needsRefreshAfterBatch = false;
        
        // 智能路径更新（用于批量拖动时避免重复计算A*）
        private bool _smartEdgeUpdateEnabled = false;
        private HashSet<string> _movingNodeIds = null;
        private Dictionary<string, Point2D> _nodeInitialPositions = null;
        private Dictionary<string, List<Point2D>> _edgeOriginalPaths = null; // 保存原始路径，避免累积偏移

        private void RefreshEdgesInternal(bool force)
        {
            var now = DateTime.Now;
            if (!force && (now - _lastEdgeRefresh).TotalMilliseconds < EdgeRefreshThrottleMs)
                return;
            _lastEdgeRefresh = now;

            if (_edgeLayer == null)
            {
                System.Diagnostics.Debug.WriteLine("[连线刷新] 连线层为空");
                return;
            }

            _edgeLayer.Children.Clear();

            if (EdgeItemsSource == null || ItemsSource == null)
            {
                System.Diagnostics.Debug.WriteLine($"[连线刷新] EdgeItemsSource: {EdgeItemsSource != null}, ItemsSource: {ItemsSource != null}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[连线刷新] 开始刷新，连线数量: {EdgeItemsSource.Cast<object>().Count()}");

            var nodes = ItemsSource.OfType<Node>().ToDictionary(n => n.Id, n => n);
            var primaryBrush = TryFindResource("PrimaryBrush") as Brush ?? Brushes.SteelBlue;
            var selectedBrush = TryFindResource("InfoBrush") as Brush ?? Brushes.DeepSkyBlue;

            // 预先计算所有节点的边界，用于避障
            var nodeBounds = new Dictionary<string, Rect>();
            foreach (var node in nodes.Values)
            {
                nodeBounds[node.Id] = GetNodeBounds(node);
            }

            var missingPorts = false;

            foreach (var edgeObj in EdgeItemsSource)
            {
                if (edgeObj is not Edge edge)
                    continue;

                if (string.IsNullOrWhiteSpace(edge.SourceNodeId) || string.IsNullOrWhiteSpace(edge.TargetNodeId))
                    continue;

                if (!nodes.TryGetValue(edge.SourceNodeId, out var source) ||
                    !nodes.TryGetValue(edge.TargetNodeId, out var target))
                {
                    continue;
                }

                var points = new PointCollection();

                // 🔧 智能连线更新：批量拖动时的优化处理
                bool useSmartTranslate = false;
                bool forceRecalculate = false;
                double smartOffsetX = 0, smartOffsetY = 0;
                List<Point2D> savedOriginalPath = null; // 保存找到的原始路径
                
                if (_smartEdgeUpdateEnabled && _movingNodeIds != null && _nodeInitialPositions != null)
                {
                    bool sourceInSet = _movingNodeIds.Contains(edge.SourceNodeId);
                    bool targetInSet = _movingNodeIds.Contains(edge.TargetNodeId);
                    
                    System.Diagnostics.Debug.WriteLine($"[智能连线检查] Edge: {edge.SourceNodeId} -> {edge.TargetNodeId}, 源在集合:{sourceInSet}, 目标在集合:{targetInSet}");
                    
                    if (sourceInSet && targetInSet)
                    {
                        // 两端都在拖动，相对位置不变，只需平移路径
                        // 🔧 使用保存的原始路径，避免累积偏移
                        if (_edgeOriginalPaths != null && _edgeOriginalPaths.TryGetValue(edge.Id, out var originalPath) && originalPath.Count > 2)
                        {
                            savedOriginalPath = originalPath; // 保存引用
                            
                            // 计算当前的偏移量（基于任意一个移动节点）
                            if (nodes.TryGetValue(edge.SourceNodeId, out var sourceNode) &&
                                _nodeInitialPositions.TryGetValue(edge.SourceNodeId, out var initialPos))
                            {
                                smartOffsetX = sourceNode.Position.X - initialPos.X;
                                smartOffsetY = sourceNode.Position.Y - initialPos.Y;
                                useSmartTranslate = true;
                                System.Diagnostics.Debug.WriteLine($"[智能连线] 计算偏移: 当前({sourceNode.Position.X:F2}, {sourceNode.Position.Y:F2}) - 初始({initialPos.X:F2}, {initialPos.Y:F2}) = ({smartOffsetX:F2}, {smartOffsetY:F2})");
                            }
                        }
                        else
                        {
                            // 🔧 没有原始路径或Points不足，需要先计算一次路径，后续帧才能使用智能平移
                            forceRecalculate = true;
                            System.Diagnostics.Debug.WriteLine($"[智能连线] 无原始路径，强制重算一次 - 当前: {edge.Points?.Count ?? 0}");
                        }
                    }
                    else if (sourceInSet || targetInSet)
                    {
                        // 只有一端在拖动，必须重新计算路径
                        forceRecalculate = true;
                        System.Diagnostics.Debug.WriteLine($"[智能连线] 强制重算 - 只有一端在拖动");
                    }
                }

                // 🔧 优化判断优先级
                if (useSmartTranslate && savedOriginalPath != null)
                {
                    // 🔧 智能模式：基于原始路径平移（避免累积偏移）
                    points = new PointCollection(savedOriginalPath.Select(p => new Point(p.X + smartOffsetX, p.Y + smartOffsetY)));
                    
                    // 🔧 同步更新 edge.Points，保持拖动后的路径形状
                    edge.Points = points.Select(p => new Point2D(p.X, p.Y)).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"[智能连线] ✅ 平移路径（基于原始） - 点数: {savedOriginalPath.Count}, 偏移: ({smartOffsetX:F2}, {smartOffsetY:F2})");
                }
                else if (!forceRecalculate && edge.Points != null && edge.Points.Count > 2)
                {
                    // 🔧 检查路径端点是否与当前端口位置匹配（容差5像素）
                    // 如果不匹配，说明节点位置已改变，需要重新计算
                    var startHint = new Point(edge.Points.First().X, edge.Points.First().Y);
                    var endHint = new Point(edge.Points.Last().X, edge.Points.Last().Y);
                    var currentStartPort = GetPortPoint(source, edge.SourcePortId, startHint);
                    var currentEndPort = GetPortPoint(target, edge.TargetPortId, endHint);
                    
                    bool pathIsValid = false;
                    if (currentStartPort.HasValue && currentEndPort.HasValue)
                    {
                        var startDist = Math.Sqrt(
                            Math.Pow(edge.Points.First().X - currentStartPort.Value.X, 2) +
                            Math.Pow(edge.Points.First().Y - currentStartPort.Value.Y, 2));
                        var endDist = Math.Sqrt(
                            Math.Pow(edge.Points.Last().X - currentEndPort.Value.X, 2) +
                            Math.Pow(edge.Points.Last().Y - currentEndPort.Value.Y, 2));
                        
                        // 如果端点距离小于5像素，认为路径有效
                        pathIsValid = startDist < 5 && endDist < 5;
                    }
                    
                    if (pathIsValid)
                    {
                        // 路径端点匹配，直接使用（粘贴、加载、撤销等场景）
                        points = new PointCollection(edge.Points.Select(p => new Point(p.X, p.Y)));
                        System.Diagnostics.Debug.WriteLine($"[连线优化] 直接使用已有路径 - 点数: {edge.Points.Count}");
                    }
                    else
                    {
                        // 路径端点不匹配，需要重新计算（拖动场景）
                        forceRecalculate = true;
                        System.Diagnostics.Debug.WriteLine($"[连线优化] 路径过期，强制重算 - 起点偏差: {(currentStartPort.HasValue ? Math.Sqrt(Math.Pow(edge.Points.First().X - currentStartPort.Value.X, 2) + Math.Pow(edge.Points.First().Y - currentStartPort.Value.Y, 2)) : 0):F2}");
                    }
                }
                
                if (forceRecalculate || points.Count == 0)
                {
                    // 需要计算新路径（新建连线、一端拖动、或无有效路径）
                    System.Diagnostics.Debug.WriteLine($"[连线优化] 需要计算路径 - Points: {edge.Points?.Count ?? 0}, 强制重算: {forceRecalculate}");
                    // 优先使用端口ID查找，如果没有ID则使用坐标作为提示
                    var startHint = edge.Points != null && edge.Points.Count > 0
                        ? new Point(edge.Points.First().X, edge.Points.First().Y)
                        : (Point?)null;
                    var endHint = edge.Points != null && edge.Points.Count > 0
                        ? new Point(edge.Points.Last().X, edge.Points.Last().Y)
                        : (Point?)null;

                    // 使用端口ID查找端口，如果没有ID则回退到hint查找
                    var startPort = GetPortPoint(source, edge.SourcePortId, startHint);
                    var endPort = GetPortPoint(target, edge.TargetPortId, endHint);

                    // 如果端口尚未生成（比如页面刚切换回来还未完成布局），先使用节点中心并标记稍后重刷
                    if (startPort == null)
                    {
                        missingPorts = true;
                        startPort = GetNodeCenter(source);
                    }
                    if (endPort == null)
                    {
                        missingPorts = true;
                        endPort = GetNodeCenter(target);
                    }

                    // 端口点在后续计算中必须为非空 Point
                    var startPortPoint = startPort ?? GetNodeCenter(source);
                    var endPortPoint = endPort ?? GetNodeCenter(target);

                    // 准备障碍物列表（排除源节点和目标节点）
                    var obstacles = new List<Rect>();
                    foreach (var kvp in nodeBounds)
                    {
                        // 排除源和目标节点，且必须是有效的矩形
                        if (kvp.Key != source.Id && kvp.Key != target.Id && !kvp.Value.IsEmpty && kvp.Value.Width > 1 && kvp.Value.Height > 1)
                        {
                            obstacles.Add(kvp.Value);
                        }
                    }

                    var routed = BuildOrthogonalRoute(startPortPoint, source, endPortPoint, target, obstacles);
                    points = new PointCollection(routed);

                    // 覆盖 Edge.Points 为最新路径，便于序列化/后续刷新
                    edge.Points = routed.Select(p => new Point2D(p.X, p.Y)).ToList();
                }

                var polyline = new Polyline
                {
                    Stroke = edge.IsSelected ? selectedBrush : primaryBrush,
                    StrokeThickness = edge.IsSelected ? 3 : 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Points = points,
                    Opacity = 0.9,
                    Tag = edge,
                    IsHitTestVisible = false
                };

                // 箭头
                var arrow = BuildArrow(points, edge.IsSelected ? selectedBrush : primaryBrush);

                System.Diagnostics.Debug.WriteLine($"[连线刷新] 添加连线 - 点数: {points.Count}, 起点: ({points[0].X:F2}, {points[0].Y:F2}), 终点: ({points[points.Count - 1].X:F2}, {points[points.Count - 1].Y:F2})");

                _edgeLayer.Children.Add(polyline);
                if (arrow != null)
                {
                    _edgeLayer.Children.Add(arrow);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[连线刷新] 完成刷新，绘制了 {_edgeLayer.Children.Count} 条连线");

            // 如果本次刷新时端口尚未解析成功，等待布局完成后再强制刷新一次，确保端口坐标正确
            if (missingPorts && !_edgeRefreshPendingDueToMissingPorts)
            {
                _edgeRefreshPendingDueToMissingPorts = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _edgeRefreshPendingDueToMissingPorts = false;
                    RefreshEdgesImmediate();
                }), DispatcherPriority.Loaded);
            }
        }

        #endregion

        #region 连线交互事件

        private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 备用手势：Shift + 左键且点在端口上开始连线
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                var port = FindPortFromHit(e.OriginalSource as DependencyObject);
                var nodeControl = FindParentNodeControl(port ?? e.OriginalSource as DependencyObject);
                if (port != null && nodeControl?.DataContext is Node node)
                {
                    BeginConnection(node, port);
                    e.Handled = true;
                }
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isConnecting || _connectionPreviewLine == null)
                return;

            // 获取逻辑坐标（统一的未缩放/未平移坐标系）
            var canvasPoint = GetLogicalMousePoint(e);

            // 检测附近端口并吸附
            var nearbyPort = FindNearbyPort(canvasPoint);
            if (nearbyPort != null && nearbyPort != _hoveredPort)
            {
                // 切换到新端口，更新预览终点
                _hoveredPort = nearbyPort;
                var portCenter = GetPortCenter(nearbyPort);
                if (!double.IsNaN(portCenter.X) && !double.IsNaN(portCenter.Y))
                {
                    UpdateConnectionPreview(portCenter);
                }
            }
            else if (nearbyPort == null && _hoveredPort != null)
            {
                // 离开端口区域，使用鼠标位置
                _hoveredPort = null;
                UpdateConnectionPreview(canvasPoint);
            }
            else if (nearbyPort == null)
            {
                // 没有靠近端口，使用鼠标位置
                UpdateConnectionPreview(canvasPoint);
            }
        }

        private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isConnecting)
                return;

            // 优先使用吸附的端口，如果没有则尝试从命中点查找
            var targetPort = _hoveredPort ?? FindPortFromHit(e.OriginalSource as DependencyObject);

            // 如果还是没有找到端口，尝试在鼠标位置附近查找
            if (targetPort == null)
            {
                var canvasPoint = GetLogicalMousePoint(e);
                System.Diagnostics.Debug.WriteLine($"[连线] 鼠标位置（逻辑坐标）: ({canvasPoint.X:F2}, {canvasPoint.Y:F2}), Scale={Scale:F2}, Pan=({PanX:F2},{PanY:F2})");
                targetPort = FindNearbyPort(canvasPoint);
            }

            var targetControl = FindParentNodeControl(targetPort ?? e.OriginalSource as DependencyObject);
            var targetNode = targetControl?.DataContext as Node;

            System.Diagnostics.Debug.WriteLine($"[连线] 释放鼠标 - 目标端口: {targetPort != null}, 目标节点: {targetNode?.Name ?? "null"}, 源节点: {_connectionSourceNode?.Name ?? "null"}");

            // 检查是否连接到了同一节点
            bool isSameNode = targetNode != null && _connectionSourceNode != null &&
                             (ReferenceEquals(targetNode, _connectionSourceNode) || targetNode.Id == _connectionSourceNode.Id);

            if (targetPort != null &&
                targetNode != null &&
                _connectionSourceNode != null &&
                !isSameNode)
            {
                var endPoint = GetPortCenter(targetPort);
                System.Diagnostics.Debug.WriteLine($"[连线] 端点坐标 - 起点: ({_connectionStartPoint.X:F2}, {_connectionStartPoint.Y:F2}), 终点: ({endPoint.X:F2}, {endPoint.Y:F2})");

                if (!double.IsNaN(endPoint.X) && !double.IsNaN(endPoint.Y))
                {
                    TryCreateEdge(_connectionSourceNode, targetNode, _connectionStartPoint, endPoint);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[连线] 警告：端点坐标无效");
                }
            }
            else
            {
                if (isSameNode)
                {
                    System.Diagnostics.Debug.WriteLine($"[连线] 无法创建连线：不能连接到同一节点（{_connectionSourceNode?.Name ?? "未知"}）");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[连线] 无法创建连线 - 目标端口: {targetPort != null}, 目标节点: {targetNode?.Name ?? "null"}, 源节点: {_connectionSourceNode?.Name ?? "null"}");
                }
            }

            StopConnectionPreview();
            _isConnecting = false;
            _connectionSourceNode = null;
            _hoveredPort = null;

            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// 从外部（节点端口）发起连线（必须点在端口上）
        /// </summary>
        public void BeginConnection(Node sourceNode, FrameworkElement sourcePortElement)
        {
            if (sourceNode == null || sourcePortElement == null)
                return;

            var start = GetPortCenter(sourcePortElement);
            if (double.IsNaN(start.X) || double.IsNaN(start.Y))
                return;

            // 确保源端口有稳定的 PortId（若未设置则自动生成）
            if (sourcePortElement is PortControl pc && string.IsNullOrWhiteSpace(pc.PortId))
            {
                pc.PortId = Guid.NewGuid().ToString("N");
                System.Diagnostics.Debug.WriteLine($"[连线] 为源端口自动生成 PortId: {pc.PortId}");
            }

            _isConnecting = true;
            _connectionSourceNode = sourceNode;
            _connectionSourcePortElement = sourcePortElement;  // 保存源端口元素
            _connectionStartPoint = start;
            StartConnectionPreview(_connectionStartPoint);
            CaptureMouse();
        }

        private void StartConnectionPreview(Point start)
        {
            if (_connectionPreviewLayer == null)
                return;

            _connectionPreviewLayer.Children.Clear();
            _connectionPreviewLine = new Polyline
            {
                Stroke = TryFindResource("InfoBrush") as Brush ?? Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = 0.9,
                Points = new PointCollection { start }
            };
            _connectionPreviewLayer.Children.Add(_connectionPreviewLine);
        }

        private void UpdateConnectionPreview(Point end)
        {
            if (_connectionPreviewLine == null || _connectionSourceNode == null)
                return;

            List<Point> route;

            // 如果鼠标悬停在目标端口上，使用完整的正交路由
            if (_hoveredPort != null)
            {
                var targetControl = FindParentNodeControl(_hoveredPort);
                if (targetControl?.DataContext is Node hoveredNode)
                {
                    var endPort = GetPortCenter(_hoveredPort);
                    route = BuildOrthogonalRoute(_connectionStartPoint, _connectionSourceNode, endPort, hoveredNode);
                }
                else
                {
                    // 悬停端口但找不到节点，使用简化路径
                    route = BuildSimpleOrthogonalPath(_connectionStartPoint, end, _connectionSourceNode);
                }
            }
            else
            {
                // 没有悬停端口，使用简化的L形路径到鼠标位置
                route = BuildSimpleOrthogonalPath(_connectionStartPoint, end, _connectionSourceNode);
            }

            // 更新 Polyline 的点集合
            _connectionPreviewLine.Points = new PointCollection(route);
        }

        private void StopConnectionPreview()
        {
            _connectionPreviewLayer?.Children.Clear();
            _connectionPreviewLine = null;
            _hoveredPort = null;
        }

        private void TryCreateEdge(Node source, Node target, Point startPoint, Point endPoint)
        {
            System.Diagnostics.Debug.WriteLine($"[连线] TryCreateEdge - EdgeItemsSource: {EdgeItemsSource != null}, 类型: {EdgeItemsSource?.GetType().Name ?? "null"}");

            // 如果 EdgeItemsSource 为 null，尝试从父级 FlowEditor 获取或自动创建
            if (EdgeItemsSource == null)
            {
                var flowEditor = FindParentFlowEditor(this);
                if (flowEditor != null)
                {
                    System.Diagnostics.Debug.WriteLine("[连线] 从 FlowEditor 获取 EdgeItemsSource");
                    EdgeItemsSource = flowEditor.EdgeItemsSource;

                    // 如果 FlowEditor 的也是 null，自动创建一个新的集合
                    if (flowEditor.EdgeItemsSource == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[连线] FlowEditor 的 EdgeItemsSource 也为 null，自动创建新的集合");
                        var edges = new System.Collections.ObjectModel.ObservableCollection<Edge>();
                        flowEditor.EdgeItemsSource = edges;
                        EdgeItemsSource = edges;
                    }
                }
                else
                {
                    // 如果找不到 FlowEditor，直接创建一个本地集合
                    System.Diagnostics.Debug.WriteLine("[连线] 未找到 FlowEditor，创建本地 EdgeItemsSource 集合");
                    EdgeItemsSource = new System.Collections.ObjectModel.ObservableCollection<Edge>();
                }
            }

            if (EdgeItemsSource is not System.Collections.IList list)
            {
                System.Diagnostics.Debug.WriteLine($"[连线] 错误：EdgeItemsSource 不是 IList 类型，类型: {EdgeItemsSource?.GetType().Name ?? "null"}");
                return;
            }

            // 获取源端口和目标端口的ID
            string sourcePortId = null;
            string targetPortId = null;

            if (_connectionSourcePortElement is PortControl sourcePort)
            {
                sourcePortId = sourcePort.PortId;
                System.Diagnostics.Debug.WriteLine($"[连线] 源端口ID: {sourcePortId ?? "null"}");
            }

            if (_hoveredPort is PortControl targetPort)
            {
                // 确保目标端口有稳定的 PortId（若未设置则自动生成）
                if (string.IsNullOrWhiteSpace(targetPort.PortId))
                {
                    targetPort.PortId = Guid.NewGuid().ToString("N");
                    System.Diagnostics.Debug.WriteLine($"[连线] 为目标端口自动生成 PortId: {targetPort.PortId}");
                }

                targetPortId = targetPort.PortId;
                System.Diagnostics.Debug.WriteLine($"[连线] 目标端口ID: {targetPortId ?? "null"}");
            }

            var edge = new Edge
            {
                SourceNodeId = source.Id,
                TargetNodeId = target.Id,
                SourcePortId = sourcePortId,  // 保存源端口ID
                TargetPortId = targetPortId,  // 保存目标端口ID
                Points = new List<Point2D>
                {
                    new Point2D(startPoint.X, startPoint.Y),
                    new Point2D(endPoint.X, endPoint.Y)
                }
            };

            System.Diagnostics.Debug.WriteLine($"[连线] 创建连线 - 源节点ID: {source.Id}, 目标节点ID: {target.Id}, 源端口: {sourcePortId ?? "无"}, 目标端口: {targetPortId ?? "无"}, 点数: {edge.Points.Count}");

            // 查找已存在的连线（相同源和目标节点，无论方向）
            var existingEdges = new List<object>();
            foreach (var item in list)
            {
                if (item is Edge e)
                {
                    // 检查 A->B 或 B->A
                    if ((e.SourceNodeId == source.Id && e.TargetNodeId == target.Id) ||
                        (e.SourceNodeId == target.Id && e.TargetNodeId == source.Id))
                    {
                        existingEdges.Add(e);
                    }
                }
            }

            if (_undoRedoManager != null)
            {
                var commands = new List<IUndoableCommand>();

                // 1. 如果有旧连线，先删除
                if (existingEdges.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[连线] 发现 {existingEdges.Count} 条现有连线，准备替换");
                    commands.Add(new DeleteEdgeCommand(list, existingEdges));
                }

                // 2. 添加新连线
                commands.Add(new CreateEdgeCommand(list, edge));

                System.Diagnostics.Debug.WriteLine("[连线] 使用组合命令（删除旧连线+创建新连线）");
                _undoRedoManager.Do(new CompositeCommand(commands));
            }
            else
            {
                // 不使用 UndoRedoManager，直接操作
                foreach (var oldEdge in existingEdges)
                {
                    list.Remove(oldEdge);
                }
                System.Diagnostics.Debug.WriteLine("[连线] 直接添加到集合");
                list.Add(edge);
            }

            System.Diagnostics.Debug.WriteLine($"[连线] 连线集合数量: {list.Count}");
            
            // 创建连线后立即刷新（忽略节流），确保第一次连线立即显示
            RefreshEdgesImmediate();
        }

        private bool HasEdgeBetween(string sourceId, string targetId)
        {
            if (EdgeItemsSource == null)
                return false;

            foreach (var edgeObj in EdgeItemsSource)
            {
                if (edgeObj is not Edge edge) continue;

                if (edge.SourceNodeId == sourceId && edge.TargetNodeId == targetId)
                    return true;

                // 视为双向唯一
                if (edge.SourceNodeId == targetId && edge.TargetNodeId == sourceId)
                    return true;
            }
            return false;
        }

        #endregion

        #region 端口和节点辅助方法

        private Point GetNodeCenter(Node node)
        {
            var width = node.Size.IsEmpty ? 220 : node.Size.Width;
            var height = node.Size.IsEmpty ? 40 : node.Size.Height;

            return new Point(
                node.Position.X + width / 2,
                node.Position.Y + height / 2);
        }

        private Point? GetPortPoint(Node node, string portId = null, Point? hint = null)
        {
            if (_contentCanvas == null || node == null)
                return null;

            var itemsControl = _contentCanvas.Children.OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
                return null;

            var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
            if (container == null)
                return null;

            var ports = FindPortsInContainer(container).ToList();
            if (ports.Count == 0)
                return null;

            FrameworkElement port = null;

            // 优先使用端口ID查找
            if (!string.IsNullOrEmpty(portId))
            {
                port = ports.OfType<PortControl>()
                    .FirstOrDefault(p => p.PortId == portId);

                if (port != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[端口查找] 通过ID找到端口: {portId} 在节点 {node.Name}");
                    return GetPortCenter(port);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[端口查找] 未找到ID为 {portId} 的端口在节点 {node.Name}，使用hint或默认端口");
                }
            }

            // 如果没有找到指定ID的端口，使用 hint
            if (hint.HasValue && ports.Count > 0)
            {
                port = ports
                    .OrderBy(p =>
                    {
                        var center = GetPortCenter(p);
                        return (center.X - hint.Value.X) * (center.X - hint.Value.X) +
                               (center.Y - hint.Value.Y) * (center.Y - hint.Value.Y);
                    })
                    .FirstOrDefault();
            }
            else
            {
                port = ports.FirstOrDefault();
            }

            if (port == null)
                return null;

            return GetPortCenter(port);
        }

        private FrameworkElement FindPortFromHit(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is PortControl pc)
                    return pc;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private Point GetPortCenter(FrameworkElement portElement)
        {
            if (portElement == null)
                return new Point(double.NaN, double.NaN);

            // 优先使用基于节点位置的计算（GetPortCenterByNodePosition）
            var pointByPos = GetPortCenterByNodePosition(portElement);
            if (!double.IsNaN(pointByPos.X) && !double.IsNaN(pointByPos.Y))
            {
                return pointByPos;
            }

            // 获取端口中心在端口内的相对位置
            var portCenter = new Point(portElement.ActualWidth / 2, portElement.ActualHeight / 2);

            // 回退：直接转换到 transformTarget (逻辑坐标系)
            if (_transformTarget != null)
            {
                try
                {
                    return portElement.TranslatePoint(portCenter, _transformTarget);
                }
                catch { }
            }

            // 回退：直接转换到内容画布坐标
            if (_contentCanvas != null)
            {
                try
                {
                    return portElement.TranslatePoint(portCenter, _contentCanvas);
                }
                catch { }
            }

            return new Point(double.NaN, double.NaN);
        }

        /// <summary>
        /// 通过节点位置计算端口中心（备用方法）
        /// </summary>
        private Point GetPortCenterByNodePosition(FrameworkElement portElement)
        {
            // 找到端口所属的节点
            var nodeControl = FindParentNodeControl(portElement);
            if (nodeControl == null || nodeControl.DataContext is not Node node)
                return new Point(double.NaN, double.NaN);

            // 获取节点在画布上的位置
            var itemsControl = _contentCanvas?.Children.OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
                return new Point(double.NaN, double.NaN);

            var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
            if (container == null)
                return new Point(double.NaN, double.NaN);

            var nodeX = Canvas.GetLeft(container);
            var nodeY = Canvas.GetTop(container);
            if (double.IsNaN(nodeX)) nodeX = node.Position.X;
            if (double.IsNaN(nodeY)) nodeY = node.Position.Y;

            // 获取端口相对于节点的位置
            var portCenter = new Point(portElement.ActualWidth / 2, portElement.ActualHeight / 2);
            var portInNode = portElement.TranslatePoint(portCenter, nodeControl);

            // 计算端口在画布上的绝对位置
            return new Point(nodeX + portInNode.X, nodeY + portInNode.Y);
        }

        /// <summary>
        /// 查找指定画布坐标附近最近的端口
        /// </summary>
        private FrameworkElement FindNearbyPort(Point canvasPoint)
        {
            if (ItemsSource == null || _contentCanvas == null)
                return null;

            FrameworkElement closestPort = null;
            double minDistance = (PortSnapDistance * 3) / Math.Max(Scale, 0.1);

            System.Diagnostics.Debug.WriteLine($"[端口查找] 查找附近端口，鼠标位置: ({canvasPoint.X:F2}, {canvasPoint.Y:F2}), 吸附距离: {minDistance:F2}, 缩放: {Scale:F2}");

            // 遍历所有节点，查找它们的端口
            var itemsControl = _contentCanvas.Children.OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
                return null;

            int portCount = 0;
            foreach (var node in ItemsSource.OfType<Node>())
            {
                // 跳过源节点（不能连接到自己）
                if (_connectionSourceNode != null && node.Id == _connectionSourceNode.Id)
                    continue;

                var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
                if (container == null)
                    continue;

                // 查找节点内的所有端口
                var ports = FindPortsInContainer(container);
                foreach (var port in ports)
                {
                    var portCenter = GetPortCenter(port);
                    if (double.IsNaN(portCenter.X) || double.IsNaN(portCenter.Y))
                        continue;

                    var distance = Math.Sqrt(
                        Math.Pow(canvasPoint.X - portCenter.X, 2) +
                        Math.Pow(canvasPoint.Y - portCenter.Y, 2));

                    portCount++;
                    if (portCount <= 5)  // 只输出前5个端口的信息，避免日志太多
                    {
                        System.Diagnostics.Debug.WriteLine($"[端口查找] 节点: {node.Name}, 端口中心: ({portCenter.X:F2}, {portCenter.Y:F2}), 距离: {distance:F2}");
                    }

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPort = port;
                    }
                }
            }

            if (closestPort != null)
            {
                System.Diagnostics.Debug.WriteLine($"[端口查找] 找到最近端口，最终距离: {minDistance:F2}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[端口查找] 未找到符合距离的端口，检查了 {portCount} 个端口");
            }

            return closestPort;
        }

        /// <summary>
        /// 在容器中查找所有端口控件
        /// </summary>
        private List<FrameworkElement> FindPortsInContainer(DependencyObject container)
        {
            var ports = new List<FrameworkElement>();
            if (container == null)
                return ports;

            FindPortsRecursive(container, ports);
            return ports;
        }

        /// <summary>
        /// 递归查找端口控件
        /// </summary>
        private void FindPortsRecursive(DependencyObject element, List<FrameworkElement> ports)
        {
            if (element == null)
                return;

            if (element is PortControl portControl)
            {
                ports.Add(portControl);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                FindPortsRecursive(child, ports);
            }
        }

        private NodeControl FindParentNodeControl(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current is NodeControl nc)
                    return nc;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 查找父级 FlowEditor 控件
        /// </summary>
        private FlowEditor FindParentFlowEditor(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current is FlowEditor flowEditor)
                    return flowEditor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 获取节点边界矩形（使用实际视觉尺寸）
        /// </summary>
        private Rect GetNodeBounds(Node node)
        {
            // 优先使用节点容器的实际视觉尺寸，直接转换到 transformTarget (逻辑坐标系)
            if (_transformTarget != null && _contentCanvas != null)
            {
                var itemsControl = _contentCanvas.Children.OfType<ItemsControl>().FirstOrDefault();
                if (itemsControl != null)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
                    // 如果容器已经生成，且有实际尺寸
                    if (container != null && container.ActualWidth > 0 && container.ActualHeight > 0)
                    {
                        try
                        {
                            // 确保获取的是相对于 _transformTarget 的坐标（逻辑坐标）
                            var pos = container.TranslatePoint(new Point(0, 0), _transformTarget);
                            return new Rect(
                                pos.X,
                                pos.Y,
                                container.ActualWidth,
                                container.ActualHeight
                            );
                        }
                        catch { }
                    }
                }
            }

            // 回退：使用容器的 Canvas.Left/Top 或 Node.Position
            double x = node.Position.X;
            double y = node.Position.Y;
            if (_contentCanvas != null)
            {
                var itemsControl = _contentCanvas.Children.OfType<ItemsControl>().FirstOrDefault();
                if (itemsControl != null)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
                    if (container != null)
                    {
                        var cx = Canvas.GetLeft(container);
                        var cy = Canvas.GetTop(container);
                        if (!double.IsNaN(cx)) x = cx;
                        if (!double.IsNaN(cy)) y = cy;
                    }
                }
            }

            var width = node.Size.IsEmpty ? 220 : node.Size.Width;
            var height = node.Size.IsEmpty ? 40 : node.Size.Height;

            return new Rect(x, y, width, height);
        }

        #endregion

        #region 正交路径规划

        /// <summary>
        /// 构建正交线路径：端口 -> 源外扩点 -> L 形折线/A*路径 -> 目标外扩点 -> 端口
        /// 重构版：应用障碍物膨胀 + 外延点强制离开 + 闭区间碰撞检测
        /// </summary>
        private List<Point> BuildOrthogonalRoute(Point startPort, Node source, Point endPort, Node target, List<Rect> obstacles = null)
        {
            var sourceBounds = GetNodeBounds(source);
            var targetBounds = GetNodeBounds(target);

            // 1️⃣ 膨胀起点和终点节点（使用 ObstacleMargin）
            var inflatedStart = InflateObstacle(sourceBounds);
            var inflatedEnd = InflateObstacle(targetBounds);

            // 2️⃣ 判断端口在节点的哪一边
            var sourceSide = GetPortSideByDistance(startPort, sourceBounds);
            var targetSide = GetPortSideByDistance(endPort, targetBounds);

            // 3️⃣ 计算外扩点（基于原始边界）
            var sourceOut = GetExpansionAlongSide(startPort, sourceBounds, PortExtensionDistance, sourceSide);
            var targetOut = GetExpansionAlongSide(endPort, targetBounds, PortExtensionDistance, targetSide);

            // 4️⃣ 强制外扩点离开膨胀边界（关键！）
            sourceOut = EnsureOutside(inflatedStart, sourceOut, sourceSide);
            targetOut = EnsureOutside(inflatedEnd, targetOut, targetSide);

            // 5️⃣ 构建障碍物集合：膨胀后的所有障碍 + 起点/终点
            var inflatedObstacles = new List<Rect>();
            if (obstacles != null && obstacles.Count > 0)
            {
                inflatedObstacles.AddRange(obstacles.Select(InflateObstacle));
            }
            inflatedObstacles.Add(inflatedStart);
            inflatedObstacles.Add(inflatedEnd);

            // 6️⃣ 尝试简单正交路径（从 sourceOut 到 targetOut）
            var direct = CreateSimpleOrthogonalPath(sourceOut, targetOut);

            // 7️⃣ 使用闭区间碰撞检测
            if (!PathHitObstaclesClosed(direct, inflatedObstacles))
            {
                // 直连成功，组合完整路径
                var result = CombineWithEndpoints(startPort, sourceOut, direct, targetOut, endPort, sourceSide, targetSide);
                MergeColinear(result);
                return result;
            }

            // 8️⃣ 直连失败，使用 A* 寻路（从 sourceOut 到 targetOut，避开膨胀后的障碍）
            System.Diagnostics.Debug.WriteLine($"[连线] 直连检测到碰撞，切换到 A* 寻路");
            var path = FindPathAStar(sourceOut, targetOut, inflatedObstacles, inflatedStart, inflatedEnd);

            if (path != null && path.Count > 0)
            {
                // 确保路径端点精确匹配外扩点
                if (!NearlyEqual(path[0], sourceOut))
                    path.Insert(0, sourceOut);
                else
                    path[0] = sourceOut;

                if (!NearlyEqual(path[^1], targetOut))
                    path.Add(targetOut);
                else
                    path[^1] = targetOut;

                // 组合完整路径
                var result = CombineWithEndpoints(startPort, sourceOut, path, targetOut, endPort, sourceSide, targetSide);
                MergeColinear(result);
                return result;
            }

            // 9️⃣ A* 失败，回退到简单直连（不应该发生，但作为安全回退）
            System.Diagnostics.Debug.WriteLine($"[连线] A* 寻路失败，使用简单直连作为回退");
            var fallback = CombineWithEndpoints(startPort, sourceOut, direct, targetOut, endPort, sourceSide, targetSide);
            MergeColinear(fallback);
            return fallback;
        }

        /// <summary>
        /// 构建简单的正交路径（用于预览，无目标节点）
        /// </summary>
        private List<Point> BuildSimpleOrthogonalPath(Point start, Point end, Node sourceNode)
        {
            const double margin = 18.0;
            var sourceBounds = GetNodeBounds(sourceNode);

            // 判断起始端口在节点的哪一边
            var sourceSide = GetPortSideByDistance(start, sourceBounds);

            // 计算源外扩点
            var sourceOut = GetExpansionAlongSide(start, sourceBounds, margin, sourceSide);

            // 简单的L形路径：起点 -> 外扩点 -> 转折点 -> 终点
            var route = new List<Point> { start, sourceOut };

            // 根据源端口方向选择转折方式
            if (sourceSide == PortSide.Top || sourceSide == PortSide.Bottom)
            {
                // 垂直方向的端口，先垂直后水平
                route.Add(new Point(sourceOut.X, end.Y));
            }
            else
            {
                // 水平方向的端口，先水平后垂直
                route.Add(new Point(end.X, sourceOut.Y));
            }

            route.Add(end);

            MergeColinear(route);
            return route;
        }

        #region 辅助方法：障碍物处理和几何计算

        /// <summary>
        /// 膨胀障碍物矩形（外扩 ObstacleMargin）
        /// </summary>
        private Rect InflateObstacle(Rect bounds)
        {
            return new Rect(
                bounds.Left - ObstacleMargin,
                bounds.Top - ObstacleMargin,
                bounds.Width + 2 * ObstacleMargin,
                bounds.Height + 2 * ObstacleMargin
            );
        }

        /// <summary>
        /// 确保点在膨胀矩形外部（强制离开）
        /// </summary>
        private Point EnsureOutside(Rect inflated, Point p, PortSide side)
        {
            switch (side)
            {
                case PortSide.Left:
                    if (p.X >= inflated.Left - EPS)
                        p.X = inflated.Left - SafetyOffset;
                    break;
                case PortSide.Right:
                    if (p.X <= inflated.Right + EPS)
                        p.X = inflated.Right + SafetyOffset;
                    break;
                case PortSide.Top:
                    if (p.Y >= inflated.Top - EPS)
                        p.Y = inflated.Top - SafetyOffset;
                    break;
                case PortSide.Bottom:
                    if (p.Y <= inflated.Bottom + EPS)
                        p.Y = inflated.Bottom + SafetyOffset;
                    break;
            }
            return p;
        }

        /// <summary>
        /// 闭区间碰撞检测：线段与矩形是否相交（含边界，使用 EPS 容差）
        /// </summary>
        private bool SegmentIntersectsRectClosed(Point a, Point b, Rect r)
        {
            // 水平线段
            if (Math.Abs(a.Y - b.Y) <= 0.5)
            {
                var y = a.Y;
                var minX = Math.Min(a.X, b.X);
                var maxX = Math.Max(a.X, b.X);
                return y >= r.Top - EPS && y <= r.Bottom + EPS &&
                       maxX >= r.Left - EPS && minX <= r.Right + EPS;
            }
            // 垂直线段
            else if (Math.Abs(a.X - b.X) <= 0.5)
            {
                var x = a.X;
                var minY = Math.Min(a.Y, b.Y);
                var maxY = Math.Max(a.Y, b.Y);
                return x >= r.Left - EPS && x <= r.Right + EPS &&
                       maxY >= r.Top - EPS && minY <= r.Bottom + EPS;
            }
            // 非正交线段（不应该出现）
            return false;
        }

        /// <summary>
        /// 检查路径是否与障碍物碰撞（闭区间检测）
        /// </summary>
        private bool PathHitObstaclesClosed(List<Point> pts, List<Rect> obstacles)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                foreach (var r in obstacles)
                {
                    if (SegmentIntersectsRectClosed(pts[i], pts[i + 1], r))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 点是否在任何障碍物内部（闭区间，含边界）
        /// </summary>
        private bool PointInsideAnyObstacleClosed(Point p, List<Rect> obstacles)
        {
            foreach (var r in obstacles)
            {
                if (p.X >= r.Left - EPS && p.X <= r.Right + EPS &&
                    p.Y >= r.Top - EPS && p.Y <= r.Bottom + EPS)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查两点是否接近（用于判断重复点）
        /// </summary>
        private bool NearlyEqual(Point a, Point b, double tol = 0.5)
        {
            return Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;
        }

        /// <summary>
        /// 创建简单的正交路径（直线或 L 形）
        /// </summary>
        private List<Point> CreateSimpleOrthogonalPath(Point a, Point b)
        {
            // 直线连接（水平或垂直对齐）
            if (Math.Abs(a.X - b.X) <= 0.5 || Math.Abs(a.Y - b.Y) <= 0.5)
                return new List<Point> { a, b };

            // L 形连接：两种选项，选择更优的
            var option1 = new Point(b.X, a.Y); // 先水平后垂直
            var option2 = new Point(a.X, b.Y); // 先垂直后水平

            // 简单评估：优先选择主方向上距离更长的方案
            var dx = Math.Abs(b.X - a.X);
            var dy = Math.Abs(b.Y - a.Y);

            if (dx >= dy)
                return new List<Point> { a, option1, b }; // 水平为主
            else
                return new List<Point> { a, option2, b }; // 垂直为主
        }

        /// <summary>
        /// 将端口、外扩点、中间路径组合成完整路径
        /// 新增：自动修正首尾线段的方向，确保从端口平滑引出
        /// </summary>
        private List<Point> CombineWithEndpoints(
            Point startPort, Point sourceOut, List<Point> mid, Point targetOut, Point endPort,
            PortSide sourceSide, PortSide targetSide)
        {
            var full = new List<Point>();

            // 1. 起点部分：startPort -> sourceOut
            full.Add(startPort);
            if (!NearlyEqual(startPort, sourceOut))
                full.Add(sourceOut);

            // 2. 中间部分：修正 sourceOut 连接到 mid[0] 的方向
            Point firstGridPoint;
            if (mid.Count > 0)
            {
                // mid 通常包含 sourceOut (或 Snap(sourceOut)) 和 targetOut (或 Snap(targetOut))
                // 如果 mid[0] 接近 sourceOut，则 mid[1] 是第一个真正的网格点
                // 我们需要确保 sourceOut -> mid[next] 的第一段符合 sourceSide 方向
                
                // 跳过 mid 中与 sourceOut 重合的点
                int startIndex = 0;
                while (startIndex < mid.Count && NearlyEqual(mid[startIndex], sourceOut))
                    startIndex++;

                if (startIndex < mid.Count)
                {
                    firstGridPoint = mid[startIndex];
                    
                    // 检查 sourceOut -> firstGridPoint 是否正交
                    bool isOrthogonal = Math.Abs(sourceOut.X - firstGridPoint.X) < 0.1 || Math.Abs(sourceOut.Y - firstGridPoint.Y) < 0.1;
                    
                    // 如果不正交，或者正交但方向错误（例如 Top 端口却先横向移动），强制插入拐点
                    // Top/Bottom 端口：必须先垂直移动 (Corner X = sourceOut.X)
                    // Left/Right 端口：必须先水平移动 (Corner Y = sourceOut.Y)
                    
                    bool needVerticalFirst = sourceSide == PortSide.Top || sourceSide == PortSide.Bottom;
                    bool isVertical = Math.Abs(sourceOut.X - firstGridPoint.X) < 0.1;
                    
                    if (!isOrthogonal || (needVerticalFirst != isVertical))
                    {
                        // 计算符合端口方向的拐点
                        Point corner;
                        if (needVerticalFirst)
                            corner = new Point(sourceOut.X, firstGridPoint.Y); // 先垂直
                        else
                            corner = new Point(firstGridPoint.X, sourceOut.Y); // 先水平
                            
                        full.Add(corner);
                    }
                    
                    // 添加剩余的中间点
                    // 找到终点在 mid 中的索引（跳过 targetOut）
                    int endIndex = mid.Count - 1;
                    while (endIndex >= startIndex && NearlyEqual(mid[endIndex], targetOut))
                        endIndex--;
                        
                    for (int i = startIndex; i <= endIndex; i++)
                    {
                        full.Add(mid[i]);
                    }
                }
            }

            // 3. 终点部分：修正 mid[last] -> targetOut 的方向
            if (full.Count > 0)
            {
                Point lastPoint = full[^1];
                
                // 检查 lastPoint -> targetOut
                bool isOrthogonal = Math.Abs(lastPoint.X - targetOut.X) < 0.1 || Math.Abs(lastPoint.Y - targetOut.Y) < 0.1;
                
                bool needVerticalLast = targetSide == PortSide.Top || targetSide == PortSide.Bottom;
                // 注意：Arrive Vertical 意味着线段是垂直的，即 X 坐标相同
                bool isVertical = Math.Abs(lastPoint.X - targetOut.X) < 0.1;
                
                if (!isOrthogonal || (needVerticalLast != isVertical))
                {
                    // 计算符合端口方向的拐点
                    Point corner;
                    if (needVerticalLast)
                        corner = new Point(targetOut.X, lastPoint.Y); // 最后一段垂直
                    else
                        corner = new Point(lastPoint.X, targetOut.Y); // 最后一段水平
                        
                    full.Add(corner);
                }
            }

            // 添加 targetOut 和 endPort
            if (!NearlyEqual(targetOut, endPort))
                full.Add(targetOut);
            
            if (!NearlyEqual(full[^1], endPort))
                full.Add(endPort);

            return full;
        }

        #endregion

        /// <summary>
        /// 合并共线/重复点，确保严格正交性
        /// 采用更保守的策略，避免产生斜线
        /// </summary>
        private void MergeColinear(List<Point> route)
        {
            if (route == null || route.Count < 3) return;

            // 第一步：去除重复点
            for (int i = route.Count - 2; i >= 0; i--)
            {
                if (IsSamePoint(route[i], route[i + 1]))
                {
                    route.RemoveAt(i + 1);
                }
            }

            if (route.Count < 3) return;

            // 第二步：修正非正交线段（强制对齐到水平或垂直）
            // 如果两点既不共享X也不共享Y，说明是斜线，需要插入中间点修正
            for (int i = 0; i < route.Count - 1; i++)
            {
                var p1 = route[i];
                var p2 = route[i + 1];

                bool isOrthogonal = IsSameValue(p1.X, p2.X) || IsSameValue(p1.Y, p2.Y);
                
                if (!isOrthogonal)
                {
                    // 斜线！插入中间点修正
                    // 策略：根据距离选择先走哪个方向
                    double dx = Math.Abs(p2.X - p1.X);
                    double dy = Math.Abs(p2.Y - p1.Y);
                    
                    Point mid;
                    if (dx > dy)
                    {
                        // 先水平后垂直
                        mid = new Point(p2.X, p1.Y);
                    }
                    else
                    {
                        // 先垂直后水平
                        mid = new Point(p1.X, p2.Y);
                    }
                    
                    route.Insert(i + 1, mid);
                    // 不增加 i，让下一次循环检查 p1->mid 和 mid->p2
                }
            }

            if (route.Count < 3) return;

            // 第三步：合并共线中点
            for (int i = route.Count - 2; i > 0; i--)
            {
                var p1 = route[i - 1];
                var p2 = route[i];
                var p3 = route[i + 1];

                bool colinearX = IsSameValue(p1.X, p2.X) && IsSameValue(p2.X, p3.X);
                bool colinearY = IsSameValue(p1.Y, p2.Y) && IsSameValue(p2.Y, p3.Y);
                
                if (colinearX || colinearY)
                {
                    route.RemoveAt(i);
                }
            }

            // 第四步：迭代消除短腿"狗腿"（多轮清理，逐步增大阈值）
            // 使用多轮清理策略，从小阈值到大阈值，确保彻底清除锯齿
            double[] thresholds = { 3.0, 6.0, 10.0 }; // 逐步清理 3px, 6px, 10px 以下的短腿
            
            foreach (var minSegmentLength in thresholds)
            {
                bool hasChanges = true;
                int maxIterations = 10; // 防止无限循环
                int iteration = 0;
                
                while (hasChanges && iteration < maxIterations && route.Count >= 3)
                {
                    hasChanges = false;
                    iteration++;
                    
                    for (int i = 1; i < route.Count - 1; )
                    {
                        var a = route[i - 1];
                        var b = route[i];
                        var c = route[i + 1];

                        // 检查是否为直角
                        bool isRightAngle = 
                            (IsSameValue(a.X, b.X) && IsSameValue(b.Y, c.Y)) ||
                            (IsSameValue(a.Y, b.Y) && IsSameValue(b.X, c.X));

                        if (isRightAngle)
                        {
                            double leg1 = Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);
                            double leg2 = Math.Abs(c.X - b.X) + Math.Abs(c.Y - b.Y);

                            // 只在删除后仍能保持正交时才删除
                            if ((leg1 < minSegmentLength || leg2 < minSegmentLength) &&
                                (IsSameValue(a.X, c.X) || IsSameValue(a.Y, c.Y)))
                            {
                                route.RemoveAt(i);
                                hasChanges = true;
                                continue; // 不增加 i，重新检查当前位置
                            }
                        }

                        i++;
                    }
                    
                    // 每轮迭代后重新合并共线点
                    if (hasChanges && route.Count >= 3)
                    {
                        for (int i = route.Count - 2; i > 0; i--)
                        {
                            var p1 = route[i - 1];
                            var p2 = route[i];
                            var p3 = route[i + 1];

                            bool colinearX = IsSameValue(p1.X, p2.X) && IsSameValue(p2.X, p3.X);
                            bool colinearY = IsSameValue(p1.Y, p2.Y) && IsSameValue(p2.Y, p3.Y);
                            
                            if (colinearX || colinearY)
                            {
                                route.RemoveAt(i);
                            }
                        }
                    }
                }
            }
        }

        private bool IntersectsRect(Point p1, Point p2, Rect rect)
        {
            double minX = Math.Min(p1.X, p2.X);
            double maxX = Math.Max(p1.X, p2.X);
            double minY = Math.Min(p1.Y, p2.Y);
            double maxY = Math.Max(p1.Y, p2.Y);

            if (maxX < rect.Left || minX > rect.Right || maxY < rect.Top || minY > rect.Bottom)
                return false;

            if (rect.Contains(p1) || rect.Contains(p2))
                return true;

            // 对于水平线，确保 X 轴和 Y 轴范围都有交集（包含边界）
            if (Math.Abs(p1.Y - p2.Y) < 0.1)
                return p1.Y >= rect.Top && p1.Y <= rect.Bottom &&
                       maxX >= rect.Left && minX <= rect.Right;

            // 对于垂直线，确保 Y 轴和 X 轴范围都有交集（包含边界）
            if (Math.Abs(p1.X - p2.X) < 0.1)
                return p1.X >= rect.Left && p1.X <= rect.Right &&
                       maxY >= rect.Top && minY <= rect.Bottom;

            return true;
        }

        private enum PortSide
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private PortSide GetPortSideByDistance(Point port, Rect bounds)
        {
            // 计算节点中心
            var cx = bounds.Left + bounds.Width / 2;
            var cy = bounds.Top + bounds.Height / 2;

            // 计算端口相对于中心的偏移
            var dx = port.X - cx;
            var dy = port.Y - cy;

            // 归一化到节点尺寸，避免宽高比影响判断
            var normalizedDx = dx / (bounds.Width / 2 + 0.0001); // 避免除零
            var normalizedDy = dy / (bounds.Height / 2 + 0.0001);

            // 基于归一化距离判断主方向
            if (Math.Abs(normalizedDx) > Math.Abs(normalizedDy))
            {
                // 水平方向占主导
                return normalizedDx > 0 ? PortSide.Right : PortSide.Left;
            }
            else
            {
                // 垂直方向占主导
                return normalizedDy > 0 ? PortSide.Bottom : PortSide.Top;
            }
        }

        private Point GetExpansionAlongSide(Point port, Rect bounds, double margin, PortSide side)
        {
            switch (side)
            {
                case PortSide.Top:
                    return new Point(port.X, port.Y - margin);

                case PortSide.Bottom:
                    return new Point(port.X, port.Y + margin);

                case PortSide.Left:
                    return new Point(port.X - margin, port.Y);

                case PortSide.Right:
                default:
                    return new Point(port.X + margin, port.Y);
            }
        }

        private bool IsSameValue(double a, double b) => Math.Abs(a - b) < 0.01;

        private bool IsSamePoint(Point a, Point b) => Math.Abs(a.X - b.X) < 0.01 && Math.Abs(a.Y - b.Y) < 0.01;

        /// <summary>
        /// 为折线终点构建箭头
        /// </summary>
        private Path BuildArrow(PointCollection points, Brush stroke)
        {
            if (points == null || points.Count < 2)
                return null;

            var end = points[^1];
            var prev = points[^2];

            var dx = end.X - prev.X;
            var dy = end.Y - prev.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001) return null;

            dx /= len;
            dy /= len;

            const double size = 10.0;
            var back = new Point(end.X - dx * size, end.Y - dy * size);
            var perpX = -dy;
            var perpY = dx;

            var p1 = end;
            var p2 = new Point(back.X + perpX * (size / 2), back.Y + perpY * (size / 2));
            var p3 = new Point(back.X - perpX * (size / 2), back.Y - perpY * (size / 2));

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(p1, true, true);
                ctx.LineTo(p2, true, false);
                ctx.LineTo(p3, true, false);
            }
            geo.Freeze();

            return new Path
            {
                Data = geo,
                Fill = stroke,
                Stroke = stroke,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
        }

        #endregion

        #region A*寻路算法（三级排序：折角数优先）

        /// <summary>
        /// A*寻路算法 - 优化版：折角数优先 > 距离 > 方向偏好
        /// 参考 SmartConnectionDrawer 的三级排序策略
        /// </summary>
        private List<Point> FindPathAStar(Point start, Point end, List<Rect> obstacles, Rect? sourceBounds = null, Rect? targetBounds = null)
        {
            // 网格大小（越小越精细，但计算量上升）
            double gridSize = 20.0;

            // 启发式函数：曼哈顿距离
            double Heuristic(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

            // 将点对齐到网格
            Point Snap(Point p) => new Point(Math.Round(p.X / gridSize) * gridSize, Math.Round(p.Y / gridSize) * gridSize);

            // 键生成（用于字典）
            string KeyFor(Point p) => $"{Math.Round(p.X, 3):F3}|{Math.Round(p.Y, 3):F3}";

            var startNode = Snap(start);
            var targetNode = Snap(end);

            // 点表（key -> point）
            var points = new Dictionary<string, Point>
            {
                [KeyFor(startNode)] = startNode,
                [KeyFor(targetNode)] = targetNode
            };

            // 开放集/关闭集
            var openList = new List<string> { KeyFor(startNode) };
            var closedSet = new HashSet<string>();

            // 路径与方向记录
            var cameFrom = new Dictionary<string, string>();
            var dirFrom = new Dictionary<string, int>(); // 0: 横向, 1: 纵向, -1: 未知
            dirFrom[KeyFor(startNode)] = -1;

            // gScore: (折角数, 距离, 方向偏好)
            var gScore = new Dictionary<string, (int folds, double dist, double dirPref)>
            {
                [KeyFor(startNode)] = (0, 0.0, 0.0)
            };

            // 缓存启发式
            var hCache = new Dictionary<string, double>();
            double H(string k)
            {
                if (hCache.TryGetValue(k, out var v)) return v;
                var p = points[k];
                v = Heuristic(p, targetNode);
                hCache[k] = v;
                return v;
            }

            // 方向工具
            bool SameRow(Point a, Point b) => Math.Abs(a.Y - b.Y) <= 0.5;
            int GetDir(Point from, Point to) => SameRow(from, to) ? 0 : 1;
            double DirPreference(Point from, Point to)
            {
                var idealDx = targetNode.X - from.X;
                var idealDy = targetNode.Y - from.Y;
                var actualDx = to.X - from.X;
                var actualDy = to.Y - from.Y;
                double pref = 0.0;
                if (Math.Abs(actualDx) > 0.1 && ((actualDx > 0 && idealDx > 0) || (actualDx < 0 && idealDx < 0)))
                    pref -= 0.1;
                if (Math.Abs(actualDy) > 0.1 && ((actualDy > 0 && idealDy > 0) || (actualDy < 0 && idealDy < 0)))
                    pref -= 0.1;
                return pref;
            }

            // 搜索边界（包含障碍物）
            double minX = Math.Min(startNode.X, targetNode.X);
            double maxX = Math.Max(startNode.X, targetNode.X);
            double minY = Math.Min(startNode.Y, targetNode.Y);
            double maxY = Math.Max(startNode.Y, targetNode.Y);
            foreach (var obs in obstacles)
            {
                minX = Math.Min(minX, obs.Left);
                maxX = Math.Max(maxX, obs.Right);
                minY = Math.Min(minY, obs.Top);
                maxY = Math.Max(maxY, obs.Bottom);
            }
            const double margin = 100;
            var searchBounds = new Rect(minX - margin, minY - margin, (maxX - minX) + margin * 2, (maxY - minY) + margin * 2);

            // 方向集
            var directions = new[]
            {
                new Vector(gridSize, 0),    // 右
                new Vector(-gridSize, 0),   // 左
                new Vector(0, gridSize),    // 下
                new Vector(0, -gridSize)    // 上
            };

            const double turnPenalty = 1.0; // 折角惩罚（折角数优先，惩罚可较小）
            const int maxIterations = 4000;
            int iter = 0;

            while (openList.Count > 0 && iter++ < maxIterations)
            {
                // 三级排序：折角数 -> g+h -> 方向偏好
                string currentKey = openList
                    .Where(k => !closedSet.Contains(k))
                    .OrderBy(k => gScore[k].folds)
                    .ThenBy(k => gScore[k].dist + H(k))
                    .ThenBy(k => gScore[k].dirPref)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(currentKey))
                    break;

                if (currentKey == KeyFor(targetNode))
                {
                    // 重建路径
                    var pathKeys = new List<string> { currentKey };
                    while (cameFrom.TryGetValue(currentKey, out var prev))
                    {
                        currentKey = prev;
                        pathKeys.Add(currentKey);
                    }
                    pathKeys.Reverse();

                    // 转为点并校正首尾
                    var polyline = pathKeys.Select(k => points[k]).ToList();
                    if (polyline.Count > 0)
                    {
                        polyline[0] = start;
                        polyline[^1] = end;
                    }
                    return polyline;
                }

                openList.Remove(currentKey);
                closedSet.Add(currentKey);

                var currentPoint = points[currentKey];
                var currentDir = dirFrom[currentKey];

                foreach (var dir in directions)
                {
                    var neighborPoint = new Point(currentPoint.X + dir.X, currentPoint.Y + dir.Y);
                    var neighborKey = KeyFor(neighborPoint);

                    // 边界&障碍检查
                    if (!searchBounds.Contains(neighborPoint)) continue;
                    if (PointInsideAnyObstacleClosed(neighborPoint, obstacles)) continue;

                    if (!points.ContainsKey(neighborKey))
                        points[neighborKey] = neighborPoint;

                    int nextDir = GetDir(currentPoint, neighborPoint);

                    int newFolds = gScore[currentKey].folds;
                    if (currentDir != -1 && currentDir != nextDir)
                        newFolds += 1;

                    double newDist = gScore[currentKey].dist + gridSize;
                    double newDirPref = gScore[currentKey].dirPref + DirPreference(currentPoint, neighborPoint);

                    if (!gScore.TryGetValue(neighborKey, out var prevScore))
                        prevScore = (int.MaxValue, double.PositiveInfinity, double.PositiveInfinity);

                    bool isBetter = false;
                    if (newFolds < prevScore.folds) isBetter = true;
                    else if (newFolds == prevScore.folds)
                    {
                        if (newDist + turnPenalty < prevScore.dist - 1e-6) isBetter = true;
                        else if (Math.Abs(newDist - prevScore.dist) < 1e-6 && newDirPref < prevScore.dirPref)
                            isBetter = true;
                    }

                    if (isBetter)
                    {
                        cameFrom[neighborKey] = currentKey;
                        gScore[neighborKey] = (newFolds, newDist, newDirPref);
                        dirFrom[neighborKey] = nextDir;
                        if (!openList.Contains(neighborKey))
                            openList.Add(neighborKey);
                    }
                }
            }

            // 回退：简单正交折线
            return new List<Point> { start, new Point(start.X, end.Y), end };
        }

        #endregion
    }
}

