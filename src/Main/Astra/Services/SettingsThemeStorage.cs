using Astra.UI.Abstractions.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Services
{
    public class SettingsThemeStorage : IThemeConfigStorage
    {
        public string LoadThemeId()
        {
            try
            {
                return Properties.Settings.Default.Theme;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsThemeStorage] 加载主题失败: {ex.Message}");
                return string.Empty;
            }
        }

        public void SaveThemeId(string themeId)
        {
            try
            {
                Properties.Settings.Default.Theme = themeId;
                Properties.Settings.Default.Save();
                System.Diagnostics.Debug.WriteLine($"[SettingsThemeStorage] 保存主题: {themeId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsThemeStorage] 保存主题失败: {ex.Message}");
            }
        }
    }
}
