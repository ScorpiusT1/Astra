using System;

// ⚠️ 迁移说明：
//   - 新创建：业务异常类
//   - 位置：Astra.Core/Foundation/Exceptions/BusinessException.cs
//   - 命名空间：Astra.Core.Foundation.Exceptions（已更新为与文件夹匹配）
//   - 原因：用于业务逻辑相关的异常

namespace Astra.Core.Foundation.Exceptions
{
    /// <summary>
    /// 业务异常类
    /// 
    /// ✅ 设计说明：
    ///   - 用于业务逻辑相关的异常（如验证失败、业务规则违反等）
    ///   - 继承自 AstraCoreException
    ///   - 通常不需要记录堆栈跟踪，因为这是预期的业务异常
    /// </summary>
    public class BusinessException : AstraCoreException
    {
        /// <summary>
        /// 业务错误类型
        /// </summary>
        public string BusinessErrorType { get; set; }

        public BusinessException(string message)
            : base(message)
        {
        }

        public BusinessException(string message, string businessErrorType)
            : base(message)
        {
            BusinessErrorType = businessErrorType;
        }

        public BusinessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public BusinessException(string message, string businessErrorType, Exception innerException)
            : base(message, innerException)
        {
            BusinessErrorType = businessErrorType;
        }

        public BusinessException(string message, string moduleName, string operation, string businessErrorType = null)
            : base(message, moduleName, operation)
        {
            BusinessErrorType = businessErrorType;
        }
    }
}

