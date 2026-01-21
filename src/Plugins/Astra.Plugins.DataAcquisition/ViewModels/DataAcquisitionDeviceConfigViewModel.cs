using Astra.Core.Configuration;
using Astra.Core.Devices;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Specifications;
using Astra.Plugins.DataAcquisition.Devices;
using Astra.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    public class DataAcquisitionDeviceConfigViewModel : ObservableObject
    {
        private DataAcquisitionConfig _config;

        /// <summary>
        /// 采样率选项列表（根据型号动态调整，单位：Hz）
        /// </summary>
        public IReadOnlyList<double> SampleRateOptions
        {
            get
            {
                if (DeviceSpecification != null)
                {
                    // 优先从规格中获取预定义的采样率集合
                    var allowedRates = DeviceSpecification.GetConstraint<List<double>>("AllowedSampleRates", null);
                    
                    if (allowedRates != null && allowedRates.Count > 0)
                    {
                        // 使用规格中定义的采样率集合
                        return allowedRates.OrderBy(x => x).Distinct().ToList();
                    }
                    
                    // 如果没有定义采样率集合，则根据 Min/Max 生成
                    var minSampleRate = DeviceSpecification.GetConstraint<double>("MinSampleRate", 1000.0);
                    var maxSampleRate = DeviceSpecification.GetConstraint<double>("MaxSampleRate", 51200.0);
                    
                    // 生成采样率选项列表
                    var options = new List<double>();
                    
                    // 常用采样率值（按升序排列）
                    var commonRates = new[]
                    {
                        1024.0, 1280.0, 1563.0, 1920.0, 2560.0, 3072.0,
                        3413.333, 3657.143, 3938.462, 4266.667, 4654.545,
                        5120.0, 5688.889, 6400.0, 7314.286, 8533.333,
                        10240.0, 12800.0, 17066.667, 25600.0, 48000.0, 51200.0,
                        102400.0, 204800.0, 409600.0
                    };
                    
                    // 添加在范围内的常用值
                    foreach (var rate in commonRates)
                    {
                        if (rate >= minSampleRate && rate <= maxSampleRate)
                        {
                            options.Add(rate);
                        }
                    }
                    
                    // 确保包含最小值和最大值
                    if (!options.Contains(minSampleRate))
                    {
                        options.Insert(0, minSampleRate);
                    }
                    if (!options.Contains(maxSampleRate))
                    {
                        options.Add(maxSampleRate);
                    }
                    
                    // 如果选项太少，添加一些中间值
                    if (options.Count < 5 && maxSampleRate > minSampleRate)
                    {
                        var step = (maxSampleRate - minSampleRate) / 4;
                        for (int i = 1; i < 4; i++)
                        {
                            var value = minSampleRate + step * i;
                            if (!options.Contains(value))
                            {
                                options.Add(value);
                            }
                        }
                    }
                    
                    return options.OrderBy(x => x).Distinct().ToList();
                }
                
                // 默认选项（如果没有规格）
                return new List<double>
                {
                    1024.0, 1280.0, 2560.0, 5120.0, 10240.0, 25600.0, 51200.0
                };
            }
        }

        /// <summary>
        /// 通道数量选项列表（根据型号动态调整）
        /// </summary>
        public IReadOnlyList<int> ChannelCountOptions
        {
            get
            {
                if (DeviceSpecification != null)
                {
                    var fixedChannels = DeviceSpecification.GetConstraint<int>("ChannelCount", -1);
                    if (fixedChannels > 0)
                    {
                        return new List<int> { fixedChannels };
                    }

                    var minChannels = DeviceSpecification.GetConstraint<int>("MinChannels", 1);
                    var maxChannels = DeviceSpecification.GetConstraint<int>("MaxChannels", 32);
                    
                    // 生成从最小到最大通道数的选项列表
                    var options = new List<int>();
                    var commonValues = new[] { 1, 2, 4, 8, 12, 16, 20, 24, 28, 32, 64 };
                    
                    foreach (var value in commonValues)
                    {
                        if (value >= minChannels && value <= maxChannels)
                        {
                            options.Add(value);
                        }
                    }
                    
                    // 如果常用值不够，添加边界值
                    if (!options.Contains(minChannels))
                    {
                        options.Insert(0, minChannels);
                    }
                    if (!options.Contains(maxChannels))
                    {
                        options.Add(maxChannels);
                    }
                    
                    return options.OrderBy(x => x).ToList();
                }
                
                // 默认选项
                return new List<int> { 2, 4, 8, 12, 16, 20, 24, 28, 32 };
            }
        }

        public DataAcquisitionConfig Config
        {
            get => _config;
            set
            {
                if (SetProperty(ref _config, value))
                {
                    OnConfigChanged();
                }
            }
        }

        /// <summary>
        /// 配置名称（设备名称）
        /// </summary>
        public string ConfigName
        {
            get => _config?.ConfigName ?? string.Empty;
            set
            {
                if (_config != null && _config.ConfigName != value)
                {
                    _config.ConfigName = value;
                    OnPropertyChanged();
                    // 同时通知 DeviceName 变更（因为 DeviceName 是 ConfigName 的别名）
                    OnPropertyChanged(nameof(DeviceName));
                }
            }
        }

        /// <summary>
        /// 设备名称（ConfigName 的别名，保持向后兼容）
        /// </summary>
        public string DeviceName
        {
            get => ConfigName;
            set => ConfigName = value;
        }

        public string SerialNumber
        {
            get => _config?.SerialNumber ?? string.Empty;
            set
            {
                if (_config != null && _config.SerialNumber != value)
                {
                    _config.SerialNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public double SampleRate
        {
            get => _config?.SampleRate ?? 51200.0;
            set
            {
                if (_config != null && Math.Abs(_config.SampleRate - value) > double.Epsilon)
                {
                    _config.SampleRate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 通道数量（只读，根据型号自动设置）
        /// </summary>
        public int ChannelCount
        {
            get => _config?.ChannelCount ?? 8;
        }

        /// <summary>
        /// 检查是否有已配置的通道（有传感器绑定或其他配置）
        /// </summary>
        private bool HasConfiguredChannels
        {
            get
            {
                if (_config?.Channels == null || _config.Channels.Count == 0)
                    return false;

                // 检查是否有通道配置了传感器或其他重要设置
                return _config.Channels.Any(channel =>
                    channel?.HasSensor == true ||
                    !string.IsNullOrWhiteSpace(channel?.ChannelName) ||
                    channel?.AlarmEnabled == true ||
                    !string.IsNullOrWhiteSpace(channel?.MeasurementLocation));
            }
        }

        public int BufferSize
        {
            get => _config?.BufferSize ?? 8192;
            set
            {
                if (_config != null && _config.BufferSize != value)
                {
                    _config.BufferSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoStart
        {
            get => _config?.AutoStart ?? true;
            set
            {
                if (_config != null && _config.AutoStart != value)
                {
                    _config.AutoStart = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEnabled
        {
            get => _config?.IsEnabled ?? true;
            set
            {
                if (_config != null && _config.IsEnabled != value)
                {
                    _config.IsEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GroupId
        {
            get => _config?.GroupId ?? "G0";
            set
            {
                if (_config != null && _config.GroupId != value)
                {
                    _config.GroupId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SlotId
        {
            get => _config?.SlotId ?? "S0";
            set
            {
                if (_config != null && _config.SlotId != value)
                {
                    _config.SlotId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DeviceId => _config?.DeviceId ?? string.Empty;

        #region 设备标识属性

        private string _manufacturer = string.Empty;
        private string _model = string.Empty;
        private ObservableCollection<string> _availableManufacturers;
        private ObservableCollection<string> _availableModels;

        /// <summary>
        /// 设备厂家
        /// </summary>
        public string Manufacturer
        {
            get => _manufacturer;
            set
            {
                if (SetProperty(ref _manufacturer, value))
                {
                    if (_config != null)
                    {
                        _config.Manufacturer = value;
                    }
                    
                    // 更新可用型号列表
                    UpdateAvailableModels();
                    
                    // 重置型号选择
                    Model = string.Empty;
                }
            }
        }

        /// <summary>
        /// 设备型号
        /// </summary>
        public string Model
        {
            get => _model;
            set
            {
                // 如果型号没有变化，直接返回
                if (_model == value)
                    return;

                // 保存原值
                var oldValue = _model;

                // 如果已有配置的通道，显示警告（使用报警弹窗）
                if (HasConfiguredChannels)
                {
                    var result = Astra.UI.Styles.Controls.ModernMessageBox.Show(
                        $"切换设备型号将导致通道数量改变，所有已配置的通道信息（包括传感器绑定、报警设置等）将会丢失。\n\n" +
                        $"{(string.IsNullOrWhiteSpace(_model) ? "当前未选择型号" : $"当前型号：{_model}")}\n" +
                        $"新型号：{value ?? "未选择"}\n\n" +
                        $"是否继续？",
                        "切换型号警告",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Cancel || result == MessageBoxResult.No)
                    {
                        // 用户取消或拒绝，恢复原值
                        // 由于 ComboBox 的绑定可能已经更新，我们需要强制恢复
                        _model = oldValue;
                        if (_config != null)
                        {
                            _config.Model = oldValue;
                        }
                        OnPropertyChanged(nameof(Model)); // 通知UI恢复原值
                        return;
                    }
                }

                // 用户确认，更新型号
                if (SetProperty(ref _model, value))
                {
                    if (_config != null)
                    {
                        _config.Model = value;
                    }
                    
                    // 通知规格相关属性更新
                    OnPropertyChanged(nameof(DeviceSpecification));
                    OnPropertyChanged(nameof(MaxChannels));
                    OnPropertyChanged(nameof(MaxSampleRate));
                    OnPropertyChanged(nameof(DeviceDescription));
                    
                    // 通知通道数量选项更新（根据型号限制）
                    OnPropertyChanged(nameof(ChannelCountOptions));
                    
                    // 通知采样率选项更新（根据型号限制）
                    OnPropertyChanged(nameof(SampleRateOptions));
                    
                    // 根据型号自动设置通道数
                    UpdateChannelCountFromSpecification();
                    
                    // 如果当前采样率超出范围，调整为最大值
                    if (_config != null && DeviceSpecification != null)
                    {
                        var maxRate = DeviceSpecification.GetConstraint<double>("MaxSampleRate", 51200.0);
                        if (_config.SampleRate > maxRate)
                        {
                            _config.SampleRate = maxRate;
                            OnPropertyChanged(nameof(SampleRate));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 根据设备规格更新通道数
        /// </summary>
        private void UpdateChannelCountFromSpecification()
        {
            if (_config == null || DeviceSpecification == null)
                return;

            var fixedChannels = DeviceSpecification.GetConstraint<int>("ChannelCount", -1);
            if (fixedChannels > 0)
            {
                if (_config.ChannelCount != fixedChannels)
                {
                    _config.ChannelCount = fixedChannels;
                    OnPropertyChanged(nameof(ChannelCount));
                }
                return;
            }

            var maxChannels = DeviceSpecification.GetConstraint<int>("MaxChannels", 32);
            var minChannels = DeviceSpecification.GetConstraint<int>("MinChannels", 1);
            
            // 如果当前通道数超出范围，自动调整为最大值
            if (_config.ChannelCount > maxChannels || _config.ChannelCount < minChannels)
            {
                _config.ChannelCount = maxChannels;
                OnPropertyChanged(nameof(ChannelCount));
            }
        }

        /// <summary>
        /// 可用厂家列表
        /// </summary>
        public ObservableCollection<string> AvailableManufacturers
        {
            get
            {
                if (_availableManufacturers == null)
                {
                    UpdateAvailableManufacturers();
                }
                return _availableManufacturers;
            }
        }

        /// <summary>
        /// 可用型号列表（根据选中的厂家动态更新）
        /// </summary>
        public ObservableCollection<string> AvailableModels
        {
            get
            {
                if (_availableModels == null)
                {
                    _availableModels = new ObservableCollection<string>();
                }
                return _availableModels;
            }
        }

        /// <summary>
        /// 更新可用厂家列表
        /// </summary>
        private void UpdateAvailableManufacturers()
        {
            var manufacturers = DeviceSpecificationRegistry.GetAvailableManufacturers(DeviceType.DataAcquisition)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            if (_availableManufacturers == null)
            {
                _availableManufacturers = new ObservableCollection<string>(manufacturers);
            }
            else
            {
                _availableManufacturers.Clear();
                foreach (var manufacturer in manufacturers)
                {
                    _availableManufacturers.Add(manufacturer);
                }
            }
        }

        /// <summary>
        /// 更新可用型号列表
        /// </summary>
        private void UpdateAvailableModels()
        {
            if (_availableModels == null)
            {
                _availableModels = new ObservableCollection<string>();
            }
            else
            {
                _availableModels.Clear();
            }

            if (!string.IsNullOrWhiteSpace(Manufacturer))
            {
                var models = DeviceSpecificationRegistry.GetAvailableModels(DeviceType.DataAcquisition, Manufacturer)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToList();

                foreach (var model in models)
                {
                    _availableModels.Add(model);
                }
            }

            OnPropertyChanged(nameof(AvailableModels));
        }

        /// <summary>
        /// 当前设备规格
        /// </summary>
        public IDeviceSpecification? DeviceSpecification
        {
            get => DeviceSpecificationRegistry.GetSpecification(DeviceType.DataAcquisition, Manufacturer, Model);
        }

        /// <summary>
        /// 最大通道数（根据型号动态返回）
        /// </summary>
        public int MaxChannels => DeviceSpecification?.GetConstraint<int>("MaxChannels") ?? 32;

        /// <summary>
        /// 最大采样率（根据型号动态返回）
        /// </summary>
        public double MaxSampleRate => DeviceSpecification?.GetConstraint<double>("MaxSampleRate") ?? 51200.0;

        /// <summary>
        /// 设备描述
        /// </summary>
        public string DeviceDescription => DeviceSpecification?.Description ?? "未选择设备型号";


        #endregion

        public DataAcquisitionDeviceConfigViewModel(IConfig config)
        {
            Config = config as DataAcquisitionConfig;
        }

        private void OnConfigChanged()
        {
            if (_config != null)
            {
                // 从配置初始化厂家和型号
                _manufacturer = _config.Manufacturer ?? string.Empty;
                _model = _config.Model ?? string.Empty;

                // 初始化可用厂家和型号列表
                UpdateAvailableManufacturers();
                UpdateAvailableModels();

                // 订阅配置变更事件（DeviceConfig 的 PropertyChanged）
                _config.PropertyChanged += (sender, e) =>
                {
                    // 如果配置中的厂家或型号变更，同步到 ViewModel
                    if (e.PropertyName == nameof(DeviceConfig.Manufacturer))
                    {
                        _manufacturer = _config.Manufacturer ?? string.Empty;
                        OnPropertyChanged(nameof(Manufacturer));
                        UpdateAvailableModels();
                    }
                    else if (e.PropertyName == nameof(DeviceConfig.Model))
                    {
                        _model = _config.Model ?? string.Empty;
                        OnPropertyChanged(nameof(Model));
                        OnPropertyChanged(nameof(DeviceSpecification));
                        OnPropertyChanged(nameof(MaxChannels));
                        OnPropertyChanged(nameof(MaxSampleRate));
                        OnPropertyChanged(nameof(DeviceDescription));
                        OnPropertyChanged(nameof(ChannelCountOptions));
                        OnPropertyChanged(nameof(SampleRateOptions));
                        OnPropertyChanged(nameof(ChannelCount));
                        
                        // 根据型号自动更新通道数
                        UpdateChannelCountFromSpecification();
                        
                        // 如果当前采样率超出范围，调整为最大值
                        if (DeviceSpecification != null)
                        {
                            var maxRate = DeviceSpecification.GetConstraint<double>("MaxSampleRate", 51200.0);
                            if (_config.SampleRate > maxRate)
                            {
                                _config.SampleRate = maxRate;
                                OnPropertyChanged(nameof(SampleRate));
                            }
                        }
                    }
                    else if (e.PropertyName == nameof(DataAcquisitionConfig.ChannelCount))
                    {
                        // 通道数变化时通知UI更新
                        OnPropertyChanged(nameof(ChannelCount));
                    }
                    else if (e.PropertyName == nameof(DeviceConfig.DeviceName))
                    {
                        // DeviceName 变更时通知UI更新（DeviceName 是 ConfigName 的别名）
                        OnPropertyChanged(nameof(DeviceName));
                    }
                    else
                    {
                        OnPropertyChanged(e.PropertyName);
                    }
                };

                // 订阅 ConfigBase 的 ConfigChanged 事件（监听 ConfigName 变更）
                if (_config is Astra.Core.Configuration.ConfigBase configBase)
                {
                    configBase.ConfigChanged += (sender, e) =>
                    {
                        // 当 ConfigName 变更时，同时通知 ConfigName 和 DeviceName 变更（因为 DeviceName 是 ConfigName 的别名）
                        if (e.PropertyName == nameof(Astra.Core.Configuration.ConfigBase.ConfigName))
                        {
                            OnPropertyChanged(nameof(ConfigName));
                            OnPropertyChanged(nameof(DeviceName));
                        }
                    };
                }
            }

            OnPropertyChanged(nameof(ConfigName));
            OnPropertyChanged(nameof(DeviceName));
            OnPropertyChanged(nameof(SerialNumber));
            OnPropertyChanged(nameof(SampleRate));
            OnPropertyChanged(nameof(ChannelCount));
            OnPropertyChanged(nameof(BufferSize));
            OnPropertyChanged(nameof(AutoStart));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(GroupId));
            OnPropertyChanged(nameof(SlotId));
            OnPropertyChanged(nameof(DeviceId));
            OnPropertyChanged(nameof(Manufacturer));
            OnPropertyChanged(nameof(Model));
            OnPropertyChanged(nameof(AvailableManufacturers));
            OnPropertyChanged(nameof(AvailableModels));
            OnPropertyChanged(nameof(DeviceSpecification));
            OnPropertyChanged(nameof(MaxChannels));
            OnPropertyChanged(nameof(MaxSampleRate));
            OnPropertyChanged(nameof(DeviceDescription));
            OnPropertyChanged(nameof(ChannelCountOptions));
            OnPropertyChanged(nameof(SampleRateOptions));
            OnPropertyChanged(nameof(ChannelCount));
            
            // 根据型号自动更新通道数
            UpdateChannelCountFromSpecification();
        }
    }
}

