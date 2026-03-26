using Python.Runtime;
using PythonWrapper.Attributes;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace PythonWrapper.Utils
{
    /// <summary>
    /// PyObject → [PyMapped] C# 自定义类 通用映射引擎
    /// ⭐ 所有方法必须在持有 GIL 的专用线程内调用
    /// </summary>
    public static class PythonObjectMapper
    {
        // 反射结果缓存（避免每次调用重复反射，提升性能）
        private static readonly ConcurrentDictionary<Type, PropertyMapInfo[]>
            _cache = new ConcurrentDictionary<Type, PropertyMapInfo[]>();

        // ── 主入口 ────────────────────────────────────────
        public static T Map<T>(PyObject pyObj) => (T)Map(pyObj, typeof(T));

        public static object Map(PyObject pyObj, Type targetType)
        {
            if (pyObj is null || pyObj.IsNone())
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            // [PyMapped] 自定义类：反射映射
            if (targetType.GetCustomAttribute<PyMappedAttribute>() != null)
                return MapObject(pyObj, targetType);

            // 其他类型：交给 PythonTypeConverter
            return PythonTypeConverter.FromPython(pyObj, targetType);
        }

        // ── 映射自定义类 ──────────────────────────────────
        private static object MapObject(PyObject pyObj, Type type)
        {
            var instance = Activator.CreateInstance(type);
            if (instance == null)
                throw new InvalidOperationException("无法创建目标类型实例: " + type.FullName);
            var maps = GetPropertyMaps(type);

            foreach (var map in maps)
            {
                using (var pyValue = GetPyField(pyObj, map.PythonName))
                {
                    if (pyValue is null) continue;

                    try
                    {
                        var csValue = Map(pyValue, map.Property.PropertyType);
                        map.Property.SetValue(instance, csValue);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[PythonMapper] 字段 '{map.PythonName}' 映射失败: {ex.Message}");
                    }
                }
            }

            return instance;
        }

        // ── 从 PyObject（dict 或 object）读取字段值 ───────
        private static PyObject GetPyField(PyObject pyObj, string key)
        {
            try
            {
                if (PyDict.IsDictType(pyObj))
                {
                    using (var dict = new PyDict(pyObj))
                    {
                        using (var k = new PyString(key))
                        {
                            return dict.HasKey(k) ? dict.GetItem(k) : null;
                        }
                    }
                }
                return pyObj.HasAttr(key) ? pyObj.GetAttr(key) : null;
            }
            catch { return null; }
        }

        // ── 反射缓存：解析类的属性映射表 ─────────────────
        private static PropertyMapInfo[] GetPropertyMaps(Type type)
        {
            return _cache.GetOrAdd(type, t =>
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Where(p => p.CanWrite &&
                             p.GetCustomAttribute<PyIgnoreAttribute>() == null)
                 .Select(p =>
                 {
                     var attr = p.GetCustomAttribute<PyPropertyAttribute>();
                     var pythonName = attr?.Name ?? ToSnakeCase(p.Name);
                     return new PropertyMapInfo(p, pythonName);
                 })
                 .ToArray());
        }

        private static string ToSnakeCase(string name)
        {
            return System.Text.RegularExpressions.Regex
                .Replace(name, "(?<=[a-z0-9])([A-Z])", "_$1")
                .ToLower();
        }

        private sealed class PropertyMapInfo
        {
            public PropertyInfo Property { get; private set; }
            public string PythonName { get; private set; }

            public PropertyMapInfo(PropertyInfo property, string pythonName)
            {
                Property = property;
                PythonName = pythonName;
            }
        }
    }
}
