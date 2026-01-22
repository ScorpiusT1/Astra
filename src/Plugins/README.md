# Astra 插件开发指南

## 概述

Astra 插件系统采用统一的构建配置，通过共享的 `.props` 文件简化插件开发流程。

## 快速开始

### 1. 创建插件项目

创建新的插件项目时，只需：

1. 创建新的 `.csproj` 文件
2. 导入共享的构建配置：`<Import Project="..\Astra.Plugins.Common.props" />`
3. 配置项目特定的属性（如 TargetFramework、PackageReference 等）

### 2. 插件项目模板

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<!-- ⭐ 导入插件通用构建配置 -->
	<Import Project="..\Astra.Plugins.Common.props" />

	<PropertyGroup>
		<TargetFramework>net8.0-windows</TargetFramework>
		<Nullable>enable</Nullable>
		<UseWPF>true</UseWPF>
		<ImplicitUsings>enable</ImplicitUsings>
	</PropertyGroup>

	<ItemGroup>
		<!-- 项目引用：不复制依赖项（主程序已包含这些库） -->
		<ProjectReference Include="..\..\Main\Astra.Core\Astra.Core.csproj">
			<Private>false</Private>
			<CopyLocal>false</CopyLocal>
		</ProjectReference>
	</ItemGroup>
</Project>
```

### 3. 创建插件类

实现 `IPlugin` 接口：

```csharp
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Health;

namespace Astra.Plugins.YourPlugin
{
    public class YourPlugin : IPlugin
    {
        public string Id => "Astra.Plugins.YourPlugin";
        public string Name => "您的插件名称";
        public Version Version => new Version(1, 0, 0);

        public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            // 初始化逻辑
        }

        public async Task OnEnableAsync(CancellationToken cancellationToken = default)
        {
            // 启用逻辑
        }

        public async Task OnDisableAsync(CancellationToken cancellationToken = default)
        {
            // 禁用逻辑
        }

        public async Task<HealthCheckResult> CheckHealthAsync()
        {
            // 健康检查逻辑
            return HealthCheckResult.Healthy(Name);
        }

        public void Dispose()
        {
            // 释放资源
        }

        public async ValueTask DisposeAsync()
        {
            // 异步释放资源
        }
    }
}
```

### 4. 创建插件清单文件

创建 `YourPlugin.addin` 文件（XML 格式）：

```xml
<?xml version="1.0" encoding="utf-8"?>
<AddinManifest>
	<Schema>http://pluginsystem.org/schema/v1</Schema>
	<Addin>
		<Id>Astra.Plugins.YourPlugin</Id>
		<Name>您的插件名称</Name>
		<Version>1.0.0</Version>
		<Description>插件描述</Description>
		<Author>Astra Team</Author>
		<Copyright>Copyright © Astra Team</Copyright>
		<Runtime>
			<Assembly>YourPlugin.dll</Assembly>
			<TypeName>Astra.Plugins.YourPlugin.YourPlugin</TypeName>
			<TargetFramework>net8.0-windows</TargetFramework>
		</Runtime>
		<Dependencies />
		<Permissions>
			<Required />
		</Permissions>
	</Addin>
</AddinManifest>
```

## 共享构建配置说明

`Astra.Plugins.Common.props` 文件自动处理以下内容：

### 自动配置

1. **输出路径**：自动设置到 `Bin\{Configuration}\Plugins\{PluginName}` 目录
2. **插件名称提取**：自动从程序集名称提取（去掉 `Astra.Plugins.` 前缀）
3. **依赖项管理**：自动阻止复制依赖项到输出目录
4. **文件复制**：自动复制 `.addin` 和 `.config.json` 文件到输出目录
5. **构建后清理**：自动清理不需要的依赖项文件
6. **配置同步**：如果存在 `.config.json` 文件，自动同步到 `Bin\Configs\Devices` 目录

### 项目引用配置

所有项目引用都应该设置：

```xml
<ProjectReference Include="...">
	<Private>false</Private>
	<CopyLocal>false</CopyLocal>
</ProjectReference>
```

这样可以确保依赖项不会被复制到插件输出目录（主程序已包含这些库）。

## 配置文件支持

如果插件需要配置文件，只需：

1. 创建 `{AssemblyName}.config.json` 文件
2. 构建系统会自动：
   - 复制到插件输出目录
   - 同步到 `Bin\Configs\Devices` 目录

## 最佳实践

1. **命名约定**：插件项目名称必须以 `Astra.Plugins.` 开头
2. **配置提供者**：继承 `JsonConfigProvider<T>` 的配置提供者会自动被发现和注册
3. **服务获取**：通过 `IPluginContext.ServiceProvider` 获取服务
4. **日志记录**：使用 `IPluginContext.Logger` 或 `context.Services.Resolve<ILogger>()`

## 示例插件

参考以下插件作为示例：

- `Astra.Plugins.DataAcquisition` - 完整的数据采集插件示例
- `Astra.Plugins.AudioPlayer` - 简单的音频播放器插件示例

## 常见问题

### Q: 如何添加 NuGet 包？

A: 直接在 `.csproj` 文件中添加 `<PackageReference>`，但注意不要添加主程序已包含的包。

### Q: 如何引用其他插件？

A: 在 `.addin` 文件的 `<Dependencies>` 节点中添加依赖声明。

### Q: 插件输出目录在哪里？

A: `Bin\{Configuration}\Plugins\{PluginName}`，其中 `{PluginName}` 是自动从程序集名称提取的。

## 改进历史

- **2026-01-XX**: 创建共享的 `Astra.Plugins.Common.props` 文件，简化插件开发流程
  - 将构建逻辑从 90+ 行减少到约 30 行
  - 统一管理所有插件的构建配置
  - 提高可维护性和一致性

