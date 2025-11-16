using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Themes
{
    /// <summary>
    /// 主题改变事件参数
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 旧主题
        /// </summary>
        public ITheme OldTheme { get; }

        /// <summary>
        /// 新主题
        /// </summary>
        public ITheme NewTheme { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ThemeChangedEventArgs(ITheme oldTheme, ITheme newTheme)
        {
            OldTheme = oldTheme;
            NewTheme = newTheme;
        }
    }
}
