using Astra.Core.Logs;
using Astra.Core.Nodes.Geometry;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Nodes.Models
{

    // ========================================
    // 第1层：节点数据结构（纯POCO）
    // ========================================

    /// <summary>
    /// 节点基类 - 纯数据结构，不包含执行逻辑
    /// 设计原则：
    /// 1. 单一职责：仅负责节点数据的定义和基本验证
    /// 2. 开闭原则：通过抽象方法 ExecuteCoreAsync 支持扩展
    /// 3. 里氏替换：子类可以安全替换基类
    /// </summary>
    public abstract class Node : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private bool _isSelected;
        private ExecutionResult? _lastExecutionResult;
        private NodeExecutionState _executionState;
        private string _executionTimeDisplay = "0.00 s";

        // 节点自身持有的执行计时器（运行期间每 100 ms 更新 ExecutionTimeDisplay，支持暂停/继续）
        private System.Timers.Timer? _executionTimer;
        private DateTime _executionTimerStartedAt;   // 调整后的"虚拟起点"，= UtcNow - 已累积耗时
        private TimeSpan _executionTimerAccumulated; // 暂停前已累积的耗时（用于继续时恢复进度）
        private volatile bool _executionTimerActive;

        private static readonly JsonSerializerSettings jsonCloneSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.Auto
        };

        protected Node()
        {
            Id = Guid.NewGuid().ToString();
            Parameters = new Dictionary<string, object>();
            IsEnabled = true;
            InputPorts = new List<Port>();
            OutputPorts = new List<Port>();
        }

        /// <summary>
        /// 从工具箱拖拽到画布、由节点工厂创建实例后调用一次（例如生成默认属性）。
        /// 从 JSON 反序列化或克隆得到的节点不会调用，避免覆盖已保存数据。
        /// </summary>
        public virtual void OnPlacedFromToolbox()
        {
        }

        // ===== 基本属性 =====
        
        [JsonProperty(Order = 1)]
        public string Id { get; set; }
        
        [JsonProperty(Order = 2)]
        public string NodeType { get; set; }
        
        [JsonProperty(Order = 3)]
        public string Name { get; set; }
        
        [JsonProperty(Order = 4)]
        public string Description { get; set; }
        
        [JsonProperty(Order = 5)]
        public string Icon { get; set; }
        
        [JsonProperty(Order = 6)]
        public string Color { get; set; }

        /// <summary>
        /// 设计期：本节点所属子流程（画布上的流程页）。不序列化；由 <see cref="WorkFlowNode"/> 维护。
        /// </summary>
        [JsonIgnore]
        public WorkFlowNode? ContainingWorkflow { get; set; }

        // ===== 状态属性 =====
        
        [JsonProperty(Order = 7)]
        [Display(Name = "启用", GroupName = "基础配置", Order = 1)]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                {
                    return;
                }

                _isEnabled = value;
                OnPropertyChanged();
            }
        }
        
        [JsonProperty(Order = 8)]
        public bool IsReadonly { get; set; }

        /// <summary>
        /// 节点级失败停止开关：
        /// 当工作流配置了 StopOnError=true 时，如果本节点失败且此开关为 true，
        /// 则不会终止整个工作流，允许继续执行该节点下游。
        /// </summary>
        [JsonProperty(Order = 9)]
        [Display(Name = "失败继续(失败继续测试)", GroupName = "基础配置", Order = 2)]
        public bool ContinueOnFailure { get; set; } = false;

        [JsonProperty(Order = 10)]
        public bool IsLocked { get; set; }

        /// <summary>
        /// 最后执行：常规调度阶段结束后执行；主阶段因失败提前结束时仍会执行（与 <see cref="ContinueOnFailure"/> 正交）。
        /// 属性面板通过 <see cref="WorkFlowNode"/> / <see cref="WorkflowReferenceNode"/> 上的显示元数据暴露。
        /// </summary>
        [JsonProperty(Order = 15)]
        public bool ExecuteLast { get; set; }

        /// <summary>
        /// 是否在首页「测试项」模块中展示：子流程根节点关闭则整组不展示；子节点关闭则仅隐藏对应测试项行。
        /// </summary>
        [JsonProperty(Order = 16)]
        [Display(Name = "在主页测试项中显示", GroupName = "基础配置", Order = 1, Description = "关闭后不在首页测试项模块中展示。")]
        public bool ShowInHomeTestItems { get; set; } = true;

        /// <summary>
        /// 关闭后：该节点的单值/曲线判定行及本节点发布到测试总线的图表类产物不写入 HTML/PDF 测试报告（原始 TDMS/WAV 等归档不受影响）。
        /// </summary>
        [JsonProperty(Order = 17)]
        [Display(Name = "纳入测试报告", GroupName = "基础配置", Order = 3,
            Description = "关闭后该节点结果不出现在测试报告（图表与单值/曲线判定）；归档原始数据仍可按策略导出。")]
        public bool IncludeInTestReport { get; set; } = true;

        /// <summary>
        /// 节点是否被选中（用于 UI 框选等交互）
        /// </summary>
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        // ===== 参数和结果 =====
        
        [JsonProperty(Order = 10)]
        public Dictionary<string, object> Parameters { get; set; }

        [JsonIgnore]
        public ExecutionResult? LastExecutionResult
        {
            get => _lastExecutionResult;
            set
            {
                if (ReferenceEquals(_lastExecutionResult, value))
                {
                    return;
                }

                _lastExecutionResult = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 用于 UI 实时展示的执行耗时字符串，执行期间由定时器定期更新。
        /// </summary>
        [JsonIgnore]
        public string ExecutionTimeDisplay
        {
            get => _executionTimeDisplay;
            set
            {
                if (string.Equals(_executionTimeDisplay, value, StringComparison.Ordinal))
                    return;
                _executionTimeDisplay = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public NodeExecutionState ExecutionState
        {
            get => _executionState;
            set
            {
                if (_executionState == value)
                    return;

                var previous = _executionState;
                _executionState = value;
                OnPropertyChanged();

                HandleExecutionTimerStateChange(previous, value);
            }
        }

        // ── 节点内置执行计时器（支持暂停/继续）──────────────────────────────────────

        private void HandleExecutionTimerStateChange(NodeExecutionState previous, NodeExecutionState current)
        {
            if (current == NodeExecutionState.Running)
            {
                if (previous == NodeExecutionState.Paused)
                    ResumeExecutionTimer();   // 继续：在已累积耗时基础上接着计时
                else
                    StartExecutionTimer();    // 全新开始：重置累积量
            }
            else if (current == NodeExecutionState.Paused)
            {
                PauseExecutionTimer();        // 暂停：保存已耗时，停止计时器
            }
            else
            {
                StopExecutionTimer(current);  // 终态 / Idle：停止并写入最终耗时
            }
        }

        private void StartExecutionTimer()
        {
            DisposeExecutionTimer();
            _executionTimerAccumulated = TimeSpan.Zero;
            _executionTimerStartedAt = DateTime.UtcNow;
            _executionTimerActive = true;
            ExecutionTimeDisplay = "0.00 s";
            CreateAndStartTimer();
        }

        private void PauseExecutionTimer()
        {
            _executionTimerActive = false;
            // 累积到暂停时刻的耗时（_executionTimer 不为 null 说明计时器曾经启动过）
            if (_executionTimer != null)
                _executionTimerAccumulated += DateTime.UtcNow - _executionTimerStartedAt;
            DisposeExecutionTimer();
            // 保持当前 ExecutionTimeDisplay 不变，让用户看到暂停时刻的计时值
        }

        private void ResumeExecutionTimer()
        {
            DisposeExecutionTimer();
            // 把"虚拟起点"往回拨，使得 (UtcNow - StartedAt) == 已累积耗时 + 继续后的耗时
            _executionTimerStartedAt = DateTime.UtcNow - _executionTimerAccumulated;
            _executionTimerActive = true;
            CreateAndStartTimer();
        }

        private void StopExecutionTimer(NodeExecutionState newState)
        {
            _executionTimerActive = false;
            DisposeExecutionTimer();

            if (newState == NodeExecutionState.Idle)
            {
                _executionTimerAccumulated = TimeSpan.Zero;
                ExecutionTimeDisplay = "0.00 s";
            }
            else
            {
                // 优先使用引擎写回的精确耗时（LastExecutionResult 在 ExecutionState 变化前已被引擎赋值）
                var ms = _lastExecutionResult?.ActiveDurationMs
                         ?? _lastExecutionResult?.Duration?.TotalMilliseconds;
                if (ms.HasValue)
                    ExecutionTimeDisplay = $"{ms.Value / 1000d:F2} s";
                // 否则保留计时器最后一次更新的值
            }
        }

        private void CreateAndStartTimer()
        {
            var timer = new System.Timers.Timer(100) { AutoReset = true };
            timer.Elapsed += OnExecutionTimerElapsed;
            _executionTimer = timer;
            timer.Start();
        }

        private void DisposeExecutionTimer()
        {
            var timer = System.Threading.Interlocked.Exchange(ref _executionTimer, null);
            if (timer == null) return;
            timer.Stop();
            timer.Elapsed -= OnExecutionTimerElapsed;
            timer.Dispose();
        }

        private void OnExecutionTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_executionTimerActive) return;
            // _executionTimerStartedAt 已修正为"虚拟起点"，直接减就得到包含暂停前累积量的总耗时
            var elapsedMs = (DateTime.UtcNow - _executionTimerStartedAt).TotalMilliseconds;
            ExecutionTimeDisplay = $"{elapsedMs / 1000d:F2} s";
        }

        // ===== 端口集合 =====
        
        [JsonProperty(Order = 11)]
        public List<Port> InputPorts { get; set; }
        
        [JsonProperty(Order = 12)]
        public List<Port> OutputPorts { get; set; }

        // ===== 布局属性 =====
        
        [JsonProperty(Order = 13)]
        public Point2D Position { get; set; }
        
        [JsonProperty(Order = 14)]
        public Size2D Size { get; set; }

        // ===== 端口管理方法（符合单一职责原则） =====

        /// <summary>
        /// 添加输入端口
        /// </summary>
        public virtual void AddInputPort(Port port)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            
            port.NodeId = this.Id;
            port.ParentNode = this;
           
            if (!InputPorts.Any(p => p.Id == port.Id))
            {
                InputPorts.Add(port);
            }
        }

        /// <summary>
        /// 添加输出端口
        /// </summary>
        public virtual void AddOutputPort(Port port)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            
            port.NodeId = this.Id;
            port.ParentNode = this;
            
            if (!OutputPorts.Any(p => p.Id == port.Id))
            {
                OutputPorts.Add(port);
            }
        }

        /// <summary>
        /// 移除端口
        /// </summary>
        public virtual bool RemovePort(string portId)
        {
            var removed = InputPorts.RemoveAll(p => p.Id == portId);
            removed += OutputPorts.RemoveAll(p => p.Id == portId);
            return removed > 0;
        }

        /// <summary>
        /// 获取端口
        /// </summary>
        public virtual Port GetPort(string portId)
        {
            return InputPorts.FirstOrDefault(p => p.Id == portId) 
                   ?? OutputPorts.FirstOrDefault(p => p.Id == portId);
        }

        /// <summary>
        /// 获取所有端口
        /// </summary>
        public virtual IEnumerable<Port> GetAllPorts()
        {
            return InputPorts.Concat(OutputPorts);
        }

        // ===== 连线事件回调 =====

        /// <summary>
        /// 当有新连线连接到本节点时调用（无论本节点是源端还是目标端）。
        /// 子类可覆盖以响应连线变化，例如刷新可选数据源。
        /// </summary>
        public virtual void OnConnectionAttached(Edge edge, Node? sourceNode, Node? targetNode) { }

        /// <summary>
        /// 当与本节点相关的连线被移除时调用（无论本节点是源端还是目标端）。
        /// <paramref name="edge"/> 为 <c>null</c> 时表示 Reset（全部连线被清除）。
        /// </summary>
        public virtual void OnConnectionDetached(Edge? edge, Node? sourceNode, Node? targetNode) { }

        /// <summary>
        /// 当节点被从工作流画布中移除时调用。
        /// 子类可覆盖以清理静态注册表、释放资源等。
        /// </summary>
        public virtual void OnRemovedFromWorkflow() { }

        // ===== 核心方法：定义节点的业务逻辑（不包含执行基础设施） =====

        /// <summary>
        /// 节点的核心业务逻辑（由子类实现）
        /// 不需要处理日志、异常捕获、重试等基础设施逻辑
        /// </summary>
        protected abstract Task<ExecutionResult> ExecuteCoreAsync(
            NodeContext context,
            CancellationToken cancellationToken);

        /// <summary>
        /// 执行入口 - 供执行器调用
        /// 此方法由执行引擎调用，执行节点的核心逻辑（ExecuteCoreAsync）
        /// </summary>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public Task<ExecutionResult> InvokeExecuteCoreAsync(
            NodeContext context,
            CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(context, cancellationToken);
        }

        /// <summary>
        /// 验证节点配置
        /// 符合开闭原则：子类可以重写扩展验证逻辑
        /// </summary>
        public virtual ValidationResult Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Id))
                errors.Add("节点ID不能为空");

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("节点名称不能为空");

            if (string.IsNullOrWhiteSpace(NodeType))
                errors.Add("节点类型不能为空");

            // 验证端口
            foreach (var port in GetAllPorts())
            {
                if (string.IsNullOrWhiteSpace(port.Name))
                    errors.Add($"端口名称不能为空");
                
                if (port.NodeId != this.Id)
                    errors.Add($"端口 {port.Name} 的所属节点ID不匹配");
            }

            return errors.Any()
                ? ValidationResult.Failure(errors.ToArray())
                : ValidationResult.Success();
        }

        /// <summary>
        /// 克隆节点（默认实现：基于 Newtonsoft.Json 的序列化/反序列化深拷贝）
        /// 符合开闭原则：提供默认实现，子类可重写定制
        /// </summary>
        public virtual Node Clone()
        {
            // 保存只读结构体属性（JSON 反序列化无法设置只读属性）
            var originalPosition = this.Position;
            var originalSize = this.Size;
            
            var json = JsonConvert.SerializeObject(this, GetType(), jsonCloneSettings);
            var cloned = (Node)JsonConvert.DeserializeObject(json, GetType(), jsonCloneSettings);
            cloned.Id = Guid.NewGuid().ToString();
            
            // 手动恢复只读结构体属性
            cloned.Position = originalPosition;
            cloned.Size = originalSize;
            
            RebuildPortRelationships(cloned);
            
            AfterClone(cloned);
            return cloned;
        }

        /// <summary>
        /// 重建端口关系（克隆后的后处理）
        /// 符合单一职责原则：专门负责关系重建
        /// </summary>
        protected virtual void RebuildPortRelationships(Node cloned)
        {
            foreach (var port in cloned.InputPorts ?? new List<Port>())
            {
                port.Id = Guid.NewGuid().ToString();
                port.NodeId = cloned.Id;
                port.ParentNode = cloned;
                port.Connections?.Clear();
            }
            
            foreach (var port in cloned.OutputPorts ?? new List<Port>())
            {
                port.Id = Guid.NewGuid().ToString();
                port.NodeId = cloned.Id;
                port.ParentNode = cloned;
                port.Connections?.Clear();
            }
        }

        /// <summary>
        /// 克隆后钩子：子类可覆盖以执行深拷贝或修正引用
        /// 符合开闭原则：提供扩展点
        /// </summary>
        protected virtual void AfterClone(Node cloned)
        {
        }

        /// <summary>
        /// 保留，供子类在 JSON 克隆后进行统一后处理（如 ID 重映射、关系修补）。
        /// </summary>
        // 下面保留的类型克隆计划结构已不再被默认实现使用，若未来需要切换为"表达式委托 + 缓存"的方案，可恢复使用。

        // ===== 执行入口（通过扩展方法提供，在 Astra.Engine 中实现） =====
        // 注意：ExecuteAsync 方法已移至 Astra.Engine.Execution.NodeExecutor.NodeExecutionExtensions
        // 这样可以避免 Core 直接依赖 Engine 的实现

        /// <summary>
        /// 设计期：在节点标题上自动维护「 - 设备/通道…」后缀。
        /// <paramref name="trackedAutoSuffix"/> 为当前自动追加的片段（不含「 - 」），用于下次更新前从 <see cref="Name"/> 中剥离。
        /// </summary>
        protected void ApplyAutoChannelSuffixToDisplayName(ref string? trackedAutoSuffix, string newSuffixFragment)
        {
            var newS = newSuffixFragment ?? "";
            var baseName = NodeNameChannelSuffixHelper.StripTrackedSuffix(Name, trackedAutoSuffix);
            var composed = NodeNameChannelSuffixHelper.ComposeWithAutoSuffix(baseName, newS);
            trackedAutoSuffix = string.IsNullOrEmpty(newS) ? null : newS;
            if (string.Equals(Name, composed, StringComparison.Ordinal))
                return;
            Name = composed;
            OnPropertyChanged(nameof(Name));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ========================================
    // 第2层：节点执行器接口（接口定义保留在 Core，实现移至 Engine）
    // ========================================

    /// <summary>
    /// 节点执行器接口
    /// 符合单一职责：仅负责节点执行
    /// 符合依赖倒置：高层模块依赖抽象而非具体实现
    /// </summary>
    public interface INodeExecutor
    {
        Task<ExecutionResult> ExecuteAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken);

        INodeExecutor Use(INodeMiddleware middleware);

        INodeExecutor AddInterceptor(INodeInterceptor interceptor);
    }

    /// <summary>
    /// 节点中间件接口
    /// 符合单一职责：每个中间件处理一个横切关注点
    /// </summary>
    public interface INodeMiddleware
    {
        Task<ExecutionResult> InvokeAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<ExecutionResult>> next);
    }

    /// <summary>
    /// 节点拦截器接口
    /// 符合接口隔离原则：客户端只依赖需要的方法
    /// </summary>
    public interface INodeInterceptor
    {
        Task OnBeforeExecuteAsync(Node node, NodeContext context, CancellationToken cancellationToken);

        Task OnAfterExecuteAsync(Node node, ExecutionResult result, CancellationToken cancellationToken);

        Task OnExceptionAsync(Node node, Exception exception, CancellationToken cancellationToken);
    }

    // 注意：所有实现类（DefaultNodeExecutor, 各种Middleware, 各种Interceptor等）
    // 已迁移至 Astra.Engine.Execution 命名空间
    // 请使用 using Astra.Engine.Execution.NodeExecutor;
    //      using Astra.Engine.Execution.Middleware;
    //      using Astra.Engine.Execution.Interceptors;
    // 来引用这些实现类

    // ========================================
    // 批量克隆扩展方法
    // ========================================

    /// <summary>
    /// Node 批量克隆扩展，支持批量复制、批量粘贴等场景
    /// </summary>
    public static class NodeCloneExtensions
    {
        /// <summary>
        /// 强类型克隆
        /// </summary>
        /// <typeparam name="T">节点类型</typeparam>
        /// <param name="node">要克隆的节点</param>
        /// <returns>克隆后的节点实例</returns>
        public static T CloneAs<T>(this T node) where T : Node
        {
            return (T)node.Clone();
        }

        /// <summary>
        /// 克隆并修改
        /// </summary>
        /// <typeparam name="T">节点类型</typeparam>
        /// <param name="node">要克隆的节点</param>
        /// <param name="configure">配置委托</param>
        /// <returns>克隆并修改后的节点实例</returns>
        public static T CloneWith<T>(this T node, Action<T> configure) where T : Node
        {
            var cloned = node.CloneAs<T>();
            configure(cloned);
            return cloned;
        }

        /// <summary>
        /// 批量克隆
        /// </summary>
        /// <typeparam name="T">节点类型</typeparam>
        /// <param name="nodes">要克隆的节点集合</param>
        /// <returns>克隆后的节点集合</returns>
        public static List<T> CloneAll<T>(this IEnumerable<T> nodes) where T : Node
        {
            return nodes.Select(n => n.CloneAs<T>()).ToList();
        }
    }

    // ========================================
    // 注意：所有执行相关的实现类已迁移至 Astra.Engine
    // ========================================
    // 以下类已迁移：
    // - DefaultNodeExecutor -> Astra.Engine.Execution.NodeExecutor.DefaultNodeExecutor
    // - NodeExecutorFactory -> Astra.Engine.Execution.NodeExecutor.NodeExecutorFactory
    // - LoggingMiddleware -> Astra.Engine.Execution.Middleware.LoggingMiddleware
    // - PerformanceMiddleware -> Astra.Engine.Execution.Middleware.PerformanceMiddleware
    // - RetryMiddleware -> Astra.Engine.Execution.Middleware.RetryMiddleware
    // - TimeoutMiddleware -> Astra.Engine.Execution.Middleware.TimeoutMiddleware
    // - CacheMiddleware -> Astra.Engine.Execution.Middleware.CacheMiddleware
    // - ValidationMiddleware -> Astra.Engine.Execution.Middleware.ValidationMiddleware
    // - ConditionalMiddleware -> Astra.Engine.Execution.Middleware.ConditionalMiddleware
    // - AuditInterceptor -> Astra.Engine.Execution.Interceptors.AuditInterceptor
    // - PermissionInterceptor -> Astra.Engine.Execution.Interceptors.PermissionInterceptor
    // 
    // 使用方式：
    // using Astra.Engine.Execution.NodeExecutor;  // 使用 NodeExecutionExtensions.ExecuteAsync()
    // using Astra.Engine.Execution.NodeExecutor;  // 使用 DefaultNodeExecutor, NodeExecutorFactory
    // using Astra.Engine.Execution.Middleware;    // 使用各种中间件
    // using Astra.Engine.Execution.Interceptors; // 使用各种拦截器
    // ========================================
}
