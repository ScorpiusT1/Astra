# Astra.Engine 引擎库

## 📋 项目说明

`Astra.Engine` 是 **自动化测试系统的引擎实现层**，负责实现具体的触发器（Trigger）和设备连接器，基于 `Astra.Core` 提供的触发器框架进行扩展。

## 🎯 核心职责

### 1. **触发器实现**
实现各种具体的触发器类型，用于触发自动化测试流程：

- **手动扫码触发器** (`ManualScanTrigger`) - 事件驱动型
- **扫码枪触发器** (`ScannerTrigger`) - 事件驱动型
- **PLC监控触发器** (`PLCMonitorTrigger`) - 轮询型
- **网络API触发器** (`NetworkAPITrigger`) - 事件驱动型
- **定时触发器** (`TimerTrigger`) - 轮询型

### 2. **设备连接器**
实现具体的工业设备通信接口：

- **扫码枪设备接口** (`IScannerDevice`)
  - 基恩士扫码枪实现 (`KeyenceScanner`) - TCP/IP 连接
- **PLC连接器接口** (`IPLCConnector`)
  - 西门子PLC连接器 (`SiemensPLCConnector`) - S7协议

### 3. **测试流程示例**
提供测试流程的示例实现：

- `ProductTestProcess` - 产品测试流程
- `DataLogger` - 数据记录器
- `MESUploader` - MES系统上传器

## 📁 目录结构

```
Astra.Engine/
├── Triggers/
│   └── PLCMonitorTrigger.cs    # 包含所有触发器实现
└── README.md                    # 本文件
```

> **注意：** 当前所有触发器实现都在 `PLCMonitorTrigger.cs` 文件中（虽然文件名是 `PLCMonitorTrigger.cs`，但实际包含了所有触发器实现）。

## 🔗 依赖关系

```
Astra.Engine
    └── 依赖 ──> Astra.Core (触发器框架)
```

- `Astra.Core` 提供：
  - `TriggerBase` 基类
  - `ITrigger` 接口
  - `TriggerManager` 管理器
  - 触发器配置和枚举类型

- `Astra.Engine` 提供：
  - 具体的触发器实现
  - 设备连接器实现
  - 业务逻辑实现

## 🏗️ 架构设计

### 分层架构

```
┌─────────────────────────────────────┐
│         Astra (WPF应用)              │
│    使用 TriggerManager 管理触发器      │
└─────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────┐
│      Astra.Engine (引擎实现层)        │
│   - ManualScanTrigger               │
│   - ScannerTrigger                  │
│   - PLCMonitorTrigger               │
│   - NetworkAPITrigger               │
│   - TimerTrigger                    │
└─────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────┐
│      Astra.Core (核心框架层)          │
│   - TriggerBase (基类)              │
│   - TriggerManager (管理器)          │
│   - ITrigger (接口)                  │
│   - 配置和枚举类型                    │
└─────────────────────────────────────┘
```

### 触发器类型

#### 1. 事件驱动型触发器
- **特点：** 被动等待外部事件触发
- **实现方式：** 订阅设备事件或网络请求
- **示例：**
  - `ManualScanTrigger` - 手动触发
  - `ScannerTrigger` - 扫码枪数据接收事件
  - `NetworkAPITrigger` - HTTP请求事件

#### 2. 轮询型触发器
- **特点：** 主动定期检查状态
- **实现方式：** 定时轮询设备状态
- **示例：**
  - `PLCMonitorTrigger` - 定期读取PLC状态
  - `TimerTrigger` - 定时触发

## 📝 使用示例

### 示例1：手动扫码触发器

```csharp
var manager = new TriggerManager();
var manualTrigger = new ManualScanTrigger();
manager.RegisterTrigger("Manual", manualTrigger);
manager.RegisterObserver(new ProductTestProcess("测试流程", 2000));

await manager.StartTriggerAsync("Manual");
await manualTrigger.TriggerTestAsync("TEST001");
```

### 示例2：扫码枪触发器

```csharp
var scanner = new KeyenceScanner("Scanner001", "主扫码枪", "192.168.1.100");
var scannerTrigger = new ScannerTrigger(scanner);

manager.RegisterTrigger("MainScanner", scannerTrigger);
manager.RegisterObserver(new ProductTestProcess("测试流程", 3000));

await manager.StartTriggerAsync("MainScanner");
scanner.SimulateScan("SN001");
```

### 示例3：PLC监控触发器

```csharp
var plc = new SiemensPLCConnector("192.168.1.50", rack: 0, slot: 1);
var plcTrigger = new PLCMonitorTrigger(plc, "M0.0", "DB1.DBX0.0");

manager.RegisterTrigger("PLC", plcTrigger);
await manager.StartTriggerAsync("PLC");
```

### 示例4：网络API触发器

```csharp
var apiTrigger = new NetworkAPITrigger(8080);
manager.RegisterTrigger("API", apiTrigger);
await manager.StartTriggerAsync("API");

// 通过 HTTP GET 触发：http://localhost:8080/trigger/?sn=TEST001
```

## 🔧 扩展开发

### 添加新的触发器

1. **继承 `TriggerBase` 基类**
   ```csharp
   public class CustomTrigger : TriggerBase
   {
       public override string TriggerName => "自定义触发器";
       protected override TriggerWorkType WorkType => TriggerWorkType.EventDriven;
       
       // 实现必要的方法
   }
   ```

2. **选择工作类型**
   - `EventDriven` - 事件驱动型，重写 `InitializeEventDrivenAsync`
   - `Polling` - 轮询型，重写 `CheckTriggerAsync`

3. **注册到管理器**
   ```csharp
   manager.RegisterTrigger("Custom", new CustomTrigger());
   ```

### 添加新的设备连接器

1. **实现设备接口**
   ```csharp
   public class CustomDevice : IScannerDevice
   {
       // 实现接口方法
   }
   ```

2. **在触发器中使用**
   ```csharp
   public class CustomTrigger : TriggerBase
   {
       private readonly IScannerDevice _device;
       // ...
   }
   ```

## 📊 触发器对比

| 触发器类型 | 工作方式 | 适用场景 | 轮询间隔 |
|-----------|---------|---------|---------|
| ManualScanTrigger | 事件驱动 | 手动触发测试 | - |
| ScannerTrigger | 事件驱动 | 扫码枪自动触发 | - |
| PLCMonitorTrigger | 轮询 | PLC信号监控 | 50ms |
| NetworkAPITrigger | 事件驱动 | 外部系统调用 | - |
| TimerTrigger | 轮询 | 定时测试 | 可配置 |

## ⚠️ 注意事项

1. **命名空间**
   - 所有触发器实现位于 `Astra.Engine.Triggers` 命名空间

2. **依赖注入**
   - 当前未使用 DI，未来可以考虑通过 DI 注入设备连接器

3. **异步处理**
   - 所有触发器方法都是异步的，使用 `async/await` 模式

4. **错误处理**
   - 触发器应包含完善的异常处理机制

5. **线程安全**
   - 触发器需要保证线程安全，特别是在并发场景下

## 🎯 未来改进建议

1. **分离文件**
   - 将每个触发器实现拆分到独立文件
   - 提高代码可维护性

2. **配置化**
   - 通过配置文件管理触发器参数
   - 支持动态加载触发器配置

3. **插件化**
   - 将触发器作为插件加载
   - 支持运行时动态添加触发器

4. **单元测试**
   - 为每个触发器添加单元测试
   - 提高代码质量

---

**创建时间：** 2024年  
**项目状态：** 生产环境使用中

