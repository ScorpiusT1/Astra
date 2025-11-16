using Astra.Core.Access.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Security
{
    /// <summary>
    /// 密码服务实现类
    /// 负责密码的加密、验证和强度检查，遵循单一职责原则 (SRP)
    /// 使用SHA256算法进行密码哈希加密
    /// </summary>
    public class PasswordService : IPasswordService
    {
        /// <summary>
        /// 最小密码长度要求
        /// </summary>
        private const int MIN_PASSWORD_LENGTH = 6;

        /// <summary>
        /// 对密码进行SHA256哈希加密，返回十六进制字符串
        /// </summary>
        public string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// 验证输入的明文密码是否与存储的哈希值匹配
        /// 通过将输入密码加密后与存储的哈希值进行比较
        /// </summary>
        public bool VerifyPassword(string password, string hash)
        {
            string hashOfInput = HashPassword(password);
            return StringComparer.OrdinalIgnoreCase.Compare(hashOfInput, hash) == 0;
        }

        /// <summary>
        /// 验证密码强度是否符合要求
        /// 当前检查：密码长度至少为6位，且不能为空或仅包含空白字符
        /// </summary>
        /// <exception cref="AccessGuardException">当密码不符合强度要求时抛出异常</exception>
        public void ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < MIN_PASSWORD_LENGTH)
            {
                throw new AccessGuardException($"密码长度至少为{MIN_PASSWORD_LENGTH}位");
            }
        }
    }
}
