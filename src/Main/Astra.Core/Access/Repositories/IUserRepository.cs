using Astra.Core.Access;
using Astra.Core.Access.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Repositories
{
    /// <summary>
    /// 用户仓储接口
    /// 定义用户数据访问的抽象接口，遵循依赖倒置原则 (DIP)
    /// 用于解耦业务逻辑和数据访问层
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// 根据用户ID获取用户
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <returns>用户对象，如果不存在则返回null</returns>
        User GetById(int id);

        /// <summary>
        /// 根据用户名获取用户
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>用户对象，如果不存在则返回null</returns>
        User GetByUsername(string username);

        /// <summary>
        /// 获取所有用户列表（按角色降序，创建时间升序排序）
        /// </summary>
        /// <returns>用户列表</returns>
        IEnumerable<User> GetAll();

        /// <summary>
        /// 根据角色获取用户列表
        /// </summary>
        /// <param name="role">用户角色</param>
        /// <returns>指定角色的用户列表（按创建时间升序排序）</returns>
        IEnumerable<User> GetByRole(UserRole role);

        /// <summary>
        /// 添加新用户
        /// </summary>
        /// <param name="user">要添加的用户对象</param>
        void Add(User user);

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="user">要更新的用户对象</param>
        void Update(User user);

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="user">要删除的用户对象</param>
        void Delete(User user);

        /// <summary>
        /// 统计指定角色的用户数量
        /// </summary>
        /// <param name="role">用户角色</param>
        /// <returns>用户数量</returns>
        int CountByRole(UserRole role);

        /// <summary>
        /// 统计所有用户数量
        /// </summary>
        /// <returns>用户总数</returns>
        int Count();

        /// <summary>
        /// 检查用户名是否已存在
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>如果存在返回true，否则返回false</returns>
        bool ExistsByUsername(string username);
    }
}
