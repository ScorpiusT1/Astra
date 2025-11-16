using System.Collections.Concurrent;

namespace Astra.Core.Plugins.Resources
{
    public class IconCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _cache = new();
        private readonly List<IIconProvider> _providers = new();

        public IconCache()
        {
            _providers.Add(new FileIconProvider());
            _providers.Add(new EmbeddedIconProvider());
        }

        public async Task<byte[]> GetIconAsync(string path)
        {
            if (_cache.TryGetValue(path, out var cached))
                return cached;

            var provider = _providers.FirstOrDefault(p => p.CanHandle(path));
            if (provider == null)
                return null;

            var icon = await provider.GetIconAsync(path);
            _cache[path] = icon;
            return icon;
        }
    }
}
