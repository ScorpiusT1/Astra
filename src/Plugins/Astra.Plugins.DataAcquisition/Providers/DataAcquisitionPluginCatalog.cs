using Astra.Core.Data;

namespace Astra.Plugins.DataAcquisition.Providers;

/// <summary>
/// 将 <see cref="DataAcquisitionCardProvider"/> 挂接到 <see cref="AcquisitionDeviceCatalog"/>，
/// 使其它插件仅依赖 Core 即可解析设备与通道。
/// </summary>
internal sealed class DataAcquisitionPluginCatalog : IAcquisitionDeviceCatalog
{
    public bool TryGetDeviceIdByDisplayName(string? displayName, out string deviceId) =>
        DataAcquisitionCardProvider.TryGetDeviceIdByDisplayName(displayName, out deviceId);

    public IReadOnlyList<string> GetAcquisitionDeviceDisplayNames() =>
        DataAcquisitionCardProvider.GetDataAcquisitionNames();

    public IReadOnlyList<string> GetConfiguredChannelNamesForDeviceDisplayName(string? deviceDisplayName) =>
        DataAcquisitionCardProvider.GetConfiguredChannelNamesForDeviceDisplayName(deviceDisplayName);
}
