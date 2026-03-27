using Astra.Core.Foundation.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Astra.Plugins.PLC.Configs
{
    /// <summary>
    /// 单个 IO 点位模型（可在 IOConfig 中成批管理）。
    /// </summary>
    public partial class IoPointModel : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string plcDeviceName = string.Empty;

        [ObservableProperty]
        private string address = string.Empty;

        [ObservableProperty]
        private PlcIODataType dataType = PlcIODataType.Auto;

        [ObservableProperty]
        private string outputKey = string.Empty;

        [ObservableProperty]
        private bool isEnabled = true;

        [ObservableProperty]
        private double scale = 1.0;

        [ObservableProperty]
        private double offset = 0.0;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string pipeLabel = string.Empty;

        [ObservableProperty]
        private string tag = string.Empty;

        [ObservableProperty]
        private bool alarmEnabled;

        /// <summary>
        /// 下限阈值（仅数值类型有效）。
        /// </summary>
        [ObservableProperty]
        private double? lowLimit;

        /// <summary>
        /// 上限阈值（仅数值类型有效）。
        /// </summary>
        [ObservableProperty]
        private double? highLimit;

        /// <summary>
        /// 当前数据类型是否支持 Scale/Offset。
        /// Bool/String 等非数值类型不支持。
        /// </summary>
        public bool SupportsScaleOffset =>
            DataType is PlcIODataType.Auto
            or PlcIODataType.Int16
            or PlcIODataType.UInt16
            or PlcIODataType.Int32
            or PlcIODataType.UInt32
            or PlcIODataType.Float
            or PlcIODataType.Double;

        /// <summary>
        /// 当前数据类型是否支持上下限阈值（报警）。
        /// </summary>
        public bool SupportsLimits => SupportsScaleOffset;

        public string DisplayAddress => string.IsNullOrWhiteSpace(Address) ? "—" : Address;
        public string DisplayPipeLabel => string.IsNullOrWhiteSpace(PipeLabel) ? "—" : PipeLabel;
        public string DisplayTag => string.IsNullOrWhiteSpace(Tag) ? "—" : Tag;

        /// <summary>
        /// 便于列表快速识别配置是否完整（名称/地址/数据类型/标签至少有基础信息）。
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Name) &&
            !string.IsNullOrWhiteSpace(Address) &&
            !string.IsNullOrWhiteSpace(Tag);

        partial void OnDataTypeChanged(PlcIODataType value)
        {
            OnPropertyChanged(nameof(SupportsScaleOffset));
            OnPropertyChanged(nameof(SupportsLimits));
            OnPropertyChanged(nameof(IsConfigured));

            // 非数值类型自动归一化，避免出现无意义配置
            if (!SupportsScaleOffset)
            {
                Scale = 1.0;
                Offset = 0.0;
                AlarmEnabled = false;
                LowLimit = null;
                HighLimit = null;
            }
        }

        partial void OnNameChanged(string value) => OnPropertyChanged(nameof(IsConfigured));
        partial void OnAddressChanged(string value)
        {
            OnPropertyChanged(nameof(DisplayAddress));
            OnPropertyChanged(nameof(IsConfigured));
        }

        partial void OnPipeLabelChanged(string value) => OnPropertyChanged(nameof(DisplayPipeLabel));

        partial void OnTagChanged(string value)
        {
            OnPropertyChanged(nameof(DisplayTag));
            OnPropertyChanged(nameof(IsConfigured));
        }

        public OperationResult<bool> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("IO名称不能为空");
            }

            if (string.IsNullOrWhiteSpace(Address))
            {
                errors.Add("Address 不能为空");
            }

            if (AlarmEnabled)
            {
                if (!SupportsLimits)
                {
                    errors.Add("当前数据类型不支持上下限报警");
                }

                if (LowLimit.HasValue && HighLimit.HasValue && LowLimit.Value > HighLimit.Value)
                {
                    errors.Add("下限不能大于上限");
                }
            }

            return errors.Count > 0
                ? OperationResult<bool>.Failure(string.Join(Environment.NewLine, errors))
                : OperationResult<bool>.Succeed(true);
        }

        public bool TryApplyScaleOffset(object? rawValue, out object? result)
        {
            result = rawValue;
            if (rawValue is null)
            {
                return true;
            }

            if (!SupportsScaleOffset)
            {
                return true;
            }

            if (Math.Abs(Scale - 1.0) < 1e-12 && Math.Abs(Offset) < 1e-12)
            {
                return true;
            }

            try
            {
                if (rawValue is double d)
                {
                    result = d * Scale + Offset;
                    return true;
                }
                if (rawValue is float f)
                {
                    result = f * (float)Scale + (float)Offset;
                    return true;
                }
                if (rawValue is int i)
                {
                    result = i * Scale + Offset;
                    return true;
                }
                if (rawValue is long l)
                {
                    result = l * Scale + Offset;
                    return true;
                }
                if (rawValue is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    result = parsed * Scale + Offset;
                    return true;
                }
            }
            catch
            {
            }

            result = rawValue;
            return false;
        }

        public IoPointModel CreateSnapshot()
        {
            return new IoPointModel
            {
                Name = Name,
                PlcDeviceName = PlcDeviceName,
                Address = Address,
                DataType = DataType,
                OutputKey = OutputKey,
                IsEnabled = IsEnabled,
                Scale = Scale,
                Offset = Offset,
                Description = Description,
                PipeLabel = PipeLabel,
                Tag = Tag,
                AlarmEnabled = AlarmEnabled,
                LowLimit = LowLimit,
                HighLimit = HighLimit
            };
        }

        public void RestoreFrom(IoPointModel snapshot)
        {
            Name = snapshot.Name;
            PlcDeviceName = snapshot.PlcDeviceName;
            Address = snapshot.Address;
            DataType = snapshot.DataType;
            OutputKey = snapshot.OutputKey;
            IsEnabled = snapshot.IsEnabled;
            Scale = snapshot.Scale;
            Offset = snapshot.Offset;
            Description = snapshot.Description;
            PipeLabel = snapshot.PipeLabel;
            Tag = snapshot.Tag;
            AlarmEnabled = snapshot.AlarmEnabled;
            LowLimit = snapshot.LowLimit;
            HighLimit = snapshot.HighLimit;
        }
    }
}

