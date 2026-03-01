using Astra.Core.Foundation.Common;

namespace Astra.Core.Configuration.Services
{
    /// <summary>
    /// <see cref="OperationResult"/> 扩展 — 失败时抛出 <see cref="ConfigurationException"/>。
    /// 仅保留对基类 <see cref="OperationResult"/> 的扩展，<see cref="OperationResult{T}"/> 继承自该类型，会统一使用此方法，避免与泛型重载产生二义性。
    /// </summary>
    public static class OperationResultExtensions
    {
        public static void ThrowIfFailed(this OperationResult result)
        {
            if (!result.Success) throw new ConfigurationException(result.Message);
        }

        public static void ThrowIfFailed<T>(this OperationResult<T> result)
        {
            if (!result.Success) throw new ConfigurationException(result.Message);
        }
    }
}

