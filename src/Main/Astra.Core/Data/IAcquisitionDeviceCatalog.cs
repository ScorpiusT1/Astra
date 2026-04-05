namespace Astra.Core.Data;

/// <summary>
/// 采集设备在 UI / 工作流中的目录：显示名与内部 deviceId 映射、已启用通道枚举。
/// 由数据采集插件在 <see cref="IPlugin.InitializeAsync"/> 中注册；未注册时 <see cref="AcquisitionDeviceCatalog"/> 返回空列表或解析失败。
/// </summary>
public interface IAcquisitionDeviceCatalog
{
    /// <summary>将属性面板或「设备/通道」中的采集卡显示名解析为 Raw 工件使用的 deviceId。</summary>
    bool TryGetDeviceIdByDisplayName(string? displayName, out string deviceId);

    /// <summary>当前可用的采集设备显示名列表。</summary>
    IReadOnlyList<string> GetAcquisitionDeviceDisplayNames();

    /// <summary>
    /// 指定设备下已启用通道名；首项常为空字符串，表示「组内首通道」（与现有节点约定一致）。
    /// </summary>
    IReadOnlyList<string> GetConfiguredChannelNamesForDeviceDisplayName(string? deviceDisplayName);
}
