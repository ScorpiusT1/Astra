namespace Astra.Core.Data;

/// <summary>
/// 全局采集设备目录入口：其它插件只依赖 Core，通过本类访问，不引用数据采集插件程序集。
/// </summary>
public static class AcquisitionDeviceCatalog
{
    private static IAcquisitionDeviceCatalog? _impl;
    private static readonly object Gate = new();

    /// <summary>当前已注册的实现（通常为数据采集插件提供）。</summary>
    public static IAcquisitionDeviceCatalog? Instance
    {
        get
        {
            lock (Gate)
                return _impl;
        }
    }

    /// <summary>由数据采集插件在初始化时调用。</summary>
    public static void Register(IAcquisitionDeviceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        lock (Gate)
            _impl = catalog;
    }

    /// <summary>由数据采集插件在卸载时调用，避免悬空实现。</summary>
    public static void Unregister(IAcquisitionDeviceCatalog catalog)
    {
        lock (Gate)
        {
            if (ReferenceEquals(_impl, catalog))
                _impl = null;
        }
    }

    public static bool TryGetDeviceIdByDisplayName(string? displayName, out string deviceId)
    {
        var impl = Instance;
        if (impl == null)
        {
            deviceId = string.Empty;
            return false;
        }

        return impl.TryGetDeviceIdByDisplayName(displayName, out deviceId);
    }

    public static IReadOnlyList<string> GetAcquisitionDeviceDisplayNames()
        => Instance?.GetAcquisitionDeviceDisplayNames() ?? Array.Empty<string>();

    public static IReadOnlyList<string> GetConfiguredChannelNamesForDeviceDisplayName(string? deviceDisplayName)
        => Instance?.GetConfiguredChannelNamesForDeviceDisplayName(deviceDisplayName)
           ?? SingleEmptyChannelFallback;

    private static readonly IReadOnlyList<string> SingleEmptyChannelFallback = new[] { string.Empty };
}
