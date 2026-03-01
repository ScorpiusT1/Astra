using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows.Controls;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.UI;

namespace Astra.Services
{
    /// <summary>
    /// 在正确的插件加载上下文中创建配置/调试 View 与 ViewModel，保证 WPF 资源与类型解析正确。
    /// </summary>
    public sealed class PluginViewFactory : IPluginViewFactory
    {
        private readonly IPluginHost _pluginHost;

        public PluginViewFactory(IPluginHost pluginHost)
        {
            _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
        }

        /// <inheritdoc />
        public (object View, object ViewModel) CreateView(Type viewType, Type viewModelType, object configOrDevice)
        {
            if (viewType == null)
                return (null, null);

            try
            {
                object view = CreateViewCore(viewType);
                if (view == null || !(view is UserControl control))
                    return (null, null);

                object viewModel = null;
                if (viewModelType != null)
                {
                    viewModel = CreateViewModel(viewModelType, configOrDevice);
                    if (viewModel != null)
                        control.DataContext = viewModel;
                }

                if (view is IDeviceAwareView deviceAware && configOrDevice is IDevice device)
                    deviceAware.AttachDevice(device);

                return (control, viewModel);
            }
            catch (Exception)
            {
                return (null, null);
            }
        }

        private object CreateViewCore(Type viewType)
        {
            Assembly viewAssembly = viewType.Assembly;
            Assembly defaultContextAssembly = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(a => !a.IsDynamic && a.Location != null
                    && AssemblyName.ReferenceMatchesDefinition(a.GetName(), viewAssembly.GetName()));

            Type viewTypeToUse = viewType;
            if (defaultContextAssembly != null && defaultContextAssembly != viewAssembly)
            {
                var defaultViewType = defaultContextAssembly.GetType(viewType.FullName);
                if (defaultViewType != null)
                    viewTypeToUse = defaultViewType;
            }

            if (defaultContextAssembly == null && !string.IsNullOrEmpty(viewAssembly.Location) && File.Exists(viewAssembly.Location))
            {
                try
                {
                    defaultContextAssembly = Assembly.LoadFrom(viewAssembly.Location);
                    var defaultViewType = defaultContextAssembly.GetType(viewType.FullName);
                    if (defaultViewType != null)
                        viewTypeToUse = defaultViewType;
                }
                catch { /* 继续使用插件上下文中的类型 */ }
            }

            Assembly targetAssembly = defaultContextAssembly ?? viewAssembly;
            if (targetAssembly != null)
            {
                using (AssemblyLoadContext.EnterContextualReflection(targetAssembly))
                {
                    return Activator.CreateInstance(viewTypeToUse);
                }
            }
            return Activator.CreateInstance(viewTypeToUse);
        }

        /// <summary>
        /// 通过构造函数或属性注入创建 ViewModel，不依赖具体接口或属性名。
        /// 优先：单参构造函数（参数类型可接受 configOrDevice）；否则无参构造 + 可写且类型兼容的属性注入。
        /// </summary>
        private static object CreateViewModel(Type viewModelType, object configOrDevice)
        {
            if (viewModelType == null) return null;

            if (configOrDevice == null)
            {
                var ctor = viewModelType.GetConstructor(Type.EmptyTypes);
                return ctor != null ? Activator.CreateInstance(viewModelType) : null;
            }

            var valueType = configOrDevice.GetType();

            // 1. 任一单参构造函数，且参数类型可接受当前实参（实参可赋值给参数类型）
            foreach (var ctor in viewModelType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != 1) continue;
                var paramType = parameters[0].ParameterType;
                if (!paramType.IsInstanceOfType(configOrDevice)) continue;
                try
                {
                    return ctor.Invoke(new[] { configOrDevice });
                }
                catch { /* 忽略，尝试下一个 */ }
            }

            // 2. 无参构造 + 可写且类型兼容的属性
            var parameterless = viewModelType.GetConstructor(Type.EmptyTypes);
            if (parameterless == null) return null;

            object vm;
            try
            {
                vm = Activator.CreateInstance(viewModelType);
            }
            catch
            {
                return null;
            }

            foreach (var prop in viewModelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite || prop.SetMethod == null) continue;
                var propType = prop.PropertyType;
                if (!propType.IsInstanceOfType(configOrDevice)) continue;
                try
                {
                    prop.SetValue(vm, configOrDevice);
                    return vm;
                }
                catch { /* 忽略，尝试下一个属性 */ }
            }

            return null;
        }

        /// <inheritdoc />
        public (object View, object ViewModel) CreateConfigViewForDevice(IDevice device)
        {
            if (device == null) return (null, null);
            var attr = device.GetType().GetCustomAttribute<DeviceConfigUIAttribute>();
            if (attr == null) return (null, null);
            return CreateView(attr.ViewType, attr.ViewModelType, device);
        }

        /// <inheritdoc />
        public (object View, object ViewModel) CreateDebugViewForDevice(IDevice device)
        {
            if (device == null) return (null, null);
            var attr = device.GetType().GetCustomAttribute<DeviceDebugUIAttribute>();
            if (attr == null) return (null, null);
            return CreateView(attr.ViewType, attr.ViewModelType, device);
        }
    }
}
