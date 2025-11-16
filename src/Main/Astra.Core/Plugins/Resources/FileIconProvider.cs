namespace Astra.Core.Plugins.Resources
{
    public class FileIconProvider : IIconProvider
    {
        public async Task<byte[]> GetIconAsync(string path)
        {
            return await File.ReadAllBytesAsync(path);
        }

        public bool CanHandle(string path)
        {
            return File.Exists(path);
        }
    }
}
