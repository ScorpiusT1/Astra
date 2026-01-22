using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Devices;
using Astra.Core.Devices.Base;
using Astra.Core.Devices.Configuration;
using Astra.Core.Foundation.Common;
using Microsoft.Extensions.Logging;
using Astra.Plugins.DataAcquisition.Configs;
using Astra.Plugins.DataAcquisition.SDKs;
using Astra.Core.Logs;

namespace Astra.Plugins.DataAcquisition.Devices
{
    /// <summary>
    /// BRC数据采集卡连接管理类
    /// </summary>
    public class BRCDataAcquisitionDeviceConnection : DeviceConnectionBase
    {
        private readonly object _syncRoot = new();
        private readonly DataAcquisitionConfig _config;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private BRCSDK.BrcDevice _brcDevice;
        private BRCSDK.ModuleInfo _moduleInfo;

        public BRCDataAcquisitionDeviceConnection(DataAcquisitionConfig config, Microsoft.Extensions.Logging.ILogger logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
            AutoReconnectEnabled = true;
        }

        protected override bool DoCheckAlive()
        {
            lock (_syncRoot)
            {
                try
                {
                    if (_brcDevice == null)
                        return false;

                    // 尝试获取设备属性来验证设备是否在线
                    _brcDevice.GetModulePropertySampleRate();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        protected override Task<bool> DoCheckAliveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(DoCheckAlive());
        }

        protected override bool DoCheckDeviceExists()
        {
            lock (_syncRoot)
            {
                try
                {
                    // 扫描设备
                    var modules = BRCSDK.ScanModules();
                    
                    // 根据配置的序列号或设备ID查找设备
                    if (!string.IsNullOrWhiteSpace(_config.SerialNumber))
                    {
                        _moduleInfo = modules.FirstOrDefault(m => 
                            m.DeviceId == _config.SerialNumber || 
                            (m.ProductName != null && m.ProductName.Contains(_config.SerialNumber)));
                    }
                    else
                    {
                        // 如果没有序列号，使用第一个可用设备
                        _moduleInfo = modules.FirstOrDefault();
                    }

                    return _moduleInfo != null;
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[{_config.DeviceName}] 扫描设备失败: {ex.Message}", ex, LogCategory.Device);
                    return false;
                }
            }
        }

        protected override Task<bool> DoCheckDeviceExistsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(DoCheckDeviceExists());
        }

        protected override OperationResult DoConnect()
        {
            lock (_syncRoot)
            {
                if (_brcDevice != null)
                {
                    return OperationResult.Succeed("设备已连接");
                }

                try
                {
                    // 确保设备存在
                    if (!DoCheckDeviceExists())
                    {
                        return OperationResult.Failure(
                            $"BRC采集卡 {_config.DeviceName} 未找到", 
                            ErrorCodes.DeviceNotFound);
                    }

                    if (_moduleInfo == null)
                    {
                        return OperationResult.Failure(
                            "未找到匹配的设备模块", 
                            ErrorCodes.DeviceNotFound);
                    }

                    // 连接设备
                    _brcDevice = BRCSDK.Connect(_moduleInfo);
                    
                    // 配置模块属性
                    ConfigureModuleProperties();
                    
                    // 配置通道属性
                    ConfigureChannelProperties();

                    _logger?.LogInformation($"[{_config.DeviceName}] BRC采集卡连接成功: {_moduleInfo.DeviceId}", LogCategory.Device);
                    return OperationResult.Succeed("BRC采集卡连接成功");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[{_config.DeviceName}] 连接失败: {ex.Message}", ex, LogCategory.Device);
                    return OperationResult.Fail($"连接BRC采集卡失败: {ex.Message}", ex, ErrorCodes.ConnectFailed);
                }
            }
        }

        protected override Task<OperationResult> DoConnectAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => DoConnect(), cancellationToken);
        }

        protected override OperationResult DoDisconnect()
        {
            lock (_syncRoot)
            {
                if (_brcDevice == null)
                {
                    return OperationResult.Succeed("设备已断开");
                }

                try
                {
                    // 如果正在采集，先停止
                    try
                    {
                        _brcDevice.Stop();
                    }
                    catch
                    {
                        // 忽略停止失败的错误
                    }

                    // 断开连接
                    _brcDevice.Dispose();
                    _brcDevice = null;
                    _moduleInfo = null;

                    _logger?.LogInformation($"[{_config.DeviceName}] BRC采集卡已断开", LogCategory.Device);
                    return OperationResult.Succeed("BRC采集卡断开成功");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[{_config.DeviceName}] 断开失败: {ex.Message}", ex, LogCategory.Device);
                    return OperationResult.Fail($"断开BRC采集卡失败: {ex.Message}", ex, ErrorCodes.DisconnectFailed);
                }
            }
        }

        protected override Task<OperationResult> DoDisconnectAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => DoDisconnect(), cancellationToken);
        }

        /// <summary>
        /// 配置模块属性
        /// </summary>
        private void ConfigureModuleProperties()
        {
            if (_brcDevice == null)
                return;

            try
            {
                // 设置采样率
                if (_config.SampleRate > 0)
                {
                    // 验证采样率是否在支持范围内
                    var supportedRates = _moduleInfo.SampleRateOptions;
                    if (supportedRates != null && supportedRates.Count > 0)
                    {
                        // 找到最接近的采样率
                        var targetRate = supportedRates
                            .OrderBy(r => Math.Abs(r - _config.SampleRate))
                            .First();
                        
                        _brcDevice.SetModulePropertySampleRate(targetRate);
                        _logger?.LogInformation($"[{_config.DeviceName}] 采样率设置为: {targetRate} Hz", LogCategory.Device);
                    }
                }

                // 设置时钟源（默认使用板载时钟）
                _brcDevice.SetModulePropertyClockSource(SourceType.ONBOARD);
                
                // 设置触发源（默认使用板载触发）
                _brcDevice.SetModulePropertyTrigerSource(SourceType.ONBOARD);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"[{_config.DeviceName}] 配置模块属性失败: {ex.Message}", LogCategory.Device);
            }
        }

        /// <summary>
        /// 配置通道属性
        /// </summary>
        private void ConfigureChannelProperties()
        {
            if (_brcDevice == null || _config.Channels == null)
                return;

            try
            {
                foreach (var channelConfig in _config.Channels)
                {
                    var channelIndex = channelConfig.ChannelId - 1; // SDK使用0基索引

                    // 设置通道启用状态
                    _brcDevice.SetChannelPropertyEnabled(channelIndex, channelConfig.Enabled);

                    if (channelConfig.Enabled)
                    {
                        // 设置增益
                        if (channelConfig.Gain > 0)
                        {
                            _brcDevice.SetChannelPropertyGain(channelIndex, channelConfig.Gain);
                        }

                        // 设置电流（如果有）
                        if (channelConfig.ICPCurrent > 0)
                        {
                            _brcDevice.SetChannelPropertyCurrent(channelIndex, channelConfig.ICPCurrent);
                        }

                        // 设置耦合模式（将配置中的CouplingMode转换为BRC SDK的CouplingMode）
                       Configs.CouplingMode couplingMode;
                        switch (channelConfig.CouplingMode)
                        {
                            case Configs.CouplingMode.AC:
                                couplingMode = Configs.CouplingMode.AC;
                                break;
                            case Configs.CouplingMode.DC:
                                couplingMode = Configs.CouplingMode.DC;
                                break;
                            case Configs.CouplingMode.ICP:
                                // BRC SDK不支持ICP模式，使用AC模式替代
                                couplingMode = Configs.CouplingMode.AC;
                                _logger?.LogWarning($"[{_config.DeviceName}] 通道{channelConfig.ChannelId}的ICP模式已转换为AC模式", LogCategory.Device);
                                break;
                            default:
                                couplingMode = Configs.CouplingMode.AC;
                                break;
                        }
                        _brcDevice.SetChannelPropertyCouplingMode(channelIndex, couplingMode);
                    }
                }

                _logger?.LogInformation($"[{_config.DeviceName}] 已配置 {_config.Channels.Count(c => c.Enabled)} 个通道", LogCategory.Device);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"[{_config.DeviceName}] 配置通道属性失败: {ex.Message}", LogCategory.Device);
            }
        }

        /// <summary>
        /// 获取BRC设备实例（供设备类使用）
        /// </summary>
        public BRCSDK.BrcDevice GetBrcDevice()
        {
            lock (_syncRoot)
            {
                return _brcDevice;
            }
        }

        /// <summary>
        /// 获取模块信息
        /// </summary>
        public BRCSDK.ModuleInfo GetModuleInfo()
        {
            lock (_syncRoot)
            {
                return _moduleInfo;
            }
        }
    }
}

