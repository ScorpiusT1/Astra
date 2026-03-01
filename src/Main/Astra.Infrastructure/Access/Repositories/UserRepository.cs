using Astra.Infrastructure.Access.Data;
using Astra.Core.Access.Models;
using Astra.Core.Access.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Astra.Infrastructure.Access.Repositories
{
    /// <summary>
    /// 用户仓储 EF Core 实现（基础设施层）
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly AccessGuardDbContext _context;

        public UserRepository(AccessGuardDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public User GetById(int id) => _context.Users.Find(id);

        public User GetByUsername(string username)
            => _context.Users.AsNoTracking().FirstOrDefault(u => u.Username == username);

        public IEnumerable<User> GetAll()
            => _context.Users.AsNoTracking()
                .OrderByDescending(u => u.Role)
                .ThenBy(u => u.CreateTime)
                .ToList();

        public IEnumerable<User> GetByRole(UserRole role)
            => _context.Users.AsNoTracking()
                .Where(u => u.Role == role)
                .OrderBy(u => u.CreateTime)
                .ToList();

        public void Add(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();
        }

        public void Update(User user)
        {
            _context.Users.Update(user);
            _context.SaveChanges();
        }

        public void Delete(User user)
        {
            _context.Users.Remove(user);
            _context.SaveChanges();
        }

        public int CountByRole(UserRole role)
            => _context.Users.Count(u => u.Role == role);

        public int Count() => _context.Users.Count();

        public bool ExistsByUsername(string username)
            => _context.Users.Any(u => u.Username == username);
    }
}
