using Astra.Core.Access;
using Astra.Core.Access.Data;
using Astra.Core.Access.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Repositories
{
    /// <summary>
    /// 用户仓储实现类
    /// 负责用户数据访问的具体实现，遵循单一职责原则 (SRP)：只负责数据访问操作
    /// 使用Entity Framework Core进行数据持久化
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly AccessGuardDbContext _context;

        /// <summary>
        /// 构造函数，通过依赖注入获取数据库上下文
        /// </summary>
        /// <param name="context">数据库上下文实例</param>
        /// <exception cref="ArgumentNullException">当context为null时抛出</exception>
        public UserRepository(AccessGuardDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 根据用户ID获取用户（使用EF的Find方法，支持跟踪）
        /// </summary>
        public User GetById(int id)
        {
            return _context.Users.Find(id);
        }

        /// <summary>
        /// 根据用户名获取用户（使用AsNoTracking提高查询性能，不跟踪实体变更）
        /// </summary>
        public User GetByUsername(string username)
        {
            return _context.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.Username == username);
        }

        /// <summary>
        /// 获取所有用户列表（按角色降序，创建时间升序排序，使用AsNoTracking）
        /// </summary>
        public IEnumerable<User> GetAll()
        {
            return _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.Role)
                .ThenBy(u => u.CreateTime)
                .ToList();
        }

        /// <summary>
        /// 根据角色获取用户列表（按创建时间升序排序，使用AsNoTracking）
        /// </summary>
        public IEnumerable<User> GetByRole(UserRole role)
        {
            return _context.Users
                .AsNoTracking()
                .Where(u => u.Role == role)
                .OrderBy(u => u.CreateTime)
                .ToList();
        }

        /// <summary>
        /// 添加新用户到数据库
        /// </summary>
        public void Add(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();
        }

        /// <summary>
        /// 更新用户信息到数据库
        /// </summary>
        public void Update(User user)
        {
            _context.Users.Update(user);
            _context.SaveChanges();
        }

        /// <summary>
        /// 从数据库删除用户
        /// </summary>
        public void Delete(User user)
        {
            _context.Users.Remove(user);
            _context.SaveChanges();
        }

        /// <summary>
        /// 统计指定角色的用户数量
        /// </summary>
        public int CountByRole(UserRole role)
        {
            return _context.Users.Count(u => u.Role == role);
        }

        /// <summary>
        /// 统计所有用户数量
        /// </summary>
        public int Count()
        {
            return _context.Users.Count();
        }

        /// <summary>
        /// 检查用户名是否已存在
        /// </summary>
        public bool ExistsByUsername(string username)
        {
            return _context.Users.Any(u => u.Username == username);
        }
    }
}
