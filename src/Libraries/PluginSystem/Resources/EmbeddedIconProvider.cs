using System.Reflection;

namespace Addins.Resources
{
    public class EmbeddedIconProvider : IIconProvider
    {
        public Task<byte[]> GetIconAsync(string path)
        {
            // 从嵌入资源加载
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(path);
            if (stream == null) return Task.FromResult<byte[]>(null);

            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return Task.FromResult(buffer);
        }

        public bool CanHandle(string path)
        {
            return path.StartsWith("resource://");
        }
    }
}
