using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Astra.Plugins.DataAcquisition.Configs
{
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
        private double _voltageRange;
        private double _gain;
        private double _offset;
        private double _icpCurrent;
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

        public double VoltageRange
        {
            get => _voltageRange;
            set => SetProperty(ref _voltageRange, value);
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
                    OnPropertyChanged(nameof(HasSensor));
                    OnPropertyChanged(nameof(SensorDisplayText));
                }
            }
        }

        /// <summary>
        /// 用于JSON序列化的传感器ID
        /// </summary>
        public string SensorId
        {
            get => _sensor?.SensorId;
            set { } // 由加载时设置
        }

        /// <summary>
        /// 是否绑定了传感器
        /// </summary>
        [JsonIgnore]
        public bool HasSensor => _sensor != null;

        /// <summary>
        /// 传感器显示文本（用于UI）
        /// </summary>
        [JsonIgnore]
        public string SensorDisplayText => _sensor?.DisplayText ?? "未选择传感器";

        private void Sensor_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 传感器属性变化时通知UI更新
            OnPropertyChanged(nameof(SensorDisplayText));
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
            _voltageRange = 10.0;
            _gain = 1.0;
            _offset = 0.0;
            _icpCurrent = 4.0;
            _enableAntiAliasingFilter = true;
            _antiAliasingCutoff = 20000;
            _measurementLocation = "";
            _mountingDirection = "";
            _displayColor = "#FF0000";
            _displayOrder = 0;
            _sensorConfigMode = SensorConfigMode.Reference;
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
            Sensor.SensorId = $"{sourceSensor.SensorId}_Copy_{ChannelId}";
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

            Sensor.SensorName = name;
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

            if (VoltageRange <= 0)
                result.AddError("电压量程必须大于0");

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
