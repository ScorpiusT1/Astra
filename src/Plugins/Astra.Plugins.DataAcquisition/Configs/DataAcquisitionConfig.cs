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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Astra.Plugins.DataAcquisition.Devices
{
    [TreeNodeConfig("数据采集", "📊", typeof(DataAcquisitionDeviceConfigView), typeof(DataAcquisitionDeviceConfigViewModel))]
    [ConfigUI(typeof(DataAcquisitionDeviceConfigView), typeof(DataAcquisitionDeviceConfigViewModel))]
  
    public class DataAcquisitionConfig : DeviceConfig,IDeviceSpecificationConstraint
    {

        private ObservableCollection<DAQChannelConfig> _channels;
        
        // 同步标志，避免通道数量双向同步时的循环更新
        private bool _isSyncingChannels = false;

        public DataAcquisitionConfig() : base()
        {
            InitializeDeviceInfo(DeviceType.DataAcquisition);
            _channels = new ObservableCollection<DAQChannelConfig>();
            _channels.CollectionChanged -= Channels_CollectionChanged;
            _channels.CollectionChanged += Channels_CollectionChanged;

            // 初始化默认通道
            InitializeDefaultChannels();
        }

        public DataAcquisitionConfig(string configId) : this()
        {
            ConfigId = configId;
        }

        /// <summary>
        /// 恢复所有通道的传感器引用（在配置加载后调用，从传感器库根据保存的传感器ID查找并绑定传感器对象）
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
        /// 实现 IPreSaveConfig：保存前同步独立传感器到配置库
        /// </summary>
        public async Task<OperationResult> PreSaveAsync(IConfigurationManager configManager)
        {
            if (configManager == null)
                return OperationResult.Failure("配置管理器不能为空");

            if (_channels == null || _channels.Count == 0)
                return OperationResult.Succeed();

            // 仅处理独立模式的传感器
            var independentSensors = _channels
                .Where(c => c?.Sensor != null && c.SensorConfigMode == SensorConfigMode.Independent)
                .Select(c => c.Sensor)
                .ToList();

            if (independentSensors.Count == 0)
                return OperationResult.Succeed();

            // 遍历独立传感器
            foreach (var sensor in independentSensors)
            {
                // 若未设置 ConfigId 则生成
                if (sensor is ConfigBase sensorConfigBase && string.IsNullOrEmpty(sensorConfigBase.ConfigId))
                {
                    sensorConfigBase.SetConfigId(Guid.NewGuid().ToString());
                }

                // 若传感器无显示名则自动生成
                if (string.IsNullOrEmpty(sensor.ConfigName))
                {
                    var channel = _channels.FirstOrDefault(c => c.Sensor == sensor);
                    sensor.ConfigName = $"{sensor.SensorType}_{channel?.ChannelId ?? 0}";
                }
            }

            // 收集保存错误
            var errors = new List<string>();
            foreach (var sensor in independentSensors)
            {
                try
                {
                    var result = await configManager.SaveAsync(sensor);
                    if (result != null && !result.Success)
                    {
                        errors.Add($"传感器 '{sensor.ConfigName}' 保存失败: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"传感器 '{sensor.ConfigName}' 保存异常: {ex.Message}");
                }
            }

            return errors.Count > 0
                ? OperationResult.Failure($"保存独立传感器失败:\n{string.Join("\n", errors)}")
                : OperationResult.Succeed();
        }

        /// <summary>
        /// 实现 IPostLoadConfig：加载后恢复传感器引用
        /// </summary>
        public async Task<OperationResult> PostLoadAsync(IConfigurationManager configManager)
        {
            if (configManager == null)
                return OperationResult.Failure("配置管理器不能为空");

            if (_channels == null)
                return OperationResult.Succeed();

            try
            {
                // 加载传感器配置列表
                var sensorResult = await configManager.GetAllAsync<SensorConfig>();
                var availableSensors = sensorResult?.Data?.ToList() ?? new List<SensorConfig>();

                // 恢复通道内传感器引用
                RestoreSensorReferences(availableSensors);

                return OperationResult.Succeed();
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"加载传感器配置失败: {ex.Message}");
            }
        }

        // 注意：SerialNumber 等属性定义在基类 DeviceConfig 中

        private double _sampleRate = 51200.0;
        private int _channelCount = 8;
        private int _bufferSize = 4_096;
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
        /// 将当前采样率同步到所有通道
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
        /// 通道列表
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
        /// 通道数量（根据型号动态约束）
        /// 注意：不在 setter 中自动增删通道，避免在反序列化/克隆等场景产生副作用。
        /// 需要调整通道集合时，请显式调用 <see cref="SyncChannelsToCount(int)"/>。
        /// </summary>
        [HotUpdatable]
        public int ChannelCount
        {
            get => _channelCount;
            set => SetProperty(ref _channelCount, value);
        }

        /// <summary>
        /// 通道集合变更时同步通道数
        /// </summary>
        private void Channels_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateChannelCount();
        }

        /// <summary>
        /// 根据通道集合数量更新 ChannelCount
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
        /// 将通道数量同步到目标值（增删通道）
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
                    // 增加通道
                    for (int i = currentCount; i < targetCount; i++)
                    {
                        _channels.Add(CreateChannel(i + 1));
                    }
                }
                else if (targetCount < currentCount)
                {
                    // 移除多余通道
                    while (_channels.Count > targetCount)
                    {
                        _channels.RemoveAt(_channels.Count - 1);
                    }
                }

                // 重新编号通道
                RenumberChannels();
            }
            finally
            {
                _isSyncingChannels = false;
            }
        }

        /// <summary>
        /// 创建并返回新通道实例
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
        /// 重新编号通道 ID
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

            NormalizeChannels();
        }

        /// <summary>
        /// 统一规范化通道的编号和默认名称，确保 ChannelId 为 1..N
        /// </summary>
        private void NormalizeChannels()
        {
            if (_channels == null)
                return;

            for (int i = 0; i < _channels.Count; i++)
            {
                var channel = _channels[i];
                if (channel == null)
                    continue;

                int expectedId = i + 1;
                channel.ChannelId = expectedId;

                // 仅在名称为空时补默认名，避免覆盖用户自定义的名称
                if (string.IsNullOrWhiteSpace(channel.ChannelName))
                {
                    channel.ChannelName = $"通道 {expectedId}";
                }
            }
        }

        /// <summary>
        /// 缓冲区大小（采样点）
        /// </summary>
        [RequireRestart("缓冲区大小变更需要重新建立采集缓冲")]
        public int BufferSize
        {
            get => _bufferSize;
            set => SetProperty(ref _bufferSize, value);
        }

        /// <summary>
        /// 插件启用时是否自动开始采集
        /// </summary>
        public bool AutoStart
        {
            get => _autoStart;
            set => SetProperty(ref _autoStart, value);
        }

        /// <summary>
        /// 深拷贝配置（基于序列化，但显式保留通道的 Id 与名称，避免反序列化期间的副作用影响）
        /// </summary>
        public override DeviceConfig Clone()
        {
            // 使用基类的序列化方法实现深拷贝
            var json = Serialize();
            var clone = Deserialize<DataAcquisitionConfig>(json);

            if (clone != null && _channels != null && clone._channels != null)
            {
                var count = Math.Min(_channels.Count, clone._channels.Count);
                for (int i = 0; i < count; i++)
                {
                    // 强制让克隆后的通道 Id / 名称 与 原配置完全一致
                    clone._channels[i].ChannelId = _channels[i].ChannelId;
                    clone._channels[i].ChannelName = _channels[i].ChannelName;
                }
            }

            // 重置配置ID和元数据
            if (clone is ConfigBase cloneConfigBase)
            {
                cloneConfigBase.SetConfigId(Guid.NewGuid().ToString());
            }

            return clone;
        }

        public override string GenerateDeviceId()
        {
            // 在设备ID中包含厂商和型号信息
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

            // 显式根据最终的 ChannelCount 调整通道集合，避免在属性 setter 中产生隐式副作用
            SyncChannelsToCount(ChannelCount);

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
        /// 重写验证方法，根据规格校验
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
                    errors.Add($"通道数量必须在 {minChannels} 与 {maxChannels} 之间（当前：{ChannelCount}）");
                }
            }

            var maxSampleRate = specification.GetConstraint<double>("MaxSampleRate", double.MaxValue);
            var minSampleRate = specification.GetConstraint<double>("MinSampleRate", 1000.0);
            if (SampleRate < minSampleRate || SampleRate > maxSampleRate)
            {
                errors.Add($"采样率必须在 {minSampleRate} 与 {maxSampleRate} Hz 之间（当前：{SampleRate}）");
            }

            var maxBufferSize = specification.GetConstraint<int>("MaxBufferSize", int.MaxValue);
            var minBufferSize = specification.GetConstraint<int>("MinBufferSize", 1024);
            if (BufferSize < minBufferSize || BufferSize > maxBufferSize)
            {
                errors.Add($"缓冲区大小必须在 {minBufferSize} 与 {maxBufferSize} 之间（当前：{BufferSize}）");
            }

            return errors;
        }

        /// <summary>
        /// 获取配置的显示名称（用于树节点等 UI 显示）
        /// 要求：设备厂家名 + 设备型号（序号由外层 GetNodeDisplayName/EnsureUniqueNumber 追加）
        /// </summary>
        public override string GetDisplayName()
        {
            // 只根据厂家和型号生成基础名称，例如：“BRC BRC6804”
            // 若两者缺失，则回退到默认名称“数据采集”
            return ConfigDisplayNameHelper.BuildDisplayName(
                Manufacturer,
                Model,
                serialNumber: null,
                configName: null,
                defaultName: "数据采集");
        }

        public override OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            // 基类校验
            var baseResult = base.Validate();
            if (!baseResult.Success && !string.IsNullOrWhiteSpace(baseResult.ErrorMessage))
            {
                errors.Add(baseResult.ErrorMessage);
            }

            // 本类校验
            if (SampleRate <= 0) errors.Add("采样率必须大于 0 Hz");
            if (ChannelCount <= 0) errors.Add("通道数量必须大于 0");
            if (BufferSize <= 0) errors.Add("缓冲区大小必须大于 0");
            if (string.IsNullOrEmpty(SerialNumber)) errors.Add("序列号不能为空");

            return errors.Count > 0
                ? OperationResult<bool>.Failure(string.Join(Environment.NewLine, errors))
                : OperationResult<bool>.Succeed(true, "校验通过");
        }
    }
}

