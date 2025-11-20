namespace Astra.Plugins.DataAcquisition.Configs
{
    #region 采集卡通道配置（支持UI绑定）

    /// <summary>
    /// 传感器配置模式
    /// </summary>
    public enum SensorConfigMode
    {
        /// <summary>引用模式 - 引用传感器库中的传感器，修改会影响库</summary>
        Reference,

        /// <summary>独立模式 - 通道独立的传感器配置，修改不影响库</summary>
        Independent
    }

    #endregion
}
