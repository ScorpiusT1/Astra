using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Astra.Plugins.DataAcquisition.Configs
{
    /// <summary>
    /// 传感器轴选择（用于三轴加速度传感器）
    /// </summary>
    public enum SensorAxis
    {
        None,   // 无（单轴传感器或未选择）
        X,      // X轴
        Y,      // Y轴
        Z       // Z轴
    }

    /// <summary>
    /// 采集卡通道配置（集成硬件参数和传感器配置）
    /// </summary>
    public class DAQChannelConfig : INotifyPropertyChanged
    {
        private int _channelId;
        private string _channelName;
        private bool _enabled;
        private double _sampleRate;
        private CouplingMode _couplingMode;
        private double _gain;
        private double _offset;
        private double _icpCurrent;
        private double _triggerLevel;  // 触发电平（单位：mA）
        private bool _iepeEnabled;      // IEPE使能
        private bool _enableAntiAliasingFilter;
        private double _antiAliasingCutoff;
        private string _measurementLocation;
        private string _mountingDirection;
        private double? _coordinateX;
        private double? _coordinateY;
        private double? _coordinateZ;
        private bool _alarmEnabled;
        private double? _alarmUpperLimit;
        private double? _alarmLowerLimit;
        private string _displayColor;
        private int _displayOrder;

        // 传感器引用（可以为null表示未绑定传感器）
        private SensorConfig _sensor;

        // 传感器配置模式
        private SensorConfigMode _sensorConfigMode;

        // 选中的传感器轴（用于三轴加速度传感器）
        private SensorAxis _selectedAxis = SensorAxis.None;

        // 保存的传感器ID（用于反序列化后恢复引用）
        private string _savedSensorId;

        public event PropertyChangedEventHandler PropertyChanged;

        #region 通道基本属性

        public int ChannelId
        {
            get => _channelId;
            set => SetProperty(ref _channelId, value);
        }

        public string ChannelName
        {
            get => _channelName;
            set => SetProperty(ref _channelName, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        #endregion

        #region 采集卡硬件参数

        public double SampleRate
        {
            get => _sampleRate;
            set => SetProperty(ref _sampleRate, value);
        }

        public CouplingMode CouplingMode
        {
            get => _couplingMode;
            set => SetProperty(ref _couplingMode, value);
        }

        public double Gain
        {
            get => _gain;
            set => SetProperty(ref _gain, value);
        }

        public double Offset
        {
            get => _offset;
            set => SetProperty(ref _offset, value);
        }

        public double ICPCurrent
        {
            get => _icpCurrent;
            set => SetProperty(ref _icpCurrent, value);
        }

        /// <summary>
        /// 触发电平（单位：mA）
        /// </summary>
        public double TriggerLevel
        {
            get => _triggerLevel;
            set => SetProperty(ref _triggerLevel, value);
        }

        /// <summary>
        /// IEPE使能
        /// </summary>
        public bool IEPEEnabled
        {
            get => _iepeEnabled;
            set => SetProperty(ref _iepeEnabled, value);
        }

        public bool EnableAntiAliasingFilter
        {
            get => _enableAntiAliasingFilter;
            set => SetProperty(ref _enableAntiAliasingFilter, value);
        }

        public double AntiAliasingCutoff
        {
            get => _antiAliasingCutoff;
            set => SetProperty(ref _antiAliasingCutoff, value);
        }

        #endregion

        #region 传感器配置

        /// <summary>
        /// 传感器配置模式
        /// </summary>
        public SensorConfigMode SensorConfigMode
        {
            get => _sensorConfigMode;
            set => SetProperty(ref _sensorConfigMode, value);
        }

        /// <summary>
        /// 绑定的传感器（可为null）
        /// </summary>
        [JsonIgnore]
        public SensorConfig Sensor
        {
            get => _sensor;
            set
            {
                if (_sensor != null)
                {
                    _sensor.PropertyChanged -= Sensor_PropertyChanged;
                }

                SetProperty(ref _sensor, value);

                if (_sensor != null)
                {
                    _sensor.PropertyChanged += Sensor_PropertyChanged;

                    // 如果新传感器不是三轴加速度传感器，重置轴选择
                    if (!(_sensor.SensorType == SensorType.Accelerometer && _sensor.IsThreeAxis))
                    {
                        SelectedAxis = SensorAxis.None;
                    }
                    // 如果是三轴加速度传感器但未选择轴，默认选择X轴
                    else if (_selectedAxis == SensorAxis.None)
                    {
                        SelectedAxis = SensorAxis.X;
                    }

                    OnPropertyChanged(nameof(HasSensor));
                    OnPropertyChanged(nameof(HasAxisSelection));
                    OnPropertyChanged(nameof(SensorDisplayText));
                }
                else
                {
                    SelectedAxis = SensorAxis.None;
                    OnPropertyChanged(nameof(HasSensor));
                    OnPropertyChanged(nameof(HasAxisSelection));
                    OnPropertyChanged(nameof(SensorDisplayText));
                }
            }
        }

        /// <summary>
        /// 用于JSON序列化的传感器配置ID
        /// 始终使用 Sensor.ConfigId 作为唯一标识符（ConfigId 是 IConfig 的标准唯一标识符）
        /// 保存时：返回 Sensor.ConfigId
        /// 加载时：通过 setter 保存到 _savedSensorId，后续通过 RestoreSensorReference 方法从传感器库中查找并绑定
        /// </summary>
        public string SensorId
        {
            get
            {
                if (_sensor != null)
                {
                    // 始终使用 ConfigId 作为唯一标识符（ConfigId 是 IConfig 的标准唯一标识符）
                    if (!string.IsNullOrEmpty(_sensor.ConfigId))
                    {
                        return _sensor.ConfigId;
                    }
                }
                // 如果传感器对象不存在，返回保存的传感器ID（用于反序列化后的恢复）
                return _savedSensorId;
            }
            set
            {
                // 在反序列化时保存传感器ID，后续通过 RestoreSensorReference 方法从传感器库中查找并绑定
                _savedSensorId = value;
            }
        }

        /// <summary>
        /// 是否绑定了传感器
        /// </summary>
        [JsonIgnore]
        public bool HasSensor => _sensor != null;

        /// <summary>
        /// 选中的传感器轴（用于三轴加速度传感器）
        /// </summary>
        public SensorAxis SelectedAxis
        {
            get => _selectedAxis;
            set
            {
                if (!EqualityComparer<SensorAxis>.Default.Equals(_selectedAxis, value))
                {
                    _selectedAxis = value;
                    OnPropertyChanged(nameof(SelectedAxis));
                    OnPropertyChanged(nameof(HasAxisSelection));
                    OnPropertyChanged(nameof(SensorDisplayText));
                }
            }
        }

        /// <summary>
        /// 是否需要显示轴选择（当传感器是三轴加速度传感器时）
        /// </summary>
        [JsonIgnore]
        public bool HasAxisSelection => _sensor != null &&
                                        _sensor.SensorType == SensorType.Accelerometer &&
                                        _sensor.IsThreeAxis;

        /// <summary>
        /// 传感器显示文本（包含轴信息）
        /// </summary>
        [JsonIgnore]
        public string SensorDisplayText
        {
            get
            {
                if (_sensor == null)
                    return "未选择传感器";

                var displayText = _sensor.DisplayText;

                // 如果是三轴加速度传感器且已选择轴，显示轴信息
                if (HasAxisSelection && _selectedAxis != SensorAxis.None)
                {
                    displayText += $" ({_selectedAxis}轴)";
                }

                return displayText;
            }
        }

        private void Sensor_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 在引用模式下，传感器配置的修改需要同步更新到通道配置
            // 由于是对象引用，数据已经同步，但需要通知UI刷新
            if (_sensorConfigMode == SensorConfigMode.Reference)
            {
                OnPropertyChanged(nameof(SensorDisplayText));

                // 如果传感器类型或三轴属性改变，更新轴选择状态
                if (e.PropertyName == nameof(SensorConfig.SensorType) ||
                    e.PropertyName == nameof(SensorConfig.IsThreeAxis))
                {
                    if (!(_sensor.SensorType == SensorType.Accelerometer && _sensor.IsThreeAxis))
                    {
                        SelectedAxis = SensorAxis.None;
                    }
                    else if (_selectedAxis == SensorAxis.None)
                    {
                        SelectedAxis = SensorAxis.X;
                    }
                    OnPropertyChanged(nameof(HasAxisSelection));
                }
            }
            else
            {
                OnPropertyChanged(nameof(SensorDisplayText));

                // 独立模式下也需要处理轴选择
                if (e.PropertyName == nameof(SensorConfig.SensorType) ||
                    e.PropertyName == nameof(SensorConfig.IsThreeAxis))
                {
                    if (!(_sensor.SensorType == SensorType.Accelerometer && _sensor.IsThreeAxis))
                    {
                        SelectedAxis = SensorAxis.None;
                    }
                    else if (_selectedAxis == SensorAxis.None)
                    {
                        SelectedAxis = SensorAxis.X;
                    }
                    OnPropertyChanged(nameof(HasAxisSelection));
                }
            }
        }

        #endregion

        #region 测点位置

        public string MeasurementLocation
        {
            get => _measurementLocation;
            set => SetProperty(ref _measurementLocation, value);
        }

        public string MountingDirection
        {
            get => _mountingDirection;
            set => SetProperty(ref _mountingDirection, value);
        }

        public double? CoordinateX
        {
            get => _coordinateX;
            set => SetProperty(ref _coordinateX, value);
        }

        public double? CoordinateY
        {
            get => _coordinateY;
            set => SetProperty(ref _coordinateY, value);
        }

        public double? CoordinateZ
        {
            get => _coordinateZ;
            set => SetProperty(ref _coordinateZ, value);
        }

        #endregion

        #region 报警配置

        public bool AlarmEnabled
        {
            get => _alarmEnabled;
            set => SetProperty(ref _alarmEnabled, value);
        }

        public double? AlarmUpperLimit
        {
            get => _alarmUpperLimit;
            set => SetProperty(ref _alarmUpperLimit, value);
        }

        public double? AlarmLowerLimit
        {
            get => _alarmLowerLimit;
            set => SetProperty(ref _alarmLowerLimit, value);
        }

        #endregion

        #region 显示配置

        public string DisplayColor
        {
            get => _displayColor;
            set => SetProperty(ref _displayColor, value);
        }

        public int DisplayOrder
        {
            get => _displayOrder;
            set => SetProperty(ref _displayOrder, value);
        }

        #endregion

        public DAQChannelConfig()
        {
            _channelName = "";
            _enabled = true;
            _sampleRate = 51200;
            _couplingMode = CouplingMode.AC;
            _gain = 1.0;
            _offset = 0.0;
            _icpCurrent = 4.0;
            _triggerLevel = 0.0;  // 默认触发电平 0mA
            _iepeEnabled = false;  // 默认IEPE禁用
            _enableAntiAliasingFilter = true;
            _antiAliasingCutoff = 20000;
            _measurementLocation = "";
            _mountingDirection = "";
            _displayColor = "#FF0000";
            _displayOrder = 0;
            _sensorConfigMode = SensorConfigMode.Reference;
            _savedSensorId = null;
        }

        /// <summary>
        /// 恢复传感器引用（根据保存的传感器ID从传感器库中查找并绑定）
        /// 查找方式：通过 ConfigId 查找（SensorId 属性保存的就是 Sensor.ConfigId）
        /// </summary>
        public void RestoreSensorReference(IEnumerable<SensorConfig> availableSensors, string sensorId)
        {
            if (availableSensors == null)
                return;

            // 通过 ConfigId 查找（SensorId 属性始终保存的是 Sensor.ConfigId）
            var sensor = availableSensors.FirstOrDefault(s => s.ConfigId == sensorId);

            // 如果找到了传感器，绑定到通道
            if (sensor != null)
            {
                Sensor = sensor;
                System.Diagnostics.Debug.WriteLine($"[DAQChannelConfig] 通道 {ChannelId} 已恢复传感器引用: {sensor.ConfigName} (ConfigId: {_savedSensorId})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DAQChannelConfig] 通道 {ChannelId} 无法找到传感器 (ConfigId: {_savedSensorId})，可能已被删除");
            }
        }

        #region 传感器操作

        /// <summary>
        /// 从传感器库选择传感器（引用模式）
        /// </summary>
        public void SelectSensorFromLibrary(SensorConfig sensor)
        {
            if (sensor == null)
                throw new ArgumentNullException(nameof(sensor));

            SensorConfigMode = SensorConfigMode.Reference;
            Sensor = sensor;
        }

        /// <summary>
        /// 创建传感器副本（独立模式）- 修改不影响库
        /// </summary>
        public void CreateSensorCopy(SensorConfig sourceSensor)
        {
            if (sourceSensor == null)
                throw new ArgumentNullException(nameof(sourceSensor));

            SensorConfigMode = SensorConfigMode.Independent;
            Sensor = (SensorConfig)sourceSensor.Clone();
            // 为新克隆的传感器设置新的 ConfigId（统一使用 ConfigId 作为唯一标识符）
            Sensor.ConfigId = $"{sourceSensor.ConfigId}_Copy_{ChannelId}";
        }

        /// <summary>
        /// 清除传感器绑定
        /// </summary>
        public void ClearSensor()
        {
            Sensor = null;
        }

        /// <summary>
        /// 快速设置传感器参数（用于UI直接编辑）
        /// </summary>
        public void SetQuickSensorParams(string name, SensorType type, double sensitivity, string unit)
        {
            if (Sensor == null)
            {
                SensorConfigMode = SensorConfigMode.Independent;
                Sensor = new SensorConfig();
            }

            Sensor.ConfigName = name;
            Sensor.SensorType = type;
            Sensor.Sensitivity = sensitivity;
            Sensor.SensitivityUnit = unit;
        }

        #endregion

        #region 数据转换

        /// <summary>
        /// 将原始电压转换为物理量
        /// </summary>
        public double ConvertToPhysical(double voltage)
        {
            if (!HasSensor)
                return voltage;

            // 应用硬件参数
            double adjustedVoltage = (voltage - Offset) / Gain;

            // 应用传感器灵敏度
            double physicalValue = (adjustedVoltage / Sensor.Sensitivity) * Sensor.CalibrationFactor;

            return physicalValue;
        }

        #endregion

        #region 验证

        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(ChannelName))
                result.AddWarning("通道名称为空");

            if (SampleRate <= 0)
                result.AddError("采样率必须大于0");

            if (EnableAntiAliasingFilter && AntiAliasingCutoff >= SampleRate / 2)
                result.AddWarning($"抗混叠滤波器截止频率应小于奈奎斯特频率({SampleRate / 2} Hz)");

            if (!HasSensor)
                result.AddWarning("未配置传感器");
            else if (Sensor.Sensitivity == 0)
                result.AddError("传感器灵敏度不能为0");

            if (AlarmEnabled && !AlarmUpperLimit.HasValue && !AlarmLowerLimit.HasValue)
                result.AddWarning("启用了报警但未设置报警限值");

            return result;
        }

        #endregion

        protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"Ch{ChannelId}: {ChannelName} - {SensorDisplayText}";
        }
    }
}
