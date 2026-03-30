namespace Astra.Core.Configuration.Helpers
{
    /// <summary>
    /// 配置集合文件名生成器（不含 .json 扩展名）。
    /// 规则：程序集名（库名） + "." + 业务类型名 + ".config"。
    /// 示例：Astra.Plugins.PLC.Configs.PlcTriggerConfig -> Astra.Plugins.PLC.Trigger.config
    /// </summary>
    public static class ConfigFileNameHelper
    {
        public static string GetDefaultCollectionFileName(Type configType)
        {
            if (configType == null) throw new ArgumentNullException(nameof(configType));

            var libraryName = configType.Assembly.GetName().Name ?? "Unknown";
            var businessTypeName = NormalizeBusinessTypeName(configType, libraryName);
            return $"{libraryName}.{businessTypeName}.config";
        }

        private static string NormalizeBusinessTypeName(Type configType, string libraryName)
        {
            var typeName = configType.Name;
            const string configSuffix = "Config";
            if (typeName.EndsWith(configSuffix, StringComparison.Ordinal))
            {
                typeName = typeName[..^configSuffix.Length];
            }

            // 若类型名前缀与库末段一致（如 PlcTrigger / Astra.Plugins.PLC），去掉前缀，保留业务名 Trigger。
            var libraryTail = libraryName.Split('.').LastOrDefault();
            if (!string.IsNullOrWhiteSpace(libraryTail) &&
                typeName.StartsWith(libraryTail, StringComparison.OrdinalIgnoreCase) &&
                typeName.Length > libraryTail.Length)
            {
                typeName = typeName[libraryTail.Length..];
            }

            return string.IsNullOrWhiteSpace(typeName) ? configType.Name : typeName;
        }
    }
}
