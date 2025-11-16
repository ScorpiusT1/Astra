using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Themes
{
    /// <summary>
    /// 主题配置存储接口（由应用层实现）
    /// </summary>
    public interface IThemeConfigStorage
    {
        /// <summary>
        /// 加载保存的主题 ID
        /// </summary>
        /// <returns>主题 ID，如果未保存则返回 null 或空字符串</returns>
        string LoadThemeId();

        /// <summary>
        /// 保存主题 ID
        /// </summary>
        /// <param name="themeId">要保存的主题 ID</param>
        void SaveThemeId(string themeId);
    }
}
