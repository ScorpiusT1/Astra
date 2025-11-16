using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Logs.Filters
{
    /// <summary>
    /// 日志分类过滤器
    /// 根据日志分类过滤日志
    /// </summary>
    public class CategoryFilter : ILogFilter
    {
        private readonly HashSet<LogCategory> _allowedCategories;

        /// <summary>
        /// 创建分类过滤器
        /// </summary>
        /// <param name="allowedCategories">允许的日志分类，只有这些分类的日志会被记录</param>
        public CategoryFilter(params LogCategory[] allowedCategories)
        {
            if (allowedCategories == null || allowedCategories.Length == 0)
            {
                _allowedCategories = null; // null 表示允许所有分类
            }
            else
            {
                _allowedCategories = new HashSet<LogCategory>(allowedCategories);
            }
        }

        public bool ShouldLog(LogEntry entry)
        {
            if (entry == null)
                return false;

            // 如果没有限制分类，允许所有
            if (_allowedCategories == null)
                return true;

            return _allowedCategories.Contains(entry.Category);
        }
    }
}

