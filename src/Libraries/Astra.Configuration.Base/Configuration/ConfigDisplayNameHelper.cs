namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置显示名称辅助工具
    /// </summary>
    public static class ConfigDisplayNameHelper
    {
        public static string BuildDisplayName(string manufacturer, string model, string serialNumber, string configName, string defaultName)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(manufacturer)) parts.Add(manufacturer);
            if (!string.IsNullOrWhiteSpace(model)) parts.Add(model);
            if (!string.IsNullOrWhiteSpace(serialNumber)) parts.Add(serialNumber);

            var baseName = string.Join(" ", parts);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = string.IsNullOrWhiteSpace(configName) ? defaultName : configName;
            }
            else if (!string.IsNullOrWhiteSpace(configName))
            {
                baseName = $"{baseName} - {configName}";
            }

            return baseName;
        }
    }
}

