# 插件 UI 契约

- **IPluginViewFactory**：在正确的 AssemblyLoadContext 下创建插件提供的配置/调试 View 与 ViewModel，供宿主展示。
- 宿主通过 DI 解析 `IPluginViewFactory`，调用 `CreateView(viewType, viewModelType, configOrDevice)` 或设备便捷方法 `CreateConfigViewForDevice`/`CreateDebugViewForDevice`。
- 设备端在类型上标记 `DeviceConfigUIAttribute` / `DeviceDebugUIAttribute`（见 `Astra.Core.Devices.Attributes`），可选实现 `IDeviceAwareView` 以接收当前设备实例。
