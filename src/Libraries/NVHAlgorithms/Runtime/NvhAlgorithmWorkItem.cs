using NVHAlgorithms.Abstractions;

namespace NVHAlgorithms.Runtime;

/// <summary>批处理中的单条可执行项（由宿主或桥接层组装）。</summary>
public sealed record NvhAlgorithmWorkItem(
    string TaskId,
    NvhAlgorithmDescriptor Descriptor,
    Func<CancellationToken, Task<NvhSingleTaskResult>> Execute);
