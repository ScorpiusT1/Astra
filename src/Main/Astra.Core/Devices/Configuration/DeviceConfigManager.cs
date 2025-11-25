using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Serialization;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Configuration
{
    /// <summary>
    /// 设备配置管理器
    /// </summary>
    public class DeviceConfigManager<TConfig> where TConfig : DeviceConfig
    {
        private readonly ConcurrentDictionary<string, TConfig> _configs = new ConcurrentDictionary<string, TConfig>();
        private readonly ConcurrentDictionary<string, IConfigurable<TConfig>> _configurables = new ConcurrentDictionary<string, IConfigurable<TConfig>>();
        private readonly ConcurrentDictionary<string, List<ConfigChangeRecord>> _changeHistory = new ConcurrentDictionary<string, List<ConfigChangeRecord>>();
        private readonly IConfigSerializer _serializer;

        public DeviceConfigManager(IConfigSerializer serializer = null)
        {
            _serializer = serializer ?? new JsonConfigSerializer();
        }

        #region 配置注册

        public OperationResult RegisterConfigurable(string deviceId, IConfigurable<TConfig> configurable)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return OperationResult.Failure("设备ID不能为空", ErrorCodes.InvalidData);

            if (configurable == null)
                return OperationResult.Failure("可配置对象不能为空", ErrorCodes.InvalidData);

            if (_configurables.TryAdd(deviceId, configurable))
            {
                _configs[deviceId] = (TConfig)configurable.CurrentConfig.Clone();

                configurable.ConfigChanged += (sender, e) =>
                {
                    _configs[deviceId] = (TConfig)e.NewConfig.Clone();
                    RecordConfigChange(deviceId, e);
                };

                return OperationResult.Succeed($"设备 {deviceId} 注册成功");
            }

            return OperationResult.Failure($"设备 {deviceId} 已存在", ErrorCodes.InvalidData);
        }

        public OperationResult UnregisterConfigurable(string deviceId)
        {
            if (_configurables.TryRemove(deviceId, out _))
            {
                _configs.TryRemove(deviceId, out _);
                _changeHistory.TryRemove(deviceId, out _);
                return OperationResult.Succeed($"设备 {deviceId} 注销成功");
            }

            return OperationResult.Failure($"设备 {deviceId} 不存在", ErrorCodes.DeviceNotFound);
        }

        #endregion

        #region 配置更新

        public OperationResult UpdateConfig(string deviceId, TConfig newConfig, string changedBy = null)
        {
            if (!_configurables.TryGetValue(deviceId, out var configurable))
                return OperationResult.Failure($"设备 {deviceId} 未注册", ErrorCodes.DeviceNotFound);

            var validateResult = newConfig.Validate();
            if (!validateResult.Success)
                return OperationResult.Failure($"配置验证失败: {validateResult.ErrorMessage}", ErrorCodes.InvalidConfig);

            var oldConfig = configurable.CurrentConfig;
            var changedProps = oldConfig.GetChangedProperties(newConfig);

            if (changedProps.Count == 0)
                return OperationResult.Succeed("配置无变更");

            var restartRequired = newConfig.GetRestartRequiredProperties()
                .Intersect(changedProps)
                .ToList();

            if (restartRequired.Any())
            {
                var restartProps = string.Join(", ", restartRequired);
                return OperationResult.Failure(
                    $"以下配置项需要重启设备: {restartProps}。请使用 UpdateConfigWithRestart 方法。",
                    ErrorCodes.ConfigRequireRestart)
                    .WithData("RestartRequired", true)
                    .WithData("RestartProperties", restartRequired);
            }

            var applyResult = configurable.ApplyConfig(newConfig);
            if (!applyResult.Success)
                return applyResult;

            return OperationResult.Succeed($"配置更新成功，变更了 {changedProps.Count} 个属性")
                .WithData("ChangedProperties", changedProps);
        }

        public OperationResult UpdateConfigWithRestart(string deviceId, TConfig newConfig, string changedBy = null)
        {
            if (!_configurables.TryGetValue(deviceId, out var configurable))
                return OperationResult.Failure($"设备 {deviceId} 未注册", ErrorCodes.DeviceNotFound);

            if (configurable is IDevice device)
            {
                var disconnectResult = device.Disconnect();
                if (!disconnectResult.Success)
                    return OperationResult.Failure($"断开设备失败: {disconnectResult.ErrorMessage}", ErrorCodes.DisconnectFailed);

                var applyResult = configurable.ApplyConfig(newConfig);
                if (!applyResult.Success)
                {
                    device.Connect();
                    return OperationResult.Failure($"应用配置失败: {applyResult.ErrorMessage}", ErrorCodes.ConfigApplyFailed);
                }

                var connectResult = device.Connect();
                if (!connectResult.Success)
                    return OperationResult.Failure($"重启设备失败: {connectResult.ErrorMessage}", ErrorCodes.ConnectFailed);

                return OperationResult.Succeed("配置更新成功，设备已重启");
            }

            return OperationResult.Failure("设备不支持重启", ErrorCodes.NotSupported);
        }

        public OperationResult UpdateProperty(string deviceId, string propertyName, object value, string changedBy = null)
        {
            if (!_configurables.TryGetValue(deviceId, out var configurable))
                return OperationResult.Failure($"设备 {deviceId} 未注册", ErrorCodes.DeviceNotFound);

            var newConfig = (TConfig)configurable.CurrentConfig.Clone();

            var prop = newConfig.GetType().GetProperty(propertyName);
            if (prop == null)
                return OperationResult.Failure($"属性 {propertyName} 不存在", ErrorCodes.InvalidData);

            try
            {
                prop.SetValue(newConfig, Convert.ChangeType(value, prop.PropertyType));
                return UpdateConfig(deviceId, newConfig, changedBy);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"设置属性失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 配置查询

        public OperationResult<TConfig> GetConfig(string deviceId)
        {
            if (_configs.TryGetValue(deviceId, out var config))
            {
                return OperationResult<TConfig>.Succeed((TConfig)config.Clone());
            }

            return OperationResult<TConfig>.Failure($"设备 {deviceId} 配置不存在", ErrorCodes.DeviceNotFound);
        }

        public OperationResult<Dictionary<string, TConfig>> GetAllConfigs()
        {
            var configs = _configs.ToDictionary(
                kvp => kvp.Key,
                kvp => (TConfig)kvp.Value.Clone()
            );

            return OperationResult<Dictionary<string, TConfig>>.Succeed(configs);
        }

        #endregion

        #region 配置历史

        private void RecordConfigChange(string deviceId, ConfigChangedEventArgs<TConfig> e)
        {
            var record = new ConfigChangeRecord
            {
                Timestamp = e.Timestamp,
                ChangedBy = e.ChangedBy,
                ChangedProperties = e.ChangedProperties,
                OldValues = e.OldConfig?.ToDictionary(),
                NewValues = e.NewConfig?.ToDictionary()
            };

            _changeHistory.AddOrUpdate(
                deviceId,
                new List<ConfigChangeRecord> { record },
                (key, list) =>
                {
                    list.Add(record);
                    if (list.Count > 100)
                        list.RemoveAt(0);
                    return list;
                }
            );
        }

        public OperationResult<List<ConfigChangeRecord>> GetChangeHistory(string deviceId, int maxCount = 10)
        {
            if (_changeHistory.TryGetValue(deviceId, out var history))
            {
                var records = history.TakeLast(maxCount).ToList();
                return OperationResult<List<ConfigChangeRecord>>.Succeed(records);
            }

            return OperationResult<List<ConfigChangeRecord>>.Succeed(new List<ConfigChangeRecord>());
        }

        #endregion

        #region 配置持久化

        public OperationResult SaveToFile(string filePath)
        {
            try
            {
                var json = _serializer.Serialize(_configs);
                System.IO.File.WriteAllText(filePath, json);
                return OperationResult.Succeed($"配置已保存到: {filePath}");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"保存配置失败: {ex.Message}", ex, ErrorCodes.ConfigSaveFailed);
            }
        }

        public OperationResult LoadFromFile(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                    return OperationResult.Failure($"配置文件不存在: {filePath}", ErrorCodes.FileNotFound);

                var json = System.IO.File.ReadAllText(filePath);
                var configs = _serializer.Deserialize<ConcurrentDictionary<string, TConfig>>(json);

                if (configs == null)
                    return OperationResult.Failure("配置文件格式错误", ErrorCodes.InvalidConfig);

                int successCount = 0;
                foreach (var kvp in configs)
                {
                    var result = UpdateConfig(kvp.Key, kvp.Value, "FileLoad");
                    if (result.Success)
                        successCount++;
                }

                return OperationResult.Succeed($"从文件加载配置成功: {successCount}/{configs.Count}");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"加载配置失败: {ex.Message}", ex, ErrorCodes.ConfigLoadFailed);
            }
        }

        #endregion
    }
}