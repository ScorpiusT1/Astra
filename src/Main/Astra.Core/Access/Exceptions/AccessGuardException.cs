using System;
using Astra.Core.Foundation.Exceptions;

namespace Astra.Core.Access.Exceptions
{
    /// <summary>
    /// Access自定义异常
    /// 
    /// ✅ 重构说明：
    ///   - 继承自 AstraCoreException，获得统一的异常结构
    ///   - 保持命名空间 Astra.Core.Access.Exceptions 以保持向后兼容
    /// </summary>
    public class AccessGuardException : AstraCoreException
    {
        public AccessGuardException(string message)
            : base(message, "Access", null)
        {
        }

        public AccessGuardException(string message, Exception innerException)
            : base(message, "Access", null, innerException)
        {
        }

        public AccessGuardException(string message, string operation)
            : base(message, "Access", operation)
        {
        }

        public AccessGuardException(string message, string operation, Exception innerException)
            : base(message, "Access", operation, innerException)
        {
        }
    }
}
