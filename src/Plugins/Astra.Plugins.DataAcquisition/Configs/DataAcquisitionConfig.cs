using Astra.Core.Configuration;
using Astra.Core.Devices;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Common;
using Astra.Core.Devices.Configuration;
using Astra.Core.Foundation.Common;
using Astra.Plugins.DataAcquisition.Configs;
using Astra.Plugins.DataAcquisition.ViewModels;
using Astra.Plugins.DataAcquisition.Views;
using Astra.UI.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Astra.Plugins.DataAcquisition.Devices
{
    [TreeNodeConfig("采集卡", "📊", typeof(DataAcquisitionDeviceConfigView), typeof(DataAcquisitionDeviceConfigViewModel))]
    public class DataAcquisitionConfig : DeviceConfig, IPreSaveConfig, IPostLoadConfig
    {
        private string _serialNumber = string.Empty;

        private ObservableCollection<DAQChannelConfig> _channels;

        public DataAcquisitionConfig() : base()
        {
            InitializeDeviceInfo(DeviceType.DataAcquisition);
            _channels = new ObservableCollection<DAQChannelConfig>();
            _channels.CollectionChanged += Channels_CollectionChanged;

            // 初始化默认通道
            InitializeDefaultChannels();
        }

        public DataAcquisitionConfig(string configId) : this()
        {
            ConfigId = configId;
        }

        /// <summary>
        /// 恢复所有通道的传感器引用（在配置加载后调用）
        /// 从传感器库中根据保存的传感器ID查找并绑定传感器对象
        /// </summary>
        public void RestoreSensorReferences(IEnumerable<SensorConfig> availableSensors)
        {
            if (_channels == null || availableSensors == null)
                return;

            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionConfig] 开始恢复传感器引用，通道数: {_channels.Count}, 可用传感器数: {availableSensors.Count()}");

            foreach (var channel in _channels)
            {
                channel?.RestoreSensorReference(availableSensors, channel.SensorId);
            }

            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionConfig] 传感器引用恢复完成");
        }

        /// <summary>
        /// 实现 IPreSaveConfig 接口：在保存配置前保存通道中的独立模式传感器配置
        /// </summary>
        public async Task<OperationResult> PreSaveAsync(IConfigurationManager configManager)
        {
            if (configManager == null)
            {
                return OperationResult.Failure("配置管理器未初始化");
            }

            if (_channels == null)
            {
                return OperationResult.Succeed();
            }

            var sensorConfigs = new List<SensorConfig>();

            // 收集所有独立模式的传感器配置
            foreach (var channel in _channels)
            {
                if (channel?.Sensor != null &&
                    channel.SensorConfigMode == SensorConfigMode.Independent)
                {
                    // 确保传感器配置有有效的 ConfigId
                    if (string.IsNullOrEmpty(channel.Sensor.ConfigId))
                    {
                        // 如果 ConfigId 为空，生成一个新的（使用反射设置）
                        var configIdField = typeof(ConfigBase).GetField("_configId",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (configIdField != null)
                        {
                            configIdField.SetValue(channel.Sensor, Guid.NewGuid().ToString());
                        }
                    }

                    // 确保传感器配置有名称
                    if (string.IsNullOrEmpty(channel.Sensor.ConfigName))
                    {
                        channel.Sensor.ConfigName = $"{channel.Sensor.SensorType}_{channel.ChannelId}";
                    }

                    sensorConfigs.Add(channel.Sensor);
                }
            }

            // 保存所有独立模式的传感器配置
            var errors = new List<string>();
            foreach (var sensor in sensorConfigs)
            {
                try
                {
                    var result = await configManager.UpdateConfigAsync(sensor);
                    if (result != null && !result.Success)
                    {
                        errors.Add($"传感器 {sensor.ConfigName}: {result.Message}");
                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionConfig] 保存独立模式传感器配置失败: {sensor.ConfigName}, 错误: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"传感器 {sensor.ConfigName}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionConfig] 保存独立模式传感器配置异常: {sensor.ConfigName}, 错误: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                return OperationResult.Failure($"保存独立模式传感器配置时发生错误:\n{string.Join("\n", errors)}");
            }

            return OperationResult.Succeed();
        }

        /// <summary>
        /// 实现 IPostLoadConfig 接口：在配置加载后恢复传感器引用
        /// </summary>
        public async Task<OperationResult> PostLoadAsync(IConfigurationManager configManager)
        {
            if (configManager == null)
            {
                return OperationResult.Failure("配置管理器未初始化");
            }

            if (_channels == null)
            {
                return OperationResult.Succeed();
            }

            try
            {
                // 获取所有传感器配置
                var sensorResult = await configManager.GetAllConfigsAsync<SensorConfig>();
                var availableSensors = sensorResult?.Data?.ToList() ?? new List<SensorConfig>();

                // 恢复传感器引用
                RestoreSensorReferences(availableSensors);

                return OperationResult.Succeed();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionConfig] 加载后处理失败: {ex.Message}");
                return OperationResult.Failure($"加载后处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设备序列号
        /// </summary>
        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                var oldValue = _serialNumber;
                SetProperty(ref _serialNumber, value);

                if (!string.Equals(oldValue, value, StringComparison.OrdinalIgnoreCase))
                {
                    DeviceId = GenerateDeviceId();
                }
            }
        }

        private double _sampleRate = 51200.0;
        private int _channelCount = 8;
        private int _bufferSize = 8_192;
        private bool _autoStart = true;

        /// <summary>
        /// 采样率（Hz）
        /// </summary>
        [HotUpdatable]
        public double SampleRate
        {
            get => _sampleRate;
            set
            {
                if (SetProperty(ref _sampleRate, value))
                {
                    // 同步更新所有通道的采样率
                    SyncSampleRateToChannels();
                }
            }
        }

        /// <summary>
        /// 同步采样率到所有通道
        /// </summary>
        private void SyncSampleRateToChannels()
        {
            if (_channels == null)
                return;

            foreach (var channel in _channels)
            {
                channel.SampleRate = _sampleRate;
            }
        }

        /// <summary>
        /// 通道配置集合
        /// </summary>
        public ObservableCollection<DAQChannelConfig> Channels
        {
            get => _channels;
            set
            {
                if (_channels != null)
                {
                    _channels.CollectionChanged -= Channels_CollectionChanged;
                }

                SetProperty(ref _channels, value);

                if (_channels != null)
                {
                    _channels.CollectionChanged += Channels_CollectionChanged;
                    UpdateChannelCount();
                }
            }
        }

        /// <summary>
        /// 通道数量（从通道集合自动计算）
        /// </summary>
        [HotUpdatable]
        public int ChannelCount
        {
            get => _channelCount;
            set
            {
                if (SetProperty(ref _channelCount, value))
                {
                    // 当手动设置通道数量时，调整通道集合
                    SyncChannelsToCount(value);
                }
            }
        }

        /// <summary>
        /// 通道集合变化事件处理
        /// </summary>
        private void Channels_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateChannelCount();
        }

        /// <summary>
        /// 更新通道数量（从通道集合计算）
        /// </summary>
        private void UpdateChannelCount()
        {
            if (_channels != null)
            {
                var newCount = _channels.Count;
                if (_channelCount != newCount)
                {
                    var oldValue = _channelCount;
                    // 直接设置字段避免触发集合调整
                    _channelCount = newCount;
                    OnPropertyChanged(new PropertyChangedEventArgs
                    {
                        PropertyName = nameof(ChannelCount),
                        OldValue = oldValue,
                        NewValue = newCount
                    });
                }
            }
        }

        /// <summary>
        /// 同步通道集合到指定的数量
        /// </summary>
        private void SyncChannelsToCount(int targetCount)
        {
            if (_channels == null)
                return;

            var currentCount = _channels.Count;

            if (targetCount > currentCount)
            {
                // 添加通道
                for (int i = currentCount; i < targetCount; i++)
                {
                    var channel = new DAQChannelConfig
                    {
                        ChannelId = i + 1,
                        ChannelName = $"通道 {i + 1}",
                        SampleRate = SampleRate,
                        Enabled = true
                    };
                    _channels.Add(channel);
                }
            }
            else if (targetCount < currentCount)
            {
                // 删除多余的通道
                while (_channels.Count > targetCount)
                {
                    _channels.RemoveAt(_channels.Count - 1);
                }
            }

            // 重新编号所有通道
            for (int i = 0; i < _channels.Count; i++)
            {
                _channels[i].ChannelId = i + 1;
            }
        }

        /// <summary>
        /// 初始化默认通道
        /// </summary>
        private void InitializeDefaultChannels()
        {
            for (int i = 0; i < _channelCount; i++)
            {
                var channel = new DAQChannelConfig
                {
                    ChannelId = i + 1,
                    ChannelName = $"通道 {i + 1}",
                    SampleRate = SampleRate,
                    Enabled = true
                };
                _channels.Add(channel);
            }
        }

        /// <summary>
        /// 单帧缓冲区大小（采样点）
        /// </summary>
        [RequireRestart("缓冲区大小变更需要重新建立采集缓存")]
        public int BufferSize
        {
            get => _bufferSize;
            set => SetProperty(ref _bufferSize, value);
        }

        /// <summary>
        /// 插件启动时是否自动开始采集
        /// </summary>
        public bool AutoStart
        {
            get => _autoStart;
            set => SetProperty(ref _autoStart, value);
        }

        public override DeviceConfig Clone()
        {
            var clone = new DataAcquisitionConfig
            {
                DeviceName = DeviceName,
                SerialNumber = SerialNumber,
                SampleRate = SampleRate,
                BufferSize = BufferSize,
                AutoStart = AutoStart,
                IsEnabled = IsEnabled,
                GroupId = GroupId,
                SlotId = SlotId,
                // IConfig 接口属性
                Version = Version,
                ModifiedAt = ModifiedAt,
                ConfigName = ConfigName
            };

            // 克隆通道配置
            clone._channels.Clear();
            foreach (var channel in Channels)
            {
                // 需要实现 DAQChannelConfig 的 Clone 方法，这里先简单复制属性
                var clonedChannel = new DAQChannelConfig
                {
                    ChannelId = channel.ChannelId,
                    ChannelName = channel.ChannelName,
                    Enabled = channel.Enabled,
                    SampleRate = channel.SampleRate,
                    CouplingMode = channel.CouplingMode,
                    Gain = channel.Gain,
                    Offset = channel.Offset,
                    ICPCurrent = channel.ICPCurrent,
                    EnableAntiAliasingFilter = channel.EnableAntiAliasingFilter,
                    AntiAliasingCutoff = channel.AntiAliasingCutoff,
                    MeasurementLocation = channel.MeasurementLocation,
                    MountingDirection = channel.MountingDirection,
                    CoordinateX = channel.CoordinateX,
                    CoordinateY = channel.CoordinateY,
                    CoordinateZ = channel.CoordinateZ,
                    AlarmEnabled = channel.AlarmEnabled,
                    AlarmUpperLimit = channel.AlarmUpperLimit,
                    AlarmLowerLimit = channel.AlarmLowerLimit,
                    DisplayColor = channel.DisplayColor,
                    DisplayOrder = channel.DisplayOrder,
                    SensorConfigMode = channel.SensorConfigMode,
                    // 注意：传感器引用不复制，因为它是通过 SensorId 序列化的
                    // 反序列化时会根据 SensorId 重新加载传感器引用
                };
                clone._channels.Add(clonedChannel);
            }

            return clone;
        }

        public override string GenerateDeviceId()
        {
            return DeviceIdGenerator.Generate("DAQ", GroupId, SlotId, SerialNumber, DeviceName);
        }

        public override OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            var baseResult = base.Validate();
            if (!baseResult.Success && !string.IsNullOrWhiteSpace(baseResult.ErrorMessage))
            {
                errors.Add(baseResult.ErrorMessage);
            }

            if (SampleRate <= 0)
            {
                errors.Add("采样率必须大于 0 Hz");
            }

            if (ChannelCount <= 0)
            {
                errors.Add("通道数量必须大于 0");
            }

            if (BufferSize <= 0)
            {
                errors.Add("缓冲区大小必须大于 0");
            }

            if (errors.Count > 0)
            {
                return OperationResult<bool>.Failure(string.Join(Environment.NewLine, errors));
            }

            return OperationResult<bool>.Succeed(true, "采集卡配置验证通过");
        }
    }
}
