using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Astra.Core.Configuration
{
    public class ConfigHelper
    {
        public static string BaseConfigDirectory => Path.Combine(AppContext.BaseDirectory, "Configs");

        public static string DeviceConfigDirectory => Path.Combine(BaseConfigDirectory, "Devices");

        static ConfigHelper()
        {
            if (!Directory.Exists(BaseConfigDirectory))
            {
                Directory.CreateDirectory(BaseConfigDirectory);
            }

            if (!Directory.Exists(DeviceConfigDirectory))
            {
                Directory.CreateDirectory(DeviceConfigDirectory);
            }
        }

        public static async Task<T[]?> LoadManyAsync<T>(string fileName, CancellationToken token)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"Configuration file not found: {fileName}");
            }

            string json = await File.ReadAllTextAsync(fileName, token);
            return JsonSerializer.Deserialize<T[]>(json);
        }

        public static async Task<T?> LoadAsync<T>(string fileName, CancellationToken token)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"Configuration file not found: {fileName}");
            }

            string json = await File.ReadAllTextAsync(fileName, token);
            return JsonSerializer.Deserialize<T>(json);
        }

        public static async Task<bool> SaveManyAsync<T>(string fileName, T[] configs, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(fileName, json, token);
            return true;
        }

        public static async Task<bool> SaveAsync<T>(string fileName, T config, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(fileName, json, token);
            return true;
        }
    }
}
