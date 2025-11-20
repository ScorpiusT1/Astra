namespace Astra.Plugins.DataAcquisition.Configs
{
    /// <summary>传感器类型</summary>
    public enum SensorType
    {
        None,
        Accelerometer,    // 加速度计
        Microphone,       // 麦克风
        Force,           // 力传感器
        Pressure,        // 压力传感器
        Displacement,    // 位移传感器
        Velocity,        // 速度传感器
        Tachometer,      // 转速传感器
        StrainGauge,     // 应变片
        Voltage,         // 电压信号
        Current,         // 电流信号
        Temperature      // 温度传感器
    }

    #region 采集卡通道配置（支持UI绑定）

    #endregion
}
