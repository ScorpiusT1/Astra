using System;
using System.Security.Cryptography;
using System.Text;

// ⚠️ 迁移说明：
//   - 原位置：Astra.Core/Access/Security/PasswordHelper.cs
//   - 新位置：Astra.Core/Foundation/Common/PasswordHelper.cs
//   - 命名空间：Astra.Core.Foundation.Common（已更新为与文件夹匹配）
//   - 原因：这是通用的密码加密工具类，应放在 Foundation 层

namespace Astra.Core.Foundation.Common
{
    /// <summary>
    /// 密码加密辅助类
    /// 
    /// ✅ 迁移说明：
    ///   - 文件已移动到 Foundation/Common/
    ///   - 命名空间已更新为：Astra.Core.Foundation.Common
    ///   - 建议新代码使用：using Astra.Core.Foundation.Common;
    /// </summary>
    public static class PasswordHelper
    {
        /// <summary>
        /// 使用SHA256加密密码
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

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
        /// 验证密码
        /// </summary>
        public static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            if (string.IsNullOrEmpty(hash))
                return false;

            string hashOfInput = HashPassword(password);
            return StringComparer.OrdinalIgnoreCase.Compare(hashOfInput, hash) == 0;
        }
    }
}

