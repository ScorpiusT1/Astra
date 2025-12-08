using Astra.UI.Abstractions.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.UI.Services
{
    /// <summary>
    /// 内置主题定义
    /// </summary>
    public static class BuiltInThemes
    {
        /// <summary>
        /// 浅色主题
        /// </summary>
        public static ITheme Light { get; } = new BuiltInTheme(
            id: "Light",
            displayName: "浅色主题",
            description: "明亮的浅色界面，适合白天使用",
            resourcePath: "/Themes/Light/Colors.xaml",
            icon: "💡"
        );

        /// <summary>
        /// 深色主题
        /// </summary>
        public static ITheme Dark { get; } = new BuiltInTheme(
            id: "Dark",
            displayName: "深色主题",
            description: "护眼的深色界面，适合夜间使用",
            resourcePath: "/Themes/Dark/Colors.xaml",
            icon: "🌙"
        );

        /// <summary>
        /// 蓝色科技主题
        /// </summary>
        public static ITheme Blue { get; } = new BuiltInTheme(
            id: "Blue",
            displayName: "蓝色科技主题",
            description: "科技感的蓝色界面，适合工业环境",
            resourcePath: "/Themes/Blue/Colors.xaml",
            icon: "🔵"
        );
    }
}
