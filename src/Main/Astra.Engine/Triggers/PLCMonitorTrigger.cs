using Astra.Core.Triggers;
using Astra.Core.Triggers.Args;
using Astra.Core.Triggers.Configuration;
using Astra.Core.Triggers.Enums;
using Astra.Core.Triggers.Manager;
using Astra.Core.Triggers.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Astra.Engine.Triggers
{
    #region ========== 具体触发器实现 ==========

    #region 1. 手动扫码触发器

    /// <summary>
    /// 手动扫码触发器（事件驱动型）
    /// </summary>
    public class ManualScanTrigger : TriggerBase
    {
        public override string TriggerName => "手动扫码触发器";
        protected override TriggerWorkType WorkType => TriggerWorkType.EventDriven;

        /// <summary>
        /// 手动触发测试
        /// </summary>
        public async Task TriggerTestAsync(string sn, Dictionary<string, object> additionalData = null)
        {
            if (!IsRunning)
            {
                Console.WriteLine($"[{TriggerName}] ⚠ 触发器未运行");
                return;
            }

            var data = additionalData ?? new Dictionary<string, object>();
            data["InputMethod"] = "Manual";
            data["InputTime"] = DateTime.Now;

            // 【关键】调用父类的 RaiseTriggerAsync 方法
            await RaiseTriggerAsync(TriggerSource.ManualScan, sn, data);
        }
    }

    #endregion

    #region 2. 扫码枪设备接口和实现

    /// <summary>
    /// 扫码枪设备接口
    /// </summary>
    public interface IScannerDevice
    {
        event EventHandler<ScanDataEventArgs> OnDataReceived;
        Task StartAsync();
        Task StopAsync();
        string DeviceId { get; }
        string DeviceName { get; }
        bool IsConnected { get; }
    }

    /// <summary>
    /// 扫码数据事件参数
    /// </summary>
    public class ScanDataEventArgs : EventArgs
    {
        public string Data { get; set; }
        public DateTime ScanTime { get; set; }
        public string ScannerDeviceId { get; set; }

        public ScanDataEventArgs(string data, string deviceId)
        {
            Data = data;
            ScannerDeviceId = deviceId;
            ScanTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 基恩士扫码枪实现
    /// </summary>
    public class KeyenceScanner : IScannerDevice
    {
        public event EventHandler<ScanDataEventArgs> OnDataReceived;
        public string DeviceId { get; }
        public string DeviceName { get; }
        public bool IsConnected { get; private set; }

        private readonly string _ipAddress;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;

        public KeyenceScanner(string deviceId, string deviceName, string ipAddress, int port = 9004)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            _ipAddress = ipAddress;
            _port = port;
        }

        public async Task StartAsync()
        {
            Console.WriteLine($"[{DeviceName}] 连接到 {_ipAddress}:{_port}...");

            try
            {
                // 实际应用中这里是真实的TCP连接
                // _client = new TcpClient();
                // await _client.ConnectAsync(_ipAddress, _port);
                // _stream = _client.GetStream();

                // 模拟连接
                await Task.Delay(500);
                IsConnected = true;

                Console.WriteLine($"[{DeviceName}] ✓ 已连接");

                // 启动数据接收线程
                _cts = new CancellationTokenSource();
                _ = ReceiveDataAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DeviceName}] ✗ 连接失败: {ex.Message}");
                IsConnected = false;
            }
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();

            // _stream?.Close();
            // _client?.Close();

            await Task.Delay(100);
            IsConnected = false;
            Console.WriteLine($"[{DeviceName}] 已断开");
        }

        private async Task ReceiveDataAsync(CancellationToken cancellationToken)
        {
            // 实际应用中这里是从 TCP/串口 读取数据的循环
            /*
            var buffer = new byte[1024];
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        var data = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        OnDataReceived?.Invoke(this, new ScanDataEventArgs(data, DeviceId));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DeviceName}] 接收数据异常: {ex.Message}");
                    break;
                }
            }
            */

            // 模拟接收
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// 模拟扫码（用于测试）
        /// </summary>
        public void SimulateScan(string data)
        {
            if (!IsConnected) return;

            Console.WriteLine($"[{DeviceName}] 📷 扫描到: {data}");
            OnDataReceived?.Invoke(this, new ScanDataEventArgs(data, DeviceId));
        }
    }

    #endregion

    #region 3. 扫码枪触发器

    /// <summary>
    /// 扫码枪触发器（事件驱动型）
    /// </summary>
    public class ScannerTrigger : TriggerBase
    {
        private readonly IScannerDevice _scanner;

        public override string TriggerName => $"扫码触发器-{_scanner.DeviceName}";
        protected override TriggerWorkType WorkType => TriggerWorkType.EventDriven;

        public ScannerTrigger(IScannerDevice scanner)
        {
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        }

        protected override async Task<bool> OnBeforeStartAsync()
        {
            if (!_scanner.IsConnected)
            {
                await _scanner.StartAsync();
            }
            return true;
        }

        protected override async Task OnBeforeStopAsync()
        {
            await _scanner.StopAsync();
        }

        /// <summary>
        /// 【初始化】订阅扫码枪事件
        /// </summary>
        protected override Task InitializeEventDrivenAsync(CancellationToken cancellationToken)
        {
            // 订阅扫码枪的数据接收事件
            _scanner.OnDataReceived += Scanner_OnDataReceived;

            Console.WriteLine($"[{TriggerName}] 已订阅扫码枪事件");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 扫码枪事件处理器
        /// </summary>
        private async void Scanner_OnDataReceived(object sender, ScanDataEventArgs e)
        {
            if (!IsRunning) return;

            var additionalData = new Dictionary<string, object>
        {
            { "ScannerDeviceId", e.ScannerDeviceId },
            { "ScannerName", _scanner.DeviceName },
            { "ScanTime", e.ScanTime }
        };

            // 【关键】调用父类的 RaiseTriggerAsync 方法
            await RaiseTriggerAsync(TriggerSource.AutoScan, e.Data, additionalData);
        }
    }

    #endregion

    #region 4. PLC监控触发器

    /// <summary>
    /// PLC连接器接口
    /// </summary>
    public interface IPLCConnector
    {
        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        Task<bool> ReadBoolAsync(string address);
        Task WriteBoolAsync(string address, bool value);
        bool IsConnected { get; }
    }

    /// <summary>
    /// 西门子PLC连接器（S7协议）
    /// </summary>
    public class SiemensPLCConnector : IPLCConnector
    {
        private readonly string _ipAddress;
        private readonly int _rack;
        private readonly int _slot;

        public bool IsConnected { get; private set; }

        public SiemensPLCConnector(string ipAddress, int rack = 0, int slot = 1)
        {
            _ipAddress = ipAddress;
            _rack = rack;
            _slot = slot;
        }

        public async Task<bool> ConnectAsync()
        {
            Console.WriteLine($"[PLC] 连接到 {_ipAddress} (Rack:{_rack}, Slot:{_slot})...");

            // 实际应用中使用 S7.Net 库
            // var plc = new S7.Net.Plc(CpuType.S71200, _ipAddress, _rack, _slot);
            // plc.Open();

            await Task.Delay(500);
            IsConnected = true;

            Console.WriteLine("[PLC] ✓ 已连接");
            return true;
        }

        public async Task DisconnectAsync()
        {
            // plc?.Close();
            await Task.Delay(100);
            IsConnected = false;
            Console.WriteLine("[PLC] 已断开");
        }

        public async Task<bool> ReadBoolAsync(string address)
        {
            if (!IsConnected) return false;

            // 实际代码：return (bool)plc.Read(address);
            await Task.Delay(10);
            return false; // 模拟返回
        }

        public async Task WriteBoolAsync(string address, bool value)
        {
            if (!IsConnected) return;

            // 实际代码：plc.Write(address, value);
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// PLC监控触发器（轮询型）
    /// </summary>
    public class PLCMonitorTrigger : TriggerBase
    {
        private readonly IPLCConnector _plc;
        private readonly string _monitorAddress;
        private readonly string _snAddress;
        private bool _lastState;

        public override string TriggerName => $"PLC监控触发器-{_monitorAddress}";
        protected override TriggerWorkType WorkType => TriggerWorkType.Polling;
        protected override int PollIntervalMs => 50; // PLC快速轮询

        public PLCMonitorTrigger(IPLCConnector plc, string monitorAddress, string snAddress = null)
        {
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _monitorAddress = monitorAddress;
            _snAddress = snAddress;
            _lastState = false;
        }

        protected override async Task<bool> OnBeforeStartAsync()
        {
            if (!_plc.IsConnected)
            {
                return await _plc.ConnectAsync();
            }
            return true;
        }

        protected override async Task OnBeforeStopAsync()
        {
            await _plc.DisconnectAsync();
        }

        /// <summary>
        /// 【核心逻辑】只需要检查PLC状态，返回触发结果
        /// </summary>
        protected override async Task<TriggerResult> CheckTriggerAsync()
        {
            try
            {
                // 读取监控位
                bool currentState = await _plc.ReadBoolAsync(_monitorAddress);

                // 检测上升沿（false → true）
                if (currentState && !_lastState)
                {
                    _lastState = currentState;

                    // 读取SN（如果配置了）
                    string sn = "PLC_TRIGGER";
                    if (!string.IsNullOrEmpty(_snAddress))
                    {
                        // sn = await _plc.ReadStringAsync(_snAddress);
                    }

                    var data = new Dictionary<string, object>
                {
                    { "PLCAddress", _monitorAddress },
                    { "TriggerEdge", "Rising" }
                };

                    // 返回触发结果（父类会自动处理）
                    return TriggerResult.TriggeredWithSN(TriggerSource.PLCMonitor, sn, data);
                }

                _lastState = currentState;

                // 未触发
                return TriggerResult.NotTriggered();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{TriggerName}] PLC读取异常: {ex.Message}");
                return TriggerResult.NotTriggered();
            }
        }
    }

    #endregion

    #region 5. 网络API触发器

    /// <summary>
    /// 网络API触发器（事件驱动型）
    /// </summary>
    public class NetworkAPITrigger : TriggerBase
    {
        private readonly int _port;
        private HttpListener _listener;

        public override string TriggerName => $"网络API触发器-{_port}";
        protected override TriggerWorkType WorkType => TriggerWorkType.EventDriven;

        public NetworkAPITrigger(int port = 8080)
        {
            _port = port;
        }

        protected override async Task<bool> OnBeforeStartAsync()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{_port}/trigger/");
                _listener.Start();

                Console.WriteLine($"[{TriggerName}] HTTP服务已启动: http://localhost:{_port}/trigger/");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{TriggerName}] ✗ HTTP服务启动失败: {ex.Message}");
                return false;
            }
        }

        protected override async Task OnBeforeStopAsync()
        {
            _listener?.Stop();
            _listener?.Close();
        }

        /// <summary>
        /// 【初始化】启动HTTP监听循环
        /// </summary>
        protected override async Task InitializeEventDrivenAsync(CancellationToken cancellationToken)
        {
            // 启动异步监听循环
            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();
                        _ = HandleRequestAsync(context); // Fire-and-forget
                    }
                    catch (Exception ex)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Console.WriteLine($"[{TriggerName}] HTTP请求处理异常: {ex.Message}");
                        }
                    }
                }
            }, cancellationToken);
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var sn = request.QueryString["sn"];
                if (string.IsNullOrEmpty(sn))
                {
                    response.StatusCode = 400;
                    var errorBytes = Encoding.UTF8.GetBytes("{\"error\":\"Missing SN parameter\"}");
                    await response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                    response.Close();
                    return;
                }

                var data = new Dictionary<string, object>
            {
                { "RemoteAddress", request.RemoteEndPoint?.Address.ToString() },
                { "UserAgent", request.UserAgent }
            };

                // 【关键】触发测试
                await RaiseTriggerAsync(TriggerSource.NetworkAPI, sn, data);

                // 返回成功
                response.StatusCode = 200;
                var successBytes = Encoding.UTF8.GetBytes("{\"success\":true}");
                await response.OutputStream.WriteAsync(successBytes, 0, successBytes.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{TriggerName}] 请求处理异常: {ex.Message}");
                response.StatusCode = 500;
            }
            finally
            {
                response.Close();
            }
        }
    }

    #endregion

    #region 6. 定时触发器
    /// <summary>
    /// 定时触发器（轮询型）
    /// </summary>
    public class TimerTrigger : TriggerBase
    {
        private readonly int _intervalMs;
        private int _triggerCounter;
        private DateTime _lastTriggerTime;

        public override string TriggerName => $"定时触发器-{_intervalMs}ms";
        protected override TriggerWorkType WorkType => TriggerWorkType.Polling;
        protected override int PollIntervalMs => _intervalMs;

        public TimerTrigger(int intervalMs)
        {
            _intervalMs = intervalMs;
            _triggerCounter = 0;
            _lastTriggerTime = DateTime.MinValue;
        }

        /// <summary>
        /// 【核心逻辑】每次被调用时都返回触发结果
        /// </summary>
        protected override Task<TriggerResult> CheckTriggerAsync()
        {
            _triggerCounter++;

            var data = new Dictionary<string, object>
        {
            { "TriggerCounter", _triggerCounter },
            { "LastTriggerTime", _lastTriggerTime }
        };

            _lastTriggerTime = DateTime.Now;

            // 定时器每次都触发
            return Task.FromResult(
                TriggerResult.TriggeredWithSN(
                    TriggerSource.Timer,
                    $"TIMER_{_triggerCounter:D6}",
                    data
                )
            );
        }
    }

    #endregion

    #endregion

    #region ========== 测试流程实现示例 ==========

    /// <summary>
    /// 产品测试流程
    /// </summary>
    public class ProductTestProcess : ITriggerObserver
    {
        private readonly string _name;
        private readonly int _durationMs;

        public ProductTestProcess(string name, int durationMs)
        {
            _name = name;
            _durationMs = durationMs;
        }

        public async Task HandleTriggerAsync(TriggerEventArgs args)
        {
            Console.WriteLine($"\n[{_name}] ▶ 开始测试");
            Console.WriteLine($"  SN: {args.GetSN()}");
            Console.WriteLine($"  触发器ID: {args.GetTriggerId()}");
            Console.WriteLine($"  触发器名称: {args.GetTriggerName()}");
            Console.WriteLine($"  触发源: {args.Source}");
            Console.WriteLine($"  触发时间: {args.TriggerTime:yyyy-MM-dd HH:mm:ss.fff}");

            // 模拟测试步骤
            Console.WriteLine($"[{_name}] 步骤1: 初始化设备...");
            await Task.Delay(_durationMs / 3);

            Console.WriteLine($"[{_name}] 步骤2: 执行测试...");
            await Task.Delay(_durationMs / 3);

            Console.WriteLine($"[{_name}] 步骤3: 保存结果...");
            await Task.Delay(_durationMs / 3);

            Console.WriteLine($"[{_name}] ✓ 测试完成 - {args.GetSN()}\n");
        }
    }

    /// <summary>
    /// 数据记录器
    /// </summary>
    public class DataLogger : ITriggerObserver
    {
        public async Task HandleTriggerAsync(TriggerEventArgs args)
        {
            await Task.Run(() =>
            {
                Console.WriteLine($"[DataLogger] 记录数据: SN={args.GetSN()}, Source={args.Source}");
            });
        }
    }

    /// <summary>
    /// MES上传器
    /// </summary>
    public class MESUploader : ITriggerObserver
    {
        public async Task HandleTriggerAsync(TriggerEventArgs args)
        {
            Console.WriteLine($"[MES] 上传测试数据: {args.GetSN()}");
            await Task.Delay(500); // 模拟网络上传
            Console.WriteLine($"[MES] ✓ 上传成功");
        }
    }

    #endregion

    #region ========== 完整使用示例 ==========

    class Program
    {
      
        /// <summary>
        /// 简单示例
        /// </summary>
        static async Task SimpleDemo()
        {
            // 1. 创建管理器
            var manager = new TriggerManager();

            // 2. 创建并注册触发器
            var manualTrigger = new ManualScanTrigger();
            manager.RegisterTrigger("Manual", manualTrigger);

            // 3. 注册测试流程
            manager.RegisterObserver(new ProductTestProcess("测试流程", 2000));

            // 4. 启动触发器
            await manager.StartTriggerAsync("Manual");

            // 5. 触发测试
            await manualTrigger.TriggerTestAsync("TEST001");
            await Task.Delay(3000);

            // 6. 停止
            await manager.StopAllAsync();
        }

        /// <summary>
        /// 扫码枪示例
        /// </summary>
        static async Task ScannerDemo()
        {
            var manager = new TriggerManager();

            // 创建扫码枪
            var scanner = new KeyenceScanner("Scanner001", "主扫码枪", "192.168.1.100");

            // 注册触发器
            manager.RegisterTrigger("MainScanner", new ScannerTrigger(scanner));

            // 注册测试流程
            manager.RegisterObserver(new ProductTestProcess("测试流程", 3000));

            // 启动
            await manager.StartTriggerAsync("MainScanner");

            // 模拟扫码
            for (int i = 1; i <= 5; i++)
            {
                scanner.SimulateScan($"SN{i:D6}");
                await Task.Delay(4000);
            }

            // 停止
            await manager.StopAllAsync();
        }


        /// <summary>
        /// 网络API示例
        /// </summary>
        static async Task APIDemo()
        {
            var manager = new TriggerManager();

            // 创建API触发器
            var apiTrigger = new NetworkAPITrigger(8080);

            // 注册
            manager.RegisterTrigger("API", apiTrigger);
            manager.RegisterObserver(new ProductTestProcess("API触发测试", 2000));

            // 启动
            await manager.StartTriggerAsync("API");

            Console.WriteLine("\nAPI服务已启动，可以通过以下方式触发：");
            Console.WriteLine("  方式1: 浏览器访问 http://localhost:8080/trigger/?sn=TEST001");
            Console.WriteLine("  方式2: curl http://localhost:8080/trigger/?sn=TEST001");
            Console.WriteLine("  方式3: Postman GET http://localhost:8080/trigger/?sn=TEST001");
            Console.WriteLine("\n按任意键停止...\n");

            Console.ReadKey();

            // 停止
            await manager.StopAllAsync();
        }

        /// <summary>
        /// 并发测试示例
        /// </summary>
        static async Task ConcurrencyDemo()
        {
            // 并行模式
            var manager = new TriggerManager(new TestExecutionConfig
            {
                ExecutionMode = TestExecutionMode.Parallel,
                MaxConcurrency = 3
            });

            var scanner = new KeyenceScanner("Scanner", "测试扫码枪", "192.168.1.100");
            manager.RegisterTrigger("Scanner", new ScannerTrigger(scanner));
            manager.RegisterObserver(new ProductTestProcess("并发测试", 5000));

            await manager.StartTriggerAsync("Scanner");

            Console.WriteLine(">>> 快速连续触发10次，测试并发控制\n");

            for (int i = 1; i <= 10; i++)
            {
                scanner.SimulateScan($"SN{i:D3}");
                await Task.Delay(500); // 快速触发
            }

            Console.WriteLine("\n>>> 等待所有测试完成...\n");
            await Task.Delay(20000);

            manager.PrintStatus();

            await manager.StopAllAsync();
        }

        /// <summary>
        /// 防重复测试示例
        /// </summary>
        static async Task AntiRepeatDemo()
        {
            var manager = new TriggerManager();

            var scanner = new KeyenceScanner("Scanner", "测试扫码枪", "192.168.1.100");
            manager.RegisterTrigger("Scanner", new ScannerTrigger(scanner));

            // 配置防重复：相同SN 3秒内只能触发一次
            manager.ConfigureAntiRepeat("Scanner", new AntiRepeatConfig
            {
                Enabled = true,
                MinIntervalMs = 3000,
                GlobalMinIntervalMs = 500
            });

            manager.RegisterObserver(new ProductTestProcess("防重复测试", 1000));

            await manager.StartTriggerAsync("Scanner");

            Console.WriteLine(">>> 测试1: 相同SN快速连续扫描（应该被阻止）\n");
            scanner.SimulateScan("SN001");
            await Task.Delay(500);
            scanner.SimulateScan("SN001"); // 被阻止
            await Task.Delay(500);
            scanner.SimulateScan("SN001"); // 被阻止
            await Task.Delay(2500);
            scanner.SimulateScan("SN001"); // 超过3秒，允许

            await Task.Delay(3000);

            Console.WriteLine("\n>>> 测试2: 不同SN快速扫描（全局间隔限制）\n");
            scanner.SimulateScan("SN002");
            await Task.Delay(200);
            scanner.SimulateScan("SN003"); // 全局间隔不足，被阻止
            await Task.Delay(400);
            scanner.SimulateScan("SN004"); // 允许

            await Task.Delay(3000);

            manager.PrintStatus();

            await manager.StopAllAsync();
        }
    }

    #endregion
}
            
