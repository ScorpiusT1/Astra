using Astra.Core.Configuration;
using Astra.Plugins.DataAcquisition.Commons;
using Astra.Plugins.DataAcquisition.ViewModels;
using Astra.Plugins.DataAcquisition.Views;
using Astra.UI.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Astra.Plugins.DataAcquisition.Configs
{
    #region 传感器配置（支持UI绑定）

    /// <summary>
    /// 传感器配置（支持INotifyPropertyChanged）
    /// </summary>
    [TreeNodeConfig("传感器", "📡", typeof(SensorManagementView), typeof(SensorManagementViewModel))]
    public class SensorConfig :BaseConfig, INotifyPropertyChanged, ICloneable
    {
        private string _sensorId;
        private string _sensorName;
        private SensorType _sensorType;
        private string _manufacturer;
        private string _model;
        private string _serialNumber;
        private double _sensitivity;
        private string _sensitivityUnit;
        private string _physicalUnit;
        private double _measurementRangeMin;
        private double _measurementRangeMax;
        private double _frequencyRangeMin;
        private double _frequencyRangeMax;
        private double _calibrationFactor;
        private DateTime? _calibrationDate;
        private DateTime? _nextCalibrationDate;
        private string _calibrationCertificate;
        private string _status;
        private string _notes;

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ConfigType
        {
            get => "SensorConfig"; 
        }

        #region 属性

        public string SensorId
        {
            get => _sensorId;
            set => SetProperty(ref _sensorId, value);
        }

        public string SensorName
        {
            get => _sensorName;
            set => SetProperty(ref _sensorName, value);
        }

        public SensorType SensorType
        {
            get => _sensorType;
            set => SetProperty(ref _sensorType, value);
        }

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

        public DateTime? CalibrationDate
        {
            get => _calibrationDate;
            set => SetProperty(ref _calibrationDate, value);
        }

        public DateTime? NextCalibrationDate
        {
            get => _nextCalibrationDate;
            set => SetProperty(ref _nextCalibrationDate, value);
        }

        public string CalibrationCertificate
        {
            get => _calibrationCertificate;
            set => SetProperty(ref _calibrationCertificate, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        /// <summary>用于UI显示的描述文本</summary>
        [JsonIgnore]
        public string DisplayText => $"{SensorName} ({Model} S/N:{SerialNumber})";

        /// <summary>是否需要校准（警告提示）</summary>
        [JsonIgnore]
        public bool NeedsCalibration => NextCalibrationDate.HasValue && DateTime.Now > NextCalibrationDate.Value;

        #endregion

        public SensorConfig()
        {
            ConfigId = Guid.NewGuid().ToString();
            _sensorId = Guid.NewGuid().ToString();
            _sensorName = "";
            _sensorType = SensorType.None;
            _manufacturer = "";
            _model = "";
            _serialNumber = "";
            _sensitivity = 1.0;
            _sensitivityUnit = "mV/V";
            _physicalUnit = "mV/V";
            _measurementRangeMin = 0;
            _measurementRangeMax = 100;
            _frequencyRangeMin = 1;
            _frequencyRangeMax = 10000;
            _calibrationFactor = 1.0;
            _status = "Normal";
            _notes = "";
            _calibrationCertificate = "";
        }

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

        public override string ToString() => DisplayText;

        public override IConfig Clone()
        {
            return this.MemberwiseClone() as IConfig;
        }

        object ICloneable.Clone()
        {
            return Clone() as ICloneable;
        }
    }
    }

    #endregion

