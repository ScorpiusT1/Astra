using FontAwesome.Sharp;

namespace Astra.UI.Controls
{
    /// <summary>
    /// FlowEditor 图标常量类 - 封装所有流程编辑器使用的图标
    /// 
    /// 设计原则：
    /// 1. 单一职责：集中管理所有 FlowEditor 相关的图标常量
    /// 2. 开闭原则：通过添加新的常量支持新图标，无需修改现有代码
    /// 3. 依赖倒置：使用 FontAwesome.Sharp.IconChar 枚举，不依赖具体实现
    /// 
    /// 使用说明：
    /// - 所有图标常量都是字符串类型，值为 FontAwesome IconChar 枚举的名称
    /// - 使用 nameof() 确保编译时类型安全
    /// - 通过 GetIconChar() 方法可以将字符串转换为 IconChar 枚举值
    /// </summary>
    public static class FlowEditorIcons
    {
        #region 基础节点图标

        /// <summary>
        /// 基础节点类别图标
        /// </summary>
        public const string BasicCategory = nameof(IconChar.Circle);

        /// <summary>
        /// 开始节点图标
        /// </summary>
        public const string Start = nameof(IconChar.Play);

        /// <summary>
        /// 结束节点图标
        /// </summary>
        public const string End = nameof(IconChar.Stop);

        /// <summary>
        /// 等待节点图标
        /// </summary>
        public const string Wait = nameof(IconChar.Clock);

        #endregion

        #region 逻辑节点图标

        /// <summary>
        /// 逻辑节点类别图标
        /// </summary>
        public const string LogicCategory = nameof(IconChar.CodeBranch);

        /// <summary>
        /// 条件判断节点图标
        /// </summary>
        public const string Condition = nameof(IconChar.QuestionCircle);

        /// <summary>
        /// 循环节点图标
        /// </summary>
        public const string Loop = nameof(IconChar.Sync);

        /// <summary>
        /// 并行节点图标
        /// </summary>
        public const string Parallel = nameof(IconChar.LayerGroup);

        #endregion

        #region 设备节点图标

        /// <summary>
        /// 设备节点类别图标
        /// </summary>
        public const string DeviceCategory = nameof(IconChar.Microchip);

        /// <summary>
        /// PLC控制节点图标
        /// </summary>
        public const string PLC = nameof(IconChar.Server);

        /// <summary>
        /// 扫码枪节点图标
        /// </summary>
        public const string Scanner = nameof(IconChar.Barcode);

        /// <summary>
        /// 传感器节点图标
        /// </summary>
        public const string Sensor = nameof(IconChar.TachometerAlt);

        #endregion

        #region 数据节点图标

        /// <summary>
        /// 数据节点类别图标
        /// </summary>
        public const string DataCategory = nameof(IconChar.Database);

        /// <summary>
        /// 变量节点图标
        /// </summary>
        public const string Variable = nameof(IconChar.Tag);

        /// <summary>
        /// 数据转换节点图标
        /// </summary>
        public const string Transform = nameof(IconChar.ExchangeAlt);

        /// <summary>
        /// 数据验证节点图标
        /// </summary>
        public const string Validate = nameof(IconChar.CheckCircle);

        #endregion

        #region 通信节点图标

        /// <summary>
        /// 通信节点类别图标
        /// </summary>
        public const string CommunicationCategory = nameof(IconChar.NetworkWired);

        /// <summary>
        /// TCP/IP 节点图标
        /// </summary>
        public const string TcpIp = nameof(IconChar.NetworkWired);

        /// <summary>
        /// 串口通信节点图标
        /// </summary>
        public const string SerialPort = nameof(IconChar.Plug);

        /// <summary>
        /// Modbus 节点图标
        /// </summary>
        public const string Modbus = nameof(IconChar.Sitemap);

        #endregion

        #region 工具方法

        /// <summary>
        /// 获取图标枚举值
        /// </summary>
        /// <param name="iconName">图标名称（IconChar 枚举值名称）</param>
        /// <returns>IconChar 枚举值，如果未找到则返回 IconChar.Circle</returns>
        public static IconChar GetIconChar(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
                return IconChar.Circle;

            if (System.Enum.TryParse<IconChar>(iconName, true, out var iconChar))
            {
                return iconChar;
            }

            return IconChar.Circle;
        }

        /// <summary>
        /// 获取图标名称字符串
        /// </summary>
        /// <param name="iconChar">IconChar 枚举值</param>
        /// <returns>图标名称字符串</returns>
        public static string GetIconName(IconChar iconChar)
        {
            return iconChar.ToString();
        }

        #endregion
    }
}

