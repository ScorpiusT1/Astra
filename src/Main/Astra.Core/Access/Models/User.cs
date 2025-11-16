using System;

namespace Astra.Core.Access.Models
{
    /// <summary>
    /// 用户实体类
    /// 表示系统中的用户信息，包括身份认证和权限管理相关数据
    /// </summary>
    public class User
    {
        /// <summary>
        /// 用户唯一标识符（主键）
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 用户名（唯一，最大长度50）
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 密码哈希值（SHA256加密后的密码，最大长度256）
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// 用户角色（Administrator/Engineer/Operator）
        /// </summary>
        public UserRole Role { get; set; }

        /// <summary>
        /// 用户创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModifyTime { get; set; }

        /// <summary>
        /// 最后登录时间（可为空，表示从未登录）
        /// </summary>
        public DateTime? LastLoginTime { get; set; }
    }
}

