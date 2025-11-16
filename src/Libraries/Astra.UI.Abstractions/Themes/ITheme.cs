using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Abstractions.Themes
{
    /// <summary>
    /// 主题定义接口
    /// </summary>
    public interface ITheme
    {
        /// <summary>
        /// 主题唯一标识
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 主题显示名称
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 主题描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 主题资源 URI（相对于控件库程序集）
        /// </summary>
        Uri ResourceUri { get; }

        /// <summary>
        /// 主题图标（Emoji 或图标字符）
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// 是否为内置主题
        /// </summary>
        bool IsBuiltIn { get; }
    }
}
