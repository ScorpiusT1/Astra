namespace Astra.Core.Devices.Serialization
{
    /// <summary>
    /// 配置序列化接口
    /// </summary>
    public interface IConfigSerializer
    {
        /// <summary>
        /// 序列化对象为字符串
        /// </summary>
        string Serialize<T>(T obj);

        /// <summary>
        /// 反序列化字符串为对象
        /// </summary>
        T Deserialize<T>(string json);
    }

    /// <summary>
    /// JSON 配置序列化器（使用 System.Text.Json）
    /// </summary>
    public class JsonConfigSerializer : IConfigSerializer
    {
        private readonly System.Text.Json.JsonSerializerOptions _options;

        public JsonConfigSerializer(bool writeIndented = true)
        {
            _options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = writeIndented
            };
        }

        public string Serialize<T>(T obj)
        {
            return System.Text.Json.JsonSerializer.Serialize(obj, _options);
        }

        public T Deserialize<T>(string json)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, _options);
        }
    }
}

