using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Themes
{
    /// <summary>
    /// 主题提供者接口
    /// </summary>
    public interface IThemeProvider
    {
        /// <summary>
        /// 获取所有可用主题
        /// </summary>
        IEnumerable<ITheme> GetAvailableThemes();

        /// <summary>
        /// 根据 ID 获取主题
        /// </summary>
        /// <param name="themeId">主题 ID</param>
        /// <returns>主题对象，不存在则返回 null</returns>
        ITheme GetTheme(string themeId);

        /// <summary>
        /// 注册自定义主题
        /// </summary>
        /// <param name="theme">主题对象</param>
        void RegisterTheme(ITheme theme);

        /// <summary>
        /// 注销主题
        /// </summary>
        /// <param name="themeId">主题 ID</param>
        /// <returns>是否注销成功</returns>
        bool UnregisterTheme(string themeId);
    }
}
