using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astra.Plugins.DataAcquisition.Configs;

namespace Astra.Plugins.DataAcquisition.Commons
{
    /// <summary>
    /// PhysicalUnit 的 JSON 转换器，支持从枚举值或字符串反序列化
    /// </summary>
    public class PhysicalUnitJsonConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                // 直接返回字符串值，无论是枚举名称还是其他字符串
                return value;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                // 如果是数字，尝试转换为枚举再转为字符串
                var enumValue = reader.GetInt32();
                if (Enum.IsDefined(typeof(PhysicalUnit), enumValue))
                {
                    return ((PhysicalUnit)enumValue).ToString();
                }
                // 如果数字不在枚举范围内，返回数字的字符串表示
                return enumValue.ToString();
            }
            
            // 其他类型，返回空字符串
            return string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value);
            }
        }
    }
}

