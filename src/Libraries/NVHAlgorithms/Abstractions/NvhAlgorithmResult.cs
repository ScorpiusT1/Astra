namespace NVHAlgorithms.Abstractions;

public enum NvhTaskCompletionState
{
    Succeeded,
    Failed,
    Canceled,
    TimedOut,
}

public sealed class NvhAlgorithmResult<TOut>
{
    public required NvhTaskCompletionState State { get; init; }

    public TOut? Value { get; init; }

    public Exception? Error { get; init; }

    public static NvhAlgorithmResult<TOut> Ok(TOut value) => new() { State = NvhTaskCompletionState.Succeeded, Value = value };

    public static NvhAlgorithmResult<TOut> Fail(Exception ex) => new() { State = NvhTaskCompletionState.Failed, Error = ex };

    public static NvhAlgorithmResult<TOut> Canceled() => new() { State = NvhTaskCompletionState.Canceled };

    public static NvhAlgorithmResult<TOut> TimedOut() => new() { State = NvhTaskCompletionState.TimedOut };
}

public sealed class NvhSingleTaskResult
{
    public required string TaskId { get; init; }

    public required NvhAlgorithmDescriptor Descriptor { get; init; }

    public required NvhTaskCompletionState State { get; init; }

    public object? Output { get; init; }

    public Exception? Error { get; init; }
}
