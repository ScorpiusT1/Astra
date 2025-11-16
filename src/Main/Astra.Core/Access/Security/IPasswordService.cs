using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Security
{
    /// <summary>
    /// 密码服务接口
    /// 定义密码相关的加密、验证和强度检查功能
    /// </summary>
    public interface IPasswordService
    {
        /// <summary>
        /// 对密码进行哈希加密
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <returns>加密后的密码哈希值（SHA256十六进制字符串）</returns>
        string HashPassword(string password);

        /// <summary>
        /// 验证密码是否与哈希值匹配
        /// </summary>
        /// <param name="password">待验证的明文密码</param>
        /// <param name="hash">已存储的密码哈希值</param>
        /// <returns>如果密码匹配返回true，否则返回false</returns>
        bool VerifyPassword(string password, string hash);

        /// <summary>
        /// 验证密码强度是否符合要求
        /// </summary>
        /// <param name="password">待验证的密码</param>
        /// <exception cref="AccessGuardException">当密码不符合强度要求时抛出异常</exception>
        void ValidatePasswordStrength(string password);
    }
}
