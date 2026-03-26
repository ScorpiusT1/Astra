using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonWrapper.Attributes
{
    /// <summary>标注此 C# 类可从 Python 对象自动映射</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class PyMappedAttribute : Attribute
    {
        public string PythonClassName { get; }
        public PyMappedAttribute(string pythonClassName = "") =>
            PythonClassName = pythonClassName;
    }

    /// <summary>指定该属性对应 Python 端的字段名</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PyPropertyAttribute : Attribute
    {
        public string Name { get; }
        public PyPropertyAttribute(string name) => Name = name;
    }

    /// <summary>标注此属性在映射时忽略</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PyIgnoreAttribute : Attribute { }
}
