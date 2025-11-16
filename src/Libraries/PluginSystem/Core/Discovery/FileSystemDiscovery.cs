using Addins.Core.Models;
using Addins.Manifest.Serializers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Addins.Core.Discovery
{
    public class FileSystemDiscovery : IPluginDiscovery
    {
        private readonly List<IManifestSerializer> _serializers = new();

        public FileSystemDiscovery(IEnumerable<IManifestSerializer> serializers)
        {
            _serializers.AddRange(serializers);
        }

        public async Task<IEnumerable<PluginDescriptor>> DiscoverAsync(string searchPath)
        {
            var descriptors = new List<PluginDescriptor>();

            if (!Directory.Exists(searchPath))
                return descriptors;

            // 查找所有 .addin 文件
            var addinFiles = Directory.GetFiles(searchPath, "*.addin", SearchOption.AllDirectories);

            foreach (var addinFile in addinFiles)
            {
                try
                {
                    var descriptor = await LoadAddinAsync(addinFile);
                    if (descriptor != null)
                    {
                        descriptors.Add(descriptor);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load addin {addinFile}: {ex.Message}");
                }
            }

            return descriptors;
        }

        private async Task<PluginDescriptor> LoadAddinAsync(string addinFile)
        {
            var serializer = _serializers.FirstOrDefault(s => s.CanHandle(addinFile));
            if (serializer == null)
                return null;

            using var stream = File.OpenRead(addinFile);
            var manifest = serializer.Deserialize(stream);

            var descriptor = new PluginDescriptor
            {
                Id = manifest.Addin.Id,
                Name = manifest.Addin.Name,
                Version = Version.Parse(manifest.Addin.Version),
                Description = manifest.Addin.Description,
                Author = manifest.Addin.Author,
                AssemblyPath = Path.Combine(Path.GetDirectoryName(addinFile),
                                           manifest.Addin.Runtime.Assembly),
                TypeName = manifest.Addin.Runtime.TypeName,
                IconPath = manifest.Addin.IconPath,
                State = PluginState.Discovered
            };

            // 解析依赖
            foreach (var dep in manifest.Addin.Dependencies)
            {
                descriptor.Dependencies.Add(new DependencyInfo
                {
                    PluginId = dep.AddinId,
                    VersionRange = ParseVersionRange(dep.Version),
                    IsOptional = dep.Optional
                });
            }

            // 解析权限
            descriptor.Permissions = ParsePermissions(manifest.Addin.Permissions.Required);

            return descriptor;
        }

        private VersionRange ParseVersionRange(string versionString)
        {
            // 支持格式: "1.0", "[1.0,2.0)", "(1.0,2.0]", "1.0+"
            var range = new VersionRange();

            if (versionString.EndsWith("+"))
            {
                range.MinVersion = Version.Parse(versionString.TrimEnd('+'));
                range.IncludeMin = true;
                return range;
            }

            if (versionString.StartsWith("[") || versionString.StartsWith("("))
            {
                range.IncludeMin = versionString[0] == '[';
                range.IncludeMax = versionString[^1] == ']';

                var parts = versionString.Trim('[', ']', '(', ')').Split(',');
                range.MinVersion = Version.Parse(parts[0]);
                if (parts.Length > 1)
                {
                    range.MaxVersion = Version.Parse(parts[1]);
                }
            }
            else
            {
                range.MinVersion = range.MaxVersion = Version.Parse(versionString);
            }

            return range;
        }

        private PluginPermissions ParsePermissions(List<string> permissions)
        {
            var result = PluginPermissions.None;
            foreach (var perm in permissions)
            {
                if (Enum.TryParse<PluginPermissions>(perm, true, out var parsed))
                {
                    result |= parsed;
                }
            }
            return result;
        }
    }
}
