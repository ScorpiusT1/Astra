using Astra.Core.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Validation.ValidationRules
{
    /// <summary>
    /// 安全性验证器
    /// </summary>
    public class SecurityValidator : IValidationRule
    {
        private readonly string[] _dangerousTypes = new[]
        {
            "System.Reflection.Assembly",
            "System.AppDomain",
            "System.Diagnostics.Process",
            "Microsoft.Win32.Registry"
        };

        private readonly string[] _dangerousNamespaces = new[]
        {
            "System.Runtime.InteropServices",
            "System.Security.Principal"
        };

        public async Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor)
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // 加载程序集进行检查（不执行代码）
                var assembly = Assembly.LoadFrom(descriptor.AssemblyPath);

                // 检查是否使用了危险类型
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    // 检查字段
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var field in fields)
                    {
                        if (IsDangerousType(field.FieldType))
                        {
                            if (!descriptor.Permissions.HasFlag(GetRequiredPermission(field.FieldType)))
                            {
                                result.IsValid = false;
                                result.Errors.Add($"Type {type.Name} uses dangerous type {field.FieldType.Name} without permission");
                            }
                        }
                    }

                    // 检查方法
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        if (method.GetMethodBody() == null) continue;

                        // 检查方法参数和返回类型
                        if (IsDangerousType(method.ReturnType))
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Method {type.Name}.{method.Name} returns dangerous type");
                        }
                    }
                }

                // 检查程序集引用
                var references = assembly.GetReferencedAssemblies();
                foreach (var reference in references)
                {
                    if (IsDangerousAssembly(reference.Name))
                    {
                        // 警告但不阻止
                        Console.WriteLine($"Warn: Plugin references potentially dangerous assembly: {reference.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Security validation failed: {ex.Message}");
            }

            return result;
        }

        private bool IsDangerousType(Type type)
        {
            if (type == null) return false;

            var fullName = type.FullName ?? type.Name;

            return _dangerousTypes.Any(dt => fullName.Contains(dt)) ||
                   _dangerousNamespaces.Any(ns => fullName.StartsWith(ns));
        }

        private bool IsDangerousAssembly(string assemblyName)
        {
            var dangerous = new[] { "System.Management", "System.DirectoryServices" };
            return dangerous.Any(d => assemblyName.StartsWith(d));
        }

        private PluginPermissions GetRequiredPermission(Type type)
        {
            var fullName = type.FullName ?? type.Name;

            if (fullName.Contains("Process"))
                return PluginPermissions.NativeCode;

            if (fullName.Contains("Registry"))
                return PluginPermissions.Registry;

            if (fullName.Contains("File") || fullName.Contains("Directory"))
                return PluginPermissions.FileSystem;

            if (fullName.Contains("Socket") || fullName.Contains("Http"))
                return PluginPermissions.Network;

            return PluginPermissions.None;
        }
    }
}
