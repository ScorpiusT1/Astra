using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Manifest
{
    /// <summary>
    /// 清单文件验证器
    /// </summary>
    public class ManifestValidator
    {
        public ValidationResult Validate(AddinManifest manifest)
        {
            var result = new ValidationResult { IsValid = true };
            var errors = new List<string>();

            // 验证基本信息
            if (string.IsNullOrWhiteSpace(manifest.Addin.Id))
                errors.Add("Addin ID is required");

            if (string.IsNullOrWhiteSpace(manifest.Addin.Name))
                errors.Add("Addin Name is required");

            if (string.IsNullOrWhiteSpace(manifest.Addin.Version))
                errors.Add("Addin Version is required");
            else if (!Version.TryParse(manifest.Addin.Version, out _))
                errors.Add($"Invalid version format: {manifest.Addin.Version}");

            // 验证运行时信息
            if (manifest.Addin.Runtime == null)
            {
                errors.Add("Runtime information is required");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(manifest.Addin.Runtime.Assembly))
                    errors.Add("Runtime Assembly is required");

                if (string.IsNullOrWhiteSpace(manifest.Addin.Runtime.TypeName))
                    errors.Add("Runtime TypeName is required");
            }

            // 验证依赖
            foreach (var dep in manifest.Addin.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dep.AddinId))
                    errors.Add("Dependency AddinId is required");

                if (!string.IsNullOrWhiteSpace(dep.Version) && !IsValidVersionRange(dep.Version))
                    errors.Add($"Invalid dependency version: {dep.Version}");
            }

            // 验证扩展点
            foreach (var ep in manifest.ExtensionPoints)
            {
                if (string.IsNullOrWhiteSpace(ep.Path))
                    errors.Add("ExtensionPoint Path is required");
            }

            if (errors.Any())
            {
                result.IsValid = false;
                result.Errors.AddRange(errors);
            }

            return result;
        }

        private bool IsValidVersionRange(string version)
        {
            if (version.EndsWith("+"))
                return Version.TryParse(version.TrimEnd('+'), out _);

            if (version.StartsWith("[") || version.StartsWith("("))
            {
                var content = version.Trim('[', ']', '(', ')');
                var parts = content.Split(',');
                return parts.All(p => Version.TryParse(p.Trim(), out _));
            }

            return Version.TryParse(version, out _);
        }

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new();
        }
    }
}
