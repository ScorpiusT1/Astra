using Astra.Core.Configuration;
using Astra.Core.Devices;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Common;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Specifications;
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
    public class DataAcquisitionConfig : DeviceConfig, IPreSaveConfig, IPostLoadConfig, IDeviceSpecificationConstraint
    {

        private ObservableCollection<DAQChannelConfig> _channels;
        
        // 同步标志，避免通道数量双向同步时的循环更新
        private bool _isSyncingChannels = false;

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

            foreach (var channel in _channels)
            {
                channel?.RestoreSensorReference(availableSensors, channel.SensorId);
            }
        }

        /// <summary>
        /// 实现 IPreSaveConfig 接口：在保存配置前保存通道中的独立模式传感器配置
        /// </summary>
        public async Task<OperationResult> PreSaveAsync(IConfigurationManager configManager)
        {
            if (configManager == null)
                return OperationResult.Failure("配置管理器未初始化");

            if (_channels == null || _channels.Count == 0)
                return OperationResult.Succeed();

            // 收集所有独立模式的传感器配置
            var independentSensors = _channels
                .Where(c => c?.Sensor != null && c.SensorConfigMode == SensorConfigMode.Independent)
                .Select(c => c.Sensor)
                .ToList();

            if (independentSensors.Count == 0)
                return OperationResult.Succeed();

            // 确保所有传感器配置有效
            foreach (var sensor in independentSensors)
            {
                // ✅ 使用 SetConfigId 方法替代反射
                if (sensor is ConfigBase sensorConfigBase && string.IsNullOrEmpty(sensorConfigBase.ConfigId))
                {
                    sensorConfigBase.SetConfigId(Guid.NewGuid().ToString());
                }

                // 确保传感器配置有名称
                if (string.IsNullOrEmpty(sensor.ConfigName))
                {
                    var channel = _channels.FirstOrDefault(c => c.Sensor == sensor);
                    sensor.ConfigName = $"{sensor.SensorType}_{channel?.ChannelId ?? 0}";
                }
            }

            // 批量保存，收集错误
            var errors = new List<string>();
            foreach (var sensor in independentSensors)
            {
                try
                {
                    var result = await configManager.UpdateConfigAsync(sensor);
                    if (result != null && !result.Success)
                    {
                        errors.Add($"传感器 '{sensor.ConfigName}': {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"传感器 '{sensor.ConfigName}': {ex.Message}");
                }
            }

            return errors.Count > 0
                ? OperationResult.Failure($"保存独立模式传感器配置失败:\n{string.Join("\n", errors)}")
                : OperationResult.Succeed();
        }

        /// <summary>
        /// 实现 IPostLoadConfig 接口：在配置加载后恢复传感器引用
        /// </summary>
        public async Task<OperationResult> PostLoadAsync(IConfigurationManager configManager)
        {
            if (configManager == null)
                return OperationResult.Failure("配置管理器未初始化");

            if (_channels == null)
                return OperationResult.Succeed();

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
                return OperationResult.Failure($"加载后处理失败: {ex.Message}");
            }
        }

        // 注意：SerialNumber 属性已在基类 DeviceConfig 中定义，无需重复定义

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
            if (_isSyncingChannels || _channels == null)
                return;

            var newCount = _channels.Count;
            if (_channelCount != newCount)
            {
                _isSyncingChannels = true;
                try
                {
                    var oldValue = _channelCount;
                    _channelCount = newCount;
                    OnPropertyChanged(nameof(ChannelCount), oldValue, newCount);
                }
                finally
                {
                    _isSyncingChannels = false;
                }
            }
        }

        /// <summary>
        /// 同步通道集合到指定的数量
        /// </summary>
        private void SyncChannelsToCount(int targetCount)
        {
            if (_isSyncingChannels || _channels == null)
                return;

            if (targetCount < 0)
                targetCount = 0;

            _isSyncingChannels = true;
            try
            {
                var currentCount = _channels.Count;

                if (targetCount > currentCount)
                {
                    // 添加通道
                    for (int i = currentCount; i < targetCount; i++)
                    {
                        _channels.Add(CreateChannel(i + 1));
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
                RenumberChannels();
            }
            finally
            {
                _isSyncingChannels = false;
            }
        }

        /// <summary>
        /// 创建通道配置（提取公共逻辑，消除重复代码）
        /// </summary>
        private DAQChannelConfig CreateChannel(int channelId)
        {
            return new DAQChannelConfig
            {
                ChannelId = channelId,
                ChannelName = $"通道 {channelId}",
                SampleRate = SampleRate,
                Enabled = true
            };
        }

        /// <summary>
        /// 重新编号所有通道
        /// </summary>
        private void RenumberChannels()
        {
            if (_channels == null)
                return;

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
                _channels.Add(CreateChannel(i + 1));
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

        /// <summary>
        /// 克隆配置（使用基类的序列化方法，更简洁且不易出错）
        /// </summary>
        public override DeviceConfig Clone()
        {
            // 使用基类的序列化方法实现深拷贝
            var json = Serialize();
            var clone = ConfigBase.Deserialize<DataAcquisitionConfig>(json);

            // 重置配置ID和元数据
            if (clone is ConfigBase cloneConfigBase)
            {
                cloneConfigBase.SetConfigId(Guid.NewGuid().ToString());
            }

            // 注意：传感器引用不复制，因为它是通过 SensorId 序列化的
            // 反序列化时会根据 SensorId 重新加载传感器引用

            return clone;
        }

        public override string GenerateDeviceId()
        {
            // 在设备ID中包含厂家和型号信息
            var deviceIdentifier = string.IsNullOrEmpty(SerialNumber)
                ? $"{Manufacturer}_{Model}"
                : SerialNumber;

            return DeviceIdGenerator.Generate("DAQ", GroupId, SlotId, deviceIdentifier, DeviceName);
        }

        /// <summary>
        /// 实现 IDeviceSpecificationConstraint 接口
        /// </summary>
        public void ApplyConstraints(IDeviceSpecification specification)
        {
            // 限制通道数量（优先使用固定 ChannelCount 约束）
            var fixedChannels = specification.GetConstraint<int>("ChannelCount", -1);
            if (fixedChannels > 0)
            {
                ChannelCount = fixedChannels;
            }
            else
            {
                var maxChannels = specification.GetConstraint<int>("MaxChannels", int.MaxValue);
                var minChannels = specification.GetConstraint<int>("MinChannels", 1);
                if (ChannelCount > maxChannels)
                {
                    ChannelCount = maxChannels;
                }
                else if (ChannelCount < minChannels)
                {
                    ChannelCount = minChannels;
                }
            }

            // 限制采样率
            var maxSampleRate = specification.GetConstraint<double>("MaxSampleRate", double.MaxValue);
            var minSampleRate = specification.GetConstraint<double>("MinSampleRate", 1000.0);
            if (SampleRate > maxSampleRate)
            {
                SampleRate = maxSampleRate;
            }
            else if (SampleRate < minSampleRate)
            {
                SampleRate = minSampleRate;
            }

            // 限制缓冲区大小
            var maxBufferSize = specification.GetConstraint<int>("MaxBufferSize", int.MaxValue);
            var minBufferSize = specification.GetConstraint<int>("MinBufferSize", 1024);
            if (BufferSize > maxBufferSize)
            {
                BufferSize = maxBufferSize;
            }
            else if (BufferSize < minBufferSize)
            {
                BufferSize = minBufferSize;
            }
        }

        /// <summary>
        /// 重写验证方法，根据规格验证
        /// </summary>
        protected override List<string> ValidateAgainstSpecification(IDeviceSpecification specification)
        {
            var errors = new List<string>();

            var fixedChannels = specification.GetConstraint<int>("ChannelCount", -1);
            if (fixedChannels > 0)
            {
                if (ChannelCount != fixedChannels)
                {
                    errors.Add($"通道数量必须为 {fixedChannels}（当前：{ChannelCount}）");
                }
            }
            else
            {
                var maxChannels = specification.GetConstraint<int>("MaxChannels", int.MaxValue);
                var minChannels = specification.GetConstraint<int>("MinChannels", 1);
                if (ChannelCount < minChannels || ChannelCount > maxChannels)
                {
                    errors.Add($"通道数量必须在 {minChannels} 到 {maxChannels} 之间（当前：{ChannelCount}）");
                }
            }

            var maxSampleRate = specification.GetConstraint<double>("MaxSampleRate", double.MaxValue);
            var minSampleRate = specification.GetConstraint<double>("MinSampleRate", 1000.0);
            if (SampleRate < minSampleRate || SampleRate > maxSampleRate)
            {
                errors.Add($"采样率必须在 {minSampleRate} 到 {maxSampleRate} Hz 之间（当前：{SampleRate}）");
            }

            var maxBufferSize = specification.GetConstraint<int>("MaxBufferSize", int.MaxValue);
            var minBufferSize = specification.GetConstraint<int>("MinBufferSize", 1024);
            if (BufferSize < minBufferSize || BufferSize > maxBufferSize)
            {
                errors.Add($"缓冲区大小必须在 {minBufferSize} 到 {maxBufferSize} 之间（当前：{BufferSize}）");
            }

            return errors;
        }

        /// <summary>
        /// 获取配置的显示名称（用于树节点等UI显示）
        /// 格式：厂家 + 型号 + 编号
        /// </summary>
        public override string GetDisplayName()
        {
            // 优先使用 DeviceName，如果为空则使用 ConfigName
            var fallbackName = !string.IsNullOrWhiteSpace(DeviceName) ? DeviceName : ConfigName;
            
            return ConfigDisplayNameHelper.BuildDisplayName(
                Manufacturer,
                Model,
                SerialNumber,
                fallbackName,
                "未命名采集卡");
        }

        public override OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            // 基类验证
            var baseResult = base.Validate();
            if (!baseResult.Success && !string.IsNullOrWhiteSpace(baseResult.ErrorMessage))
            {
                errors.Add(baseResult.ErrorMessage);
            }

            // 本地验证
            if (SampleRate <= 0) errors.Add("采样率必须大于 0 Hz");
            if (ChannelCount <= 0) errors.Add("通道数量必须大于 0");
            if (BufferSize <= 0) errors.Add("缓冲区大小必须大于 0");

            return errors.Count > 0
                ? OperationResult<bool>.Failure(string.Join(Environment.NewLine, errors))
                : OperationResult<bool>.Succeed(true, "采集卡配置验证通过");
        }
    }
}

