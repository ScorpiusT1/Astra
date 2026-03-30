using System;
using System.Collections.Generic;
using FontAwesome.Sharp;

namespace Astra.UI
{
    /// <summary>
    /// 将清单/配置中的图标名字符串解析为 <see cref="IconChar"/>。
    /// 对部分在较旧 FontAwesome.Sharp 中缺失的 FA6 名称提供别名，避免回退为默认圆点。
    /// </summary>
    public static class FontAwesomeIconResolver
    {
        /// <summary>
        /// 名称 → 实际用于解析的 <see cref="IconChar"/> 成员名（须在当前包中存在）。
        /// </summary>
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // wave-square：部分 FontAwesome.Sharp 版本未生成 WaveSquare 枚举成员
            ["WaveSquare"] = nameof(IconChar.FileWaveform),
            // 插件清单用名；若当前包无对应枚举则走别名
            ["FileImport"] = nameof(IconChar.FileArrowDown),
            ["FileExport"] = nameof(IconChar.FileArrowUp),
        };

        /// <summary>
        /// 解析为图标枚举；无法识别时返回 <see cref="IconChar.Circle"/>。
        /// </summary>
        public static IconChar Resolve(string? iconCode)
        {
            if (string.IsNullOrWhiteSpace(iconCode))
                return IconChar.Circle;

            var trimmed = iconCode.Trim();
            if (Enum.TryParse<IconChar>(trimmed, true, out var direct))
                return direct;

            if (Aliases.TryGetValue(trimmed, out var targetName) &&
                Enum.TryParse<IconChar>(targetName, true, out var aliased))
                return aliased;

            return IconChar.Circle;
        }

        /// <summary>
        /// 是否为可识别的 FontAwesome 图标名（含别名），用于可见性等逻辑。
        /// </summary>
        public static bool IsKnownIconName(string? iconCode)
        {
            if (string.IsNullOrWhiteSpace(iconCode))
                return false;
            var trimmed = iconCode.Trim();
            if (Enum.TryParse<IconChar>(trimmed, true, out _))
                return true;
            return Aliases.ContainsKey(trimmed);
        }
    }
}
