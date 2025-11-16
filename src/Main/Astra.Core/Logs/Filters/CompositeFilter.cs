using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Logs.Filters
{
    /// <summary>
    /// 组合过滤器
    /// 支持多个过滤器的组合逻辑（AND 或 OR）
    /// </summary>
    public class CompositeFilter : ILogFilter
    {
        private readonly List<ILogFilter> _filters;
        private readonly FilterMode _mode;

        /// <summary>
        /// 过滤器组合模式
        /// </summary>
        public enum FilterMode
        {
            /// <summary>
            /// AND 模式：所有过滤器都必须通过
            /// </summary>
            And,

            /// <summary>
            /// OR 模式：至少一个过滤器通过即可
            /// </summary>
            Or
        }

        /// <summary>
        /// 创建组合过滤器
        /// </summary>
        /// <param name="mode">组合模式</param>
        /// <param name="filters">过滤器列表</param>
        public CompositeFilter(FilterMode mode, params ILogFilter[] filters)
        {
            _mode = mode;
            _filters = filters?.ToList() ?? new List<ILogFilter>();
        }

        /// <summary>
        /// 添加过滤器
        /// </summary>
        public CompositeFilter Add(ILogFilter filter)
        {
            if (filter != null)
            {
                _filters.Add(filter);
            }
            return this;
        }

        public bool ShouldLog(LogEntry entry)
        {
            if (_filters.Count == 0)
                return true; // 没有过滤器，允许所有日志

            if (_mode == FilterMode.And)
            {
                // AND 模式：所有过滤器都必须通过
                return _filters.All(f => f.ShouldLog(entry));
            }
            else
            {
                // OR 模式：至少一个过滤器通过
                return _filters.Any(f => f.ShouldLog(entry));
            }
        }
    }
}

