using Python.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonWrapper.Utils
{
    internal static class PythonTypeConverter
    {
        // ─────────────────────────────────────────────────
        // C# → Python（入参转换）
        // ─────────────────────────────────────────────────
        public static PyObject ToPython(object value)
        {
            if (value is null)
                return PyObject.None;

            // 基础类型：直接转
            if (value is string)
            {
                return new PyString((string)value);
            }
            if (value is bool)
            {
                return ((bool)value).ToPython();
            }
            if (value is int)
            {
                return new PyInt((int)value);
            }
            if (value is long)
            {
                return new PyInt((long)value);
            }
            if (value is float)
            {
                return new PyFloat((float)value);
            }
            if (value is double)
            {
                return new PyFloat((double)value);
            }
            if (value is decimal)
            {
                return new PyFloat((double)(decimal)value);
            }

            // 数组：转 PyList
            if (value is double[])
            {
                return ToFloatList((double[])value);
            }
            if (value is float[])
            {
                return ToFloatList(((float[])value).Select(x => (double)x));
            }
            if (value is int[])
            {
                return ToIntList(((int[])value).Select(x => (long)x));
            }
            if (value is long[])
            {
                return ToIntList((long[])value);
            }
            if (value is string[])
            {
                return ToStringList((string[])value);
            }
            if (value is bool[])
            {
                return ToBoolList((bool[])value);
            }
            if (value is object[])
            {
                return ToObjectList((object[])value);
            }

            // IEnumerable<T>
            if (value is IEnumerable<double>)
            {
                return ToFloatList((IEnumerable<double>)value);
            }
            if (value is IEnumerable<int>)
            {
                return ToIntList(((IEnumerable<int>)value).Select(x => (long)x));
            }
            if (value is IEnumerable<string>)
            {
                return ToStringList((IEnumerable<string>)value);
            }
            if (value is IEnumerable<object>)
            {
                return ToObjectList((IEnumerable<object>)value);
            }

            // 字典：转 PyDict
            if (value is IDictionary<string, object>)
            {
                return ToDict((IDictionary<string, object>)value);
            }
            if (value is IDictionary)
            {
                return ToDict((IDictionary)value);
            }

            // 已经是 PyObject：直接用
            if (value is PyObject)
            {
                return (PyObject)value;
            }

            // 兜底：尝试用 PythonNet 内置转换
            return value.ToPython();
        }

        private static PyList ToFloatList(IEnumerable<double> src)
        {
            var list = new PyList();
            foreach (var v in src)
            {
                using (var item = new PyFloat(v))
                {
                    list.Append(item);
                }
            }
            return list;
        }

        private static PyList ToIntList(IEnumerable<long> src)
        {
            var list = new PyList();
            foreach (var v in src)
            {
                using (var item = new PyInt(v))
                {
                    list.Append(item);
                }
            }
            return list;
        }

        private static PyList ToStringList(IEnumerable<string> src)
        {
            var list = new PyList();
            foreach (var v in src)
            {
                using (var item = new PyString(v))
                {
                    list.Append(item);
                }
            }
            return list;
        }

        private static PyList ToBoolList(IEnumerable<bool> src)
        {
            var list = new PyList();
            foreach (var v in src)
            {
                using (var item = v.ToPython())
                {
                    list.Append(item);
                }
            }
            return list;
        }

        private static PyList ToObjectList(IEnumerable<object> src)
        {
            var list = new PyList();
            foreach (var v in src)
            {
                using (var item = ToPython(v))
                {
                    list.Append(item);
                }
            }
            return list;
        }

        private static PyDict ToDict(IDictionary<string, object> src)
        {
            var dict = new PyDict();
            foreach (var kv in src)
            {
                using (var k = new PyString(kv.Key))
                using (var v = ToPython(kv.Value))
                {
                    dict.SetItem(k, v);
                }
            }
            return dict;
        }

        private static PyDict ToDict(IDictionary src)
        {
            var dict = new PyDict();
            foreach (DictionaryEntry kv in src)
            {
                using (var k = ToPython(kv.Key?.ToString()))
                using (var v = ToPython(kv.Value))
                {
                    dict.SetItem(k, v);
                }
            }
            return dict;
        }

        // ─────────────────────────────────────────────────
        // Python → C#（返回值转换）
        // ─────────────────────────────────────────────────
        public static T FromPython<T>(PyObject pyObj)
        {
            return (T)FromPython(pyObj, typeof(T));
        }

        public static object FromPython(PyObject pyObj, Type targetType)
        {
            if (pyObj is null || pyObj.IsNone()) return null;

            // 目标类型是 PyObject（不转换，调用方自己处理，需在 GIL 内）
            if (targetType == typeof(PyObject)) return pyObj;

            // 目标类型是 object / dynamic：自动推断
            if (targetType == typeof(object)) return AutoConvert(pyObj);

            // 目标类型是基础类型
            if (targetType == typeof(string)) return pyObj.ToString();
            if (targetType == typeof(int)) return (int)new PyInt(pyObj).ToInt32();
            if (targetType == typeof(long)) return new PyInt(pyObj).ToInt64();
            if (targetType == typeof(double)) return new PyFloat(pyObj).As<double>();
            if (targetType == typeof(float)) return (float)new PyFloat(pyObj).As<double>();
            if (targetType == typeof(bool)) return pyObj.IsTrue();

            // 目标类型是数组
            if (targetType == typeof(double[]))
                return ConvertToList(pyObj).Select(x => (double)x).ToArray();
            if (targetType == typeof(int[]))
                return ConvertToList(pyObj).Select(x => (int)x).ToArray();
            if (targetType == typeof(string[]))
                return ConvertToList(pyObj).Select(x => x?.ToString()).ToArray();

            // 目标类型是 List<T>
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemType = targetType.GetGenericArguments()[0];
                var result = (IList)Activator.CreateInstance(targetType);
                foreach (var item in ConvertToList(pyObj))
                {
                    result.Add(Convert.ChangeType(item, elemType));
                }
                return result;
            }

            // 目标类型是 Dictionary<string,object>
            if (targetType == typeof(Dictionary<string, object>))
                return ConvertToDict(pyObj);

            // 兜底：让 PythonNet 尝试转换
            return pyObj.As<object>();
        }

        // 自动推断 Python 对象的 C# 类型
        public static object AutoConvert(PyObject obj)
        {
            if (obj.IsNone()) return null;

            // 按 Python 类型名判断
            string typeName = obj.GetPythonType().Name;
            switch (typeName)
            {
                case "str":
                    return obj.ToString();
                case "int":
                    return new PyInt(obj).ToInt64();
                case "float":
                    return new PyFloat(obj).As<double>();
                case "bool":
                    return obj.IsTrue();
                case "list":
                    return ConvertToList(obj);
                case "dict":
                    return ConvertToDict(obj);
                case "NoneType":
                    return null;
                case "float64":
                case "float32":
                    return new PyFloat(obj).As<double>();
                case "int64":
                case "int32":
                    return new PyInt(obj).ToInt64();
                default:
                    // 其他：转字符串兜底
                    return obj.ToString();
            }
        }

        private static List<object> ConvertToList(PyObject pyList)
        {
            var result = new List<object>();
            using (var list = new PyList(pyList))
            {
                for (int i = 0; i < list.Length(); i++)
                {
                    using (var item = list.GetItem(i))
                    {
                        result.Add(AutoConvert(item));
                    }
                }
            }
            return result;
        }

        private static Dictionary<string, object> ConvertToDict(PyObject pyDict)
        {
            var result = new Dictionary<string, object>();
            using (var dict = new PyDict(pyDict))
            {
                using (var keys = dict.Keys())
                {
                    foreach (PyObject key in keys)
                    {
                        using (key)
                        using (var value = dict.GetItem(key))
                        {
                            result[key.ToString()] = AutoConvert(value);
                        }
                    }
                }
            }
            return result;
        }

        // ── 新增：PyObject → double[][] ───────────────────────
        /// <summary>
        /// 将 Python list[list[float]] 转为 C# double[][]（锯齿数组）
        /// ⭐ 必须在持有 GIL 的专用线程内调用
        /// </summary>
        public static double[][] ToJaggedDoubleArray(PyObject pyObj)
        {
            using (var outerList = new PyList(pyObj))
            {
                var outer = new double[outerList.Length()][];

                for (int i = 0; i < outerList.Length(); i++)
                {
                    using (var innerObj = outerList.GetItem(i))
                    using (var innerList = new PyList(innerObj))
                    {
                        var row = new double[innerList.Length()];

                        for (int j = 0; j < innerList.Length(); j++)
                        {
                            using (var item = innerList.GetItem(j))
                            {
                                using (var pyFloat = new PyFloat(item))
                                {
                                    row[j] = pyFloat.As<double>();
                                }
                            }
                        }

                        outer[i] = row;
                    }
                }

                return outer;
            }
        }
    }
}
