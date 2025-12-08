using Astra.UI.Services;
using Astra.Utilities;
using System;
using System.Diagnostics;

namespace Astra.Services.Startup
{
    /// <summary>
    /// 主题初始化服务 - 负责主题系统的初始化
    /// </summary>
    public class ThemeInitializationService
    {
        /// <summary>
        /// 初始化主题系统
        /// </summary>
        public void Initialize()
        {
            try
            {
                ThemeManager.Instance.Configure(
                    configStorage: new SettingsThemeStorage(),
                    autoLoad: true
                );

                ApplyDefaultTheme();

                Debug.WriteLine($"[ThemeInitializationService] ✅ 主题管理器初始化成功，当前主题: {ThemeManager.Instance.CurrentTheme?.DisplayName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThemeInitializationService] ❌ 主题管理器初始化失败: {ex.Message}");
                ApplyDefaultTheme();
            }
        }

        private void ApplyDefaultTheme()
        {
            ThemeManager.Instance.ApplyTheme(BuiltInThemes.Light);
        }
    }
}
