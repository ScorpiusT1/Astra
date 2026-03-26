using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonWrapper.Modules
{
    /// <summary>
    /// 模块缓存条目，持有 PyObject 引用及统计信息
    /// ⭐ Dispose 必须在持有 GIL 的专用线程内调用
    /// </summary>
    internal sealed class CachedModule : IDisposable
    {
        public PyObject Module { get; }
        public DateTime CachedAt { get; } = DateTime.Now;
        public int HitCount { get; private set; }

        public CachedModule(PyObject module) => Module = module;
        public void RecordHit() => HitCount++;

        public void Dispose() => Module?.Dispose();
    }

    /// <summary>缓存统计快照（纯 C# 对象，可安全传递到 UI 线程）</summary>
    public sealed class ModuleCacheStats
    {
        public string ModuleName { get; set; }
        public DateTime CachedAt { get; set; }
        public int HitCount { get; set; }
        public TimeSpan Age => DateTime.Now - CachedAt;

        public override string ToString() =>
            $"{ModuleName,-20} 缓存时长:{Age:mm\\:ss}  命中:{HitCount}次";
    }
}
