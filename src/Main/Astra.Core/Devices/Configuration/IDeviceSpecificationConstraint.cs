namespace Astra.Core.Devices.Configuration
{
    /// <summary>
    /// 设备规格约束接口（用于应用设备规格限制）
    /// </summary>
    public interface IDeviceSpecificationConstraint
    {
        /// <summary>
        /// 应用设备规格约束
        /// </summary>
        void ApplyConstraints(Specifications.IDeviceSpecification specification);
    }
}

