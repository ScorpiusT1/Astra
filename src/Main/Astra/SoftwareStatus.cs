namespace Astra
{
    /// <summary>
    /// 软件运行状态，用于主界面状态指示灯的颜色与提示。
    /// </summary>
    public enum SoftwareStatus
    {
        /// <summary>运行中（绿色）</summary>
        Running,

        /// <summary>连接中 / 忙碌（青色）</summary>
        Connecting,

        /// <summary>警告（橙色）</summary>
        Warning,

        /// <summary>错误（红色）</summary>
        Error
    }
}
