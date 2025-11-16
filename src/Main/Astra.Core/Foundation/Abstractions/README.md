# Foundation/Abstractions 核心抽象目录

## 📋 目录说明

本目录用于存放**跨模块通用的核心抽象接口定义**，为平台提供统一的抽象层。

## 📁 当前文件

### **消息抽象层**

1. **IMessage.cs** ✅
   - 消息接口（平台通用）
   - 所有模块的消息类型都应实现此接口
   - 提供统一的消息结构（ChannelId, Timestamp, Properties, Length, Data）

2. **MessageBase.cs** ✅
   - 消息基类（平台通用）
   - 提供消息的通用实现
   - 各模块可以继承此类或直接实现 `IMessage` 接口

3. **IHighSpeedDataAcquisition.cs** ✅
   - 高性能数据采集接口（平台通用）
   - 使用泛型 `IHighSpeedDataAcquisition<TMessage>` 支持任意消息类型
   - 泛型约束 `where TMessage : IMessage` 确保类型安全
   - 适用于所有需要高速数据采集的模块（Devices, Nodes, Triggers, Logs 等）

### **模块注册接口**

4. **IModuleRegistrar.cs** ✅
   - 模块注册接口
   - 用于模块化服务注册

## 🎯 使用目的

### **1. 统一消息结构**

所有模块的消息类型都实现 `IMessage` 接口：
- `DeviceMessage` - 设备模块
- `NodeData` - 节点模块（未来）
- `TriggerEvent` - 触发器模块（未来）
- `LogEntry` - 日志模块（未来）

### **2. 平台通用能力**

`IHighSpeedDataAcquisition<TMessage>` 接口成为平台通用能力：
- 各模块可以复用此接口
- 使用自己的消息类型实现接口
- 符合通用平台的设计理念

### **3. 类型安全**

- 泛型约束确保类型安全
- 编译时类型检查
- 避免运行时类型错误

## 📝 使用示例

### **Devices 模块**

```csharp
// DeviceMessage 继承 MessageBase
public class DeviceMessage : MessageBase { }

// IHighSpeedDevice 使用泛型接口
public interface IHighSpeedDevice : 
    IDevice, 
    IHighSpeedDataAcquisition<DeviceMessage>
{
}
```

### **Nodes 模块（未来扩展）**

```csharp
// NodeData 继承 MessageBase
public class NodeData : MessageBase { }

// IHighSpeedNode 使用泛型接口
public interface IHighSpeedNode : 
    IHighSpeedDataAcquisition<NodeData>
{
}
```

## ⚠️ 注意事项

- ✅ 接口定义在 Foundation，但实现类在各模块
- ✅ 保持向后兼容，不破坏现有功能
- ✅ 逐步迁移，不一次性大改

## 📚 相关文档

- `../docs/MESSAGE_ABSTRACTION_LAYER.md` - 消息抽象层设计文档
- `../docs/IHIGH_SPEED_DATA_ACQUISITION_REASSESSMENT.md` - 接口重新评估文档

---

**创建时间：** 2024年  
**状态：** ✅ 已实施
