using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Devices.Specifications
{
    /// <summary>
    /// 设备规格注册表（通用，支持所有设备类型）
    /// </summary>
    public static class DeviceSpecificationRegistry
    {
        private static readonly Dictionary<(DeviceType, string, string), IDeviceSpecification> 
            _specifications = new();

        /// <summary>
        /// 注册设备规格
        /// </summary>
        public static void Register(IDeviceSpecification specification)
        {
            if (specification == null)
                throw new ArgumentNullException(nameof(specification));

            var key = (specification.DeviceType, specification.Manufacturer, specification.Model);
            _specifications[key] = specification;
        }

        /// <summary>
        /// 批量注册设备规格
        /// </summary>
        public static void RegisterRange(IEnumerable<IDeviceSpecification> specifications)
        {
            foreach (var spec in specifications)
            {
                Register(spec);
            }
        }

        /// <summary>
        /// 获取设备规格
        /// </summary>
        public static IDeviceSpecification? GetSpecification(
            DeviceType deviceType, 
            string manufacturer, 
            string model)
        {
            if (string.IsNullOrWhiteSpace(manufacturer) || string.IsNullOrWhiteSpace(model))
            {
                return null;
            }

            _specifications.TryGetValue((deviceType, manufacturer, model), out var spec);
            return spec;
        }

        /// <summary>
        /// 获取指定设备类型和厂家的所有可用型号
        /// </summary>
        public static IEnumerable<string> GetAvailableModels(
            DeviceType deviceType, 
            string manufacturer)
        {
            if (string.IsNullOrWhiteSpace(manufacturer))
            {
                return Enumerable.Empty<string>();
            }

            return _specifications
                .Where(kvp => kvp.Key.Item1 == deviceType && kvp.Key.Item2 == manufacturer)
                .Select(kvp => kvp.Key.Item3)
                .Distinct()
                .OrderBy(m => m);
        }

        /// <summary>
        /// 获取指定设备类型的所有可用厂家
        /// </summary>
        public static IEnumerable<string> GetAvailableManufacturers(DeviceType deviceType)
        {
            return _specifications
                .Where(kvp => kvp.Key.Item1 == deviceType)
                .Select(kvp => kvp.Key.Item2)
                .Distinct()
                .OrderBy(m => m);
        }

        /// <summary>
        /// 获取指定设备类型的所有规格
        /// </summary>
        public static IEnumerable<IDeviceSpecification> GetSpecifications(DeviceType deviceType)
        {
            return _specifications
                .Where(kvp => kvp.Key.Item1 == deviceType)
                .Select(kvp => kvp.Value)
                .OrderBy(s => s.Manufacturer)
                .ThenBy(s => s.Model);
        }

        /// <summary>
        /// 清除所有规格（主要用于测试）
        /// </summary>
        public static void Clear()
        {
            _specifications.Clear();
        }
    }
}

