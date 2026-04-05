using System.IO.MemoryMappedFiles;

namespace NVHAlgorithms.IO;

/// <summary>
/// 只读内存映射；用 <see cref="ViewStream"/> 与 <see cref="NvhStreamBufferExtensions"/> 组合分块读。
/// 调用方应限制 <paramref name="maxBytes"/> 以防超大文件 OOM。
/// </summary>
public sealed class NvhMemoryMappedReader : IDisposable
{
    private readonly MemoryMappedFile _mmf;

    public NvhMemoryMappedReader(string path, long maxBytes = long.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("路径无效。", nameof(path));

        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FileNotFoundException(path);

        Length = Math.Min(info.Length, maxBytes);
        _mmf = MemoryMappedFile.CreateFromFile(
            path,
            FileMode.Open,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read);

        ViewStream = _mmf.CreateViewStream(0, Length, MemoryMappedFileAccess.Read);
    }

    public long Length { get; }

    public MemoryMappedViewStream ViewStream { get; }

    public void Dispose()
    {
        ViewStream.Dispose();
        _mmf.Dispose();
    }
}
