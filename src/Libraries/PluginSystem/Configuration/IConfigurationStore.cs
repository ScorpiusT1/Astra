using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Addins.Configuration
{
    /// <summary>
    /// 配置存储抽象
    /// </summary>
    public interface IConfigurationStore
    {
        T Get<T>(string key, T defaultValue = default);
        void Set<T>(string key, T value);
        void Save();
        void Load();
    }

    public class ConfigurationStore : IConfigurationStore
    {
        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly string _filePath;

        public ConfigurationStore(string filePath = "plugin-config.json")
        {
            _filePath = filePath;
            Load();
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (_cache.TryGetValue(key, out var value))
            {
                if (value is JsonElement element)
                {
                    return JsonSerializer.Deserialize<T>(element.GetRawText());
                }
                return (T)value;
            }
            return defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            _cache[key] = value;
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_filePath, json);
        }

        public void Load()
        {
            if (!File.Exists(_filePath))
                return;

            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<ConcurrentDictionary<string, JsonElement>>(json);

            foreach (var kvp in data)
            {
                _cache[kvp.Key] = kvp.Value;
            }
        }
    }
}
