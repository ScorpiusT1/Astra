using System.ComponentModel;

namespace Astra.Plugins.Algorithms.Enums
{
    /// <summary>
    /// 窗函数类型
    /// </summary>
    public enum WindowType
    {
        [Description("无")]
        None = 0,

        [Description("汉宁窗")]
        Hanning,

        [Description("力窗")]
        ForceWindow,

        [Description("平顶窗")]
        FlatTop,

        [Description("凯撒窗")]
        Kaiser,
    }
}
