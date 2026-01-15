using Astra.Core.Access;
using Astra.Core.Access.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Services
{
    /// <summary>
    /// 用户管理服务接口 - 接口隔离原则 (ISP)
    /// </summary>
    public interface IUserManagementService
    {
        /// <summary>
        /// 用户登录验证
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>登录成功返回User对象</returns>
        User Login(string username, string password);
        
        /// <summary>
        /// 添加新用户
        /// </summary>
        void AddUser(User currentUser, string username, string password, UserRole role);
        
        /// <summary>
        /// 删除用户
        /// </summary>
        void DeleteUser(User currentUser, string username);
        
        /// <summary>
        /// 修改用户角色（仅管理员或超级管理员可操作）
        /// </summary>
        /// <param name="currentUser">当前操作用户</param>
        /// <param name="username">目标用户名</param>
        /// <param name="newRole">新角色</param>
        void ChangeUserRole(User currentUser, string username, UserRole newRole);
        
        /// <summary>
        /// 修改用户名（仅管理员或超级管理员可操作）
        /// </summary>
        /// <param name="currentUser">当前操作用户</param>
        /// <param name="oldUsername">原用户名</param>
        /// <param name="newUsername">新用户名</param>
        void ChangeUsername(User currentUser, string oldUsername, string newUsername);
        
        /// <summary>
        /// 重置密码（管理员或超级管理员操作）
        /// </summary>
        void ResetPassword(User currentUser, string username, string newPassword);
        
        /// <summary>
        /// 修改密码（用户自己修改）
        /// </summary>
        /// <param name="currentUser">当前用户</param>
        /// <param name="oldPassword">旧密码</param>
        /// <param name="newPassword">新密码</param>
        void ChangePassword(User currentUser, string oldPassword, string newPassword);
        
        /// <summary>
        /// 根据用户名获取用户
        /// </summary>
        User GetUserByUsername(string username);
        
        /// <summary>
        /// 获取所有用户
        /// </summary>
        IEnumerable<User> GetAllUsers();
        
        /// <summary>
        /// 根据角色获取用户
        /// </summary>
        IEnumerable<User> GetUsersByRole(UserRole role);
        
        /// <summary>
        /// 获取用户总数
        /// </summary>
        int GetUserCount();
        
        /// <summary>
        /// 获取管理员数量
        /// </summary>
        int GetAdminCount();

        /// <summary>
        /// 获取最后登录的操作员用户
        /// </summary>
        /// <returns>最后登录的操作员用户，如果没有则返回null</returns>
        User? GetLastLoginOperator();

        /// <summary>
        /// 更新用户最后登录时间
        /// </summary>
        /// <param name="username">用户名</param>
        void UpdateLastLoginTime(string username);
    }
}
