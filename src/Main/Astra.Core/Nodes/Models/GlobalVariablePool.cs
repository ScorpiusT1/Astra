using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 全局变量池
    /// 所有流程共享的变量集合
    /// 符合单一职责原则：专门负责全局变量的管理
    /// </summary>
    public class GlobalVariablePool
    {
        public GlobalVariablePool()
        {
            Variables = new Dictionary<string, GlobalVariable>();
        }

        /// <summary>
        /// 全局变量字典（变量名 -> 变量对象）
        /// </summary>
        [JsonPropertyOrder(1)]
        public Dictionary<string, GlobalVariable> Variables { get; set; }

        /// <summary>
        /// 添加或更新全局变量
        /// </summary>
        public void SetVariable(string name, object value, string dataType = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("变量名不能为空", nameof(name));

            if (Variables.ContainsKey(name))
            {
                Variables[name].Value = value;
                Variables[name].ModifiedAt = DateTime.Now;
            }
            else
            {
                Variables[name] = new GlobalVariable
                {
                    Name = name,
                    Value = value,
                    DataType = dataType ?? (value?.GetType().Name ?? "object"),
                    CreatedAt = DateTime.Now,
                    ModifiedAt = DateTime.Now
                };
            }
        }

        /// <summary>
        /// 获取全局变量值
        /// </summary>
        public object GetVariable(string name)
        {
            if (Variables.TryGetValue(name, out var variable))
                return variable.Value;
            return null;
        }

        /// <summary>
        /// 获取全局变量（带类型转换）
        /// </summary>
        public T GetVariable<T>(string name, T defaultValue = default(T))
        {
            var value = GetVariable(name);
            if (value == null)
                return defaultValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 移除全局变量
        /// </summary>
        public bool RemoveVariable(string name)
        {
            return Variables.Remove(name);
        }

        /// <summary>
        /// 检查变量是否存在
        /// </summary>
        public bool HasVariable(string name)
        {
            return Variables.ContainsKey(name);
        }

        /// <summary>
        /// 清空所有变量
        /// </summary>
        public void Clear()
        {
            Variables.Clear();
        }

        /// <summary>
        /// 获取所有变量名
        /// </summary>
        public IEnumerable<string> GetVariableNames()
        {
            return Variables.Keys;
        }
    }

    /// <summary>
    /// 全局变量对象
    /// </summary>
    public class GlobalVariable
    {
        /// <summary>
        /// 变量名
        /// </summary>
        [JsonPropertyOrder(1)]
        public string Name { get; set; }

        /// <summary>
        /// 变量值
        /// </summary>
        [JsonPropertyOrder(2)]
        public object Value { get; set; }

        /// <summary>
        /// 数据类型名称
        /// </summary>
        [JsonPropertyOrder(3)]
        public string DataType { get; set; }

        /// <summary>
        /// 变量描述
        /// </summary>
        [JsonPropertyOrder(4)]
        public string Description { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonPropertyOrder(5)]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        [JsonPropertyOrder(6)]
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// 是否只读
        /// </summary>
        [JsonPropertyOrder(7)]
        public bool IsReadOnly { get; set; }
    }
}

