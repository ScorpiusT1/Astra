using System.Buffers;

namespace NVHAlgorithms.IO;

/// <summary>流式读取，使用 <see cref="ArrayPool{T}"/> 租用缓冲以降低 LOH 与峰值。</summary>
public static class NvhStreamBufferExtensions
{
    /// <summary>分块读取流，将每块复制到目标写入委托（由调用方决定解析）。</summary>
    public static async Task ReadAllPooledAsync(
        this Stream stream,
        int bufferSize,
        Func<ReadOnlyMemory<byte>, CancellationToken, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        var pool = ArrayPool<byte>.Shared;
        var buffer = pool.Rent(bufferSize);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await onChunk(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            pool.Return(buffer, clearArray: true);
        }
    }
}
