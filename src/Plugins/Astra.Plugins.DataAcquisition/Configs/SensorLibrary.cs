using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Astra.Plugins.DataAcquisition.Configs
{
    /// <summary>
    /// 传感器库管理器
    /// </summary>
    public class SensorLibrary
    {
        private readonly Dictionary<string, SensorConfig> _sensors = new();

        public ObservableCollection<SensorConfig> Sensors { get; } = new ObservableCollection<SensorConfig>();

        /// <summary>添加传感器到库</summary>
        public void Add(SensorConfig sensor)
        {
            if (sensor == null)
                throw new ArgumentNullException(nameof(sensor));

            if (_sensors.ContainsKey(sensor.SensorId))
                throw new InvalidOperationException($"传感器ID {sensor.SensorId} 已存在");

            _sensors[sensor.SensorId] = sensor;
            Sensors.Add(sensor);
        }

        /// <summary>获取传感器</summary>
        public SensorConfig Get(string sensorId)
        {
            return _sensors.TryGetValue(sensorId, out var sensor) ? sensor : null;
        }

        /// <summary>移除传感器</summary>
        public bool Remove(string sensorId)
        {
            if (_sensors.TryGetValue(sensorId, out var sensor))
            {
                _sensors.Remove(sensorId);
                Sensors.Remove(sensor);
                return true;
            }
            return false;
        }

        /// <summary>按类型筛选</summary>
        public IEnumerable<SensorConfig> GetByType(SensorType type)
        {
            return Sensors.Where(s => s.SensorType == type);
        }
    }
}
