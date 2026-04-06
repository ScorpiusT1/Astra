using Astra.Core.Configuration;
using Astra.Plugins.DataAcquisition.Converters;
using Astra.Plugins.DataAcquisition.ViewModels;
using Astra.Plugins.DataAcquisition.Views;
using Astra.UI.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PhysicalUnitEnum = Astra.Plugins.DataAcquisition.Configs.PhysicalUnit;

namespace Astra.Plugins.DataAcquisition.Configs
{
    public enum SensorConversionMode
    {
        /// <summary>物理量 = 测试值 / 灵敏度</summary>
        DivideBySensitivity,

        /// <summary>物理量 = 测试值 × 灵敏度</summary>
        MultiplyBySensitivity
    }

    #region 传感器配置（支持UI绑定）

    /// <summary>
    /// 传感器配置（使用 ObservableObject 处理属性变更通知）
    /// </summary>
    [TreeNodeConfig("传感器", "📡", typeof(SensorConfigView), typeof(SensorConfigViewModel))]
    [ConfigUI(typeof(SensorConfigView), typeof(SensorConfigViewModel))]
    public class SensorConfig : ConfigBase, ICloneable
    {
        private SensorType _sensorType;
        private string _manufacturer;
        private string _model;
        private string _serialNumber;
        private double _sensitivity;
        private string _sensitivityUnit;
        // 三轴灵敏度
        private double _sensitivityX;
        private double _sensitivityY;
        private double _sensitivityZ;
        private string _sensitivityUnitX;
        private string _sensitivityUnitY;
        private string _sensitivityUnitZ;
        private string _physicalUnit;
        private double _measurementRangeMin;
        private double _measurementRangeMax;
        private double _frequencyRangeMin;
        private double _frequencyRangeMax;
        private double _calibrationFactor;
        private DateTime? _calibrationDate;
        private DateTime? _nextCalibrationDate;
        private string _notes;
        private bool _isThreeAxis; // 是否为三轴传感器（仅对加速度计有效）
        private bool _isUpdatingSensorType; // 防止循环更新的标志
        private SensorConversionMode _conversionMode; // 传感器转换方式
        private double _unitConversionFactor;         // 单位换算系数（例如 G->m/s²）

        #region 属性

        public SensorType SensorType
        {
            get => _sensorType;
            set
            {
                if (EqualityComparer<SensorType>.Default.Equals(_sensorType, value))
                    return;

                // 设置更新标志，防止循环更新
                _isUpdatingSensorType = true;
                
                try
                {
                    var oldType = _sensorType;
                    _sensorType = value;
                    
                    // 当传感器类型改变时，如果不是加速度计，则重置 IsThreeAxis
                    // 直接修改字段，避免触发 setter 导致循环
                    bool isThreeAxisChanged = false;
                    if (value != SensorType.Accelerometer && _isThreeAxis)
                    {
                        _isThreeAxis = false;
                        isThreeAxisChanged = true;
                    }
                    
                    // 根据传感器类型自动设置默认单位
                    if (oldType != value && value != SensorType.None)
                    {
                        UpdateUnitsForSensorType(value);
                    }
                    
                    // 触发属性变更通知
                    OnPropertyChanged(nameof(SensorType));
                    
                    // 如果 IsThreeAxis 改变了，通知属性变更
                    if (isThreeAxisChanged)
                    {
                        OnPropertyChanged(nameof(IsThreeAxis));
                    }
                }
                finally
                {
                    _isUpdatingSensorType = false;
                }
                
                // 在标志重置后更新计算属性，避免循环
                // 注意：这些是只读计算属性，不会触发 setter，所以是安全的
                OnPropertyChanged(nameof(IsThreeAxisSensor));
                OnPropertyChanged(nameof(SupportsAxisSelection));
            }
        }

        /// <summary>
        /// 根据传感器类型更新默认单位
        /// </summary>
        private void UpdateUnitsForSensorType(SensorType sensorType)
        {
            switch (sensorType)
            {
                case SensorType.Accelerometer:
                    // 加速度计：默认使用 mV/g，物理单位 G
                    _sensitivityUnit = "mV/g";
                    // 同步更新三轴单位
                    _sensitivityUnitX = _sensitivityUnit;
                    _sensitivityUnitY = _sensitivityUnit;
                    _sensitivityUnitZ = _sensitivityUnit;
                    _physicalUnit = PhysicalUnitEnum.G.ToString();
                    break;

                case SensorType.Microphone:
                    // 麦克风：默认使用 mV/Pa，物理单位 Pascal
                    _sensitivityUnit = "mV/Pa";
                    _physicalUnit = PhysicalUnitEnum.Pascal.ToString();
                    break;

                case SensorType.Force:
                    // 力传感器：默认使用 mV/N，物理单位 Newton
                    _sensitivityUnit = "mV/N";
                    _physicalUnit = PhysicalUnitEnum.Newton.ToString();
                    break;

                case SensorType.Pressure:
                    // 压力传感器：默认使用 mV/Pa，物理单位 Pascal
                    _sensitivityUnit = "mV/Pa";
                    _physicalUnit = PhysicalUnitEnum.Pascal.ToString();
                    break;

                case SensorType.Displacement:
                    // 位移传感器：默认使用 mV/V，物理单位 MilliMeter
                    _sensitivityUnit = "mV/V";
                    _physicalUnit = PhysicalUnitEnum.MilliMeter.ToString();
                    break;

                case SensorType.Velocity:
                    // 速度传感器：默认使用 mV/V，物理单位 MeterPerSecond
                    _sensitivityUnit = "mV/V";
                    _physicalUnit = PhysicalUnitEnum.MeterPerSecond.ToString();
                    break;

                case SensorType.Tachometer:
                    // 转速传感器：默认使用 mV/V，物理单位 RPM
                    _sensitivityUnit = "mV/V";
                    _physicalUnit = PhysicalUnitEnum.RPM.ToString();
                    break;

                case SensorType.StrainGauge:
                    // 应变片：默认使用 mV/V，物理单位 MicroStrain
                    _sensitivityUnit = "mV/V";
                    _physicalUnit = PhysicalUnitEnum.MicroStrain.ToString();
                    break;

                case SensorType.Voltage:
                    // 电压信号：默认使用 V/V，物理单位 Volt
                    _sensitivityUnit = "V/V";
                    _physicalUnit = PhysicalUnitEnum.Volt.ToString();
                    break;

                case SensorType.Current:
                    // 电流信号：默认使用 V/V，物理单位 Ampere
                    _sensitivityUnit = "V/V";
                    _physicalUnit = PhysicalUnitEnum.Ampere.ToString();
                    break;

                case SensorType.Temperature:
                    // 温度传感器：默认使用 mV/V，物理单位 Celsius
                    _sensitivityUnit = "mV/V";
                    _physicalUnit = PhysicalUnitEnum.Celsius.ToString();
                    break;

                default:
                    // 其他类型保持默认值
                    break;
            }

            // 通知单位属性变更
            OnPropertyChanged(nameof(SensitivityUnit));
            OnPropertyChanged(nameof(SensitivityUnitX));
            OnPropertyChanged(nameof(SensitivityUnitY));
            OnPropertyChanged(nameof(SensitivityUnitZ));
            OnPropertyChanged(nameof(PhysicalUnit));
        }

        /// <summary>
        /// 根据灵敏度单位（以及可选的传感器类型）推断物理单位
        /// 例如：mV/g、pC/g、V/g → G；mV/Pa、pC/Pa、V/Pa → Pascal；mV/N、pC/N、V/N → Newton
        /// 对于 mV/V、V/V 这类比值，需要结合传感器类型才能判断（位移、速度、转速、应变等）
        /// 返回值为 PhysicalUnit 的枚举名称字符串（如 "G"、"Pascal"），无法判断时返回 null
        /// </summary>
        public static string GetPhysicalUnitFromSensitivityUnit(string sensitivityUnit, SensorType? sensorType = null)
        {
            if (string.IsNullOrWhiteSpace(sensitivityUnit))
                return null;

            var unit = sensitivityUnit.Trim();

            // 直接根据灵敏度单位分母判断
            if (unit.EndsWith("/g", StringComparison.OrdinalIgnoreCase))
                return PhysicalUnitEnum.G.ToString();

            if (unit.EndsWith("/Pa", StringComparison.OrdinalIgnoreCase))
                return PhysicalUnitEnum.Pascal.ToString();

            if (unit.EndsWith("/N", StringComparison.OrdinalIgnoreCase))
                return PhysicalUnitEnum.Newton.ToString();

            // mV/V、V/V 等比值类型，需要结合传感器类型判断
            if (sensorType.HasValue)
            {
                switch (sensorType.Value)
                {
                    case SensorType.Accelerometer:
                        return PhysicalUnitEnum.G.ToString();
                    case SensorType.Microphone:
                    case SensorType.Pressure:
                        return PhysicalUnitEnum.Pascal.ToString();
                    case SensorType.Force:
                        return PhysicalUnitEnum.Newton.ToString();
                    case SensorType.Displacement:
                        return PhysicalUnitEnum.MilliMeter.ToString();
                    case SensorType.Velocity:
                        return PhysicalUnitEnum.MeterPerSecond.ToString();
                    case SensorType.Tachometer:
                        return PhysicalUnitEnum.RPM.ToString();
                    case SensorType.StrainGauge:
                        return PhysicalUnitEnum.MicroStrain.ToString();
                    case SensorType.Voltage:
                        return PhysicalUnitEnum.Volt.ToString();
                    case SensorType.Current:
                        return PhysicalUnitEnum.Ampere.ToString();
                    case SensorType.Temperature:
                        return PhysicalUnitEnum.Celsius.ToString();
                    default:
                        break;
                }
            }

            return null;
        }

        /// <summary>
        /// 图表、TDMS 纵轴等与灵敏度换算结果一致的物理量单位：优先 <see cref="PhysicalUnit"/>，
        /// 否则由 <see cref="SensitivityUnit"/> 与 <see cref="SensorType"/> 推断。
        /// </summary>
        public string GetYAxisDisplayUnit()
        {
            if (!string.IsNullOrWhiteSpace(PhysicalUnit))
                return PhysicalUnit.Trim();
            return GetPhysicalUnitFromSensitivityUnit(SensitivityUnit, SensorType) ?? string.Empty;
        }

        /// <summary>
        /// TDMS 波形 Y 轴人可读单位（标准属性 <c>unit_string</c>），与 <see cref="GetYAxisDisplayUnit"/> 对应。
        /// 能解析为 <see cref="PhysicalUnit"/> 时返回中文习惯写法，否则回退为 <see cref="GetYAxisDisplayUnit"/> 原文。
        /// </summary>
        public string GetYAxisDisplayUnitLocalizedString()
        {
            var code = GetYAxisDisplayUnit();
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;
            var trimmed = code.Trim();
            if (Enum.TryParse<PhysicalUnitEnum>(trimmed, ignoreCase: true, out var u))
                return PhysicalUnitToDisplayString(u);
            return trimmed;
        }

        private static string PhysicalUnitToDisplayString(PhysicalUnitEnum u) => u switch
        {
            PhysicalUnitEnum.Volt => "伏",
            PhysicalUnitEnum.MilliVolt => "毫伏",
            PhysicalUnitEnum.Ampere => "安",
            PhysicalUnitEnum.MilliAmpere => "毫安",
            PhysicalUnitEnum.MeterPerSecond2 => "米/秒²",
            PhysicalUnitEnum.G => "g",
            PhysicalUnitEnum.Pascal => "帕",
            PhysicalUnitEnum.Newton => "牛",
            PhysicalUnitEnum.Meter => "米",
            PhysicalUnitEnum.MilliMeter => "毫米",
            PhysicalUnitEnum.MeterPerSecond => "米/秒",
            PhysicalUnitEnum.RPM => "转/分",
            PhysicalUnitEnum.Decibel => "分贝",
            PhysicalUnitEnum.Celsius => "摄氏度",
            PhysicalUnitEnum.MicroStrain => "微应变",
            _ => u.ToString()
        };

        public string Manufacturer
        {
            get => _manufacturer;
            set => SetProperty(ref _manufacturer, value);
        }

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public string SerialNumber
        {
            get => _serialNumber;
            set => SetProperty(ref _serialNumber, value);
        }

        public double Sensitivity
        {
            get => _sensitivity;
            set => SetProperty(ref _sensitivity, value);
        }

        public string SensitivityUnit
        {
            get => _sensitivityUnit;
            set => SetProperty(ref _sensitivityUnit, value);
        }

        #region 三轴灵敏度属性

        /// <summary>X轴灵敏度</summary>
        public double SensitivityX
        {
            get => _sensitivityX;
            set => SetProperty(ref _sensitivityX, value);
        }

        /// <summary>Y轴灵敏度</summary>
        public double SensitivityY
        {
            get => _sensitivityY;
            set => SetProperty(ref _sensitivityY, value);
        }

        /// <summary>Z轴灵敏度</summary>
        public double SensitivityZ
        {
            get => _sensitivityZ;
            set => SetProperty(ref _sensitivityZ, value);
        }

        /// <summary>X轴灵敏度单位</summary>
        public string SensitivityUnitX
        {
            get => _sensitivityUnitX;
            set => SetProperty(ref _sensitivityUnitX, value);
        }

        /// <summary>Y轴灵敏度单位</summary>
        public string SensitivityUnitY
        {
            get => _sensitivityUnitY;
            set => SetProperty(ref _sensitivityUnitY, value);
        }

        /// <summary>Z轴灵敏度单位</summary>
        public string SensitivityUnitZ
        {
            get => _sensitivityUnitZ;
            set => SetProperty(ref _sensitivityUnitZ, value);
        }

        #endregion

        [JsonConverter(typeof(PhysicalUnitJsonConverter))]
        public string PhysicalUnit
        {
            get => _physicalUnit;
            set => SetProperty(ref _physicalUnit, value);
        }

        public double MeasurementRangeMin
        {
            get => _measurementRangeMin;
            set => SetProperty(ref _measurementRangeMin, value);
        }

        public double MeasurementRangeMax
        {
            get => _measurementRangeMax;
            set => SetProperty(ref _measurementRangeMax, value);
        }

        public double FrequencyRangeMin
        {
            get => _frequencyRangeMin;
            set => SetProperty(ref _frequencyRangeMin, value);
        }

        public double FrequencyRangeMax
        {
            get => _frequencyRangeMax;
            set => SetProperty(ref _frequencyRangeMax, value);
        }

        public double CalibrationFactor
        {
            get => _calibrationFactor;
            set => SetProperty(ref _calibrationFactor, value);
        }

        /// <summary>转换方式：测试值与灵敏度的关系</summary>
        public SensorConversionMode ConversionMode
        {
            get => _conversionMode;
            set => SetProperty(ref _conversionMode, value);
        }

        /// <summary>单位换算系数（例如 G→m/s² 时为 9.81）</summary>
        public double UnitConversionFactor
        {
            get => _unitConversionFactor;
            set => SetProperty(ref _unitConversionFactor, value);
        }

        public DateTime? CalibrationDate
        {
            get => _calibrationDate;
            set => SetProperty(ref _calibrationDate, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        /// <summary>用于UI显示的描述文本</summary>
        [JsonIgnore]
        public string DisplayText => $"{ConfigName} ({Model} S/N:{SerialNumber})";

        /// <summary>是否为三轴传感器（仅对加速度计有效）</summary>
        public bool IsThreeAxis
        {
            get => _isThreeAxis;
            set
            {
                // 如果正在更新 SensorType，忽略此更新（由 SensorType setter 统一处理）
                if (_isUpdatingSensorType)
                {
                    return;
                }
                
                // 如果当前不是加速度计，不允许设置为三轴
                if (value && SensorType != SensorType.Accelerometer)
                {
                    // 如果值已经是 false，直接返回，避免不必要的更新
                    if (!_isThreeAxis)
                    {
                        return;
                    }
                    // 否则强制设置为 false
                    value = false;
                }
                
                // 如果值没有改变，直接返回
                if (EqualityComparer<bool>.Default.Equals(_isThreeAxis, value))
                {
                    return;
                }
                
                _isThreeAxis = value;
                OnPropertyChanged(nameof(IsThreeAxis));
                
                // 当 IsThreeAxis 改变时，更新 IsThreeAxisSensor
                OnPropertyChanged(nameof(IsThreeAxisSensor));
            }
        }

        /// <summary>是否为三轴传感器（用于UI显示判断）</summary>
        [JsonIgnore]
        public bool IsThreeAxisSensor => SensorType == SensorType.Accelerometer && IsThreeAxis;

        /// <summary>是否支持轴数选择（仅加速度计支持）</summary>
        [JsonIgnore]
        public bool SupportsAxisSelection => SensorType == SensorType.Accelerometer;

        #endregion

        public SensorConfig()
        {
            // 注意：不在这里设置 ConfigId，让 JSON 反序列化器或基类处理
            // 如果反序列化时 JSON 中没有 ConfigId，会在反序列化完成后由其他机制设置
            // 这样确保反序列化时 ConfigId 不会被覆盖

            _sensorType = SensorType.None;
            _manufacturer = "";
            _model = "";
            _serialNumber = "";
            _sensitivity = 1.0;
            _sensitivityUnit = "mV/V";
            // 初始化三轴灵敏度
            _sensitivityX = 1.0;
            _sensitivityY = 1.0;
            _sensitivityZ = 1.0;
            _sensitivityUnitX = "mV/V";
            _sensitivityUnitY = "mV/V";
            _sensitivityUnitZ = "mV/V";
            _physicalUnit = "mV/V";
            _measurementRangeMin = 0;
            _measurementRangeMax = 100;
            _frequencyRangeMin = 1;
            _frequencyRangeMax = 10000;
            _calibrationFactor = 1.0;
            _conversionMode = SensorConversionMode.DivideBySensitivity;
            _unitConversionFactor = 1.0;
            _notes = "";
            _isThreeAxis = false; // 默认为单轴
        }

        /// <summary>
        /// 用于动态创建对象
        /// </summary>
        /// <param name="configId"></param>
        public SensorConfig(string configId) : this()
        {
            this.ConfigId = configId;
        }


        /// <summary>
        /// 获取配置的显示名称（用于树节点等 UI 显示）
        /// 要求：设备厂家名 + 设备型号（序号由外层追加）
        /// </summary>
        public override string GetDisplayName()
        {
            // 仅使用厂家和型号构成基础名称，例如：“B&K 4507B”
            // 如果两者都为空，则回退到“未命名传感器”
            return ConfigDisplayNameHelper.BuildDisplayName(
                Manufacturer,
                Model,
                serialNumber: null,
                configName: null,
                defaultName: "未命名传感器");
        }

        public override string ToString() => DisplayText;

        /// <summary>
        /// 克隆配置（使用序列化方法实现深拷贝，确保所有属性正确复制）
        /// </summary>
        public override IConfig Clone()
        {
            // 使用基类的序列化方法实现深拷贝
            var json = Serialize();
            var clone = Deserialize<SensorConfig>(json);

            // 重置配置ID和元数据
            if (clone != null)
            {
                clone.SetConfigId(Guid.NewGuid().ToString());
            }

            return clone;
        }

        object ICloneable.Clone()
        {
            return Clone() as ICloneable;
        }
    }


    #endregion
}


