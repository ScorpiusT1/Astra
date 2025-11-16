namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志分类枚举
    /// 定义日志的不同类别，用于分类管理和过滤
    /// </summary>
    public enum LogCategory
    {
        /// <summary>
        /// 系统日志 - 记录系统运行相关的信息
        /// </summary>
        System,

        /// <summary>
        /// 节点日志 - 记录节点执行相关的信息
        /// </summary>
        Node,

        /// <summary>
        /// 设备日志 - 记录设备交互相关的信息
        /// </summary>
        Device,

        /// <summary>
        /// 网络日志 - 记录网络通信相关的信息
        /// </summary>
        Network,

        /// <summary>
        /// 数据库日志 - 记录数据库操作相关的信息
        /// </summary>
        Database,

        /// <summary>
        /// 性能日志 - 记录性能相关的信息
        /// </summary>
        Performance,

        /// <summary>
        /// 安全日志 - 记录安全相关的信息
        /// </summary>
        Security,

        /// <summary>
        /// 自定义日志 - 用户自定义的日志分类
        /// </summary>
        Custom
    }
}
