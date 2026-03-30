using NVHDataBridge.Models;

namespace Astra.Plugins.DataImport.Import
{
    /// <summary>
    /// 将外部文件解析为 <see cref="NvhMemoryFile"/>，供流程中算法/滤波等节点消费。
    /// 新增格式时实现本接口并在 <see cref="NvhFormatImporterRegistry"/> 中注册。
    /// </summary>
    public interface INvhFormatImporter
    {
        /// <summary>唯一键，如 tdms、wav。</summary>
        string FormatKey { get; }

        /// <summary>文件扩展名（含点），小写。</summary>
        string FileExtension { get; }

        bool CanImport(string filePath);

        /// <summary>从磁盘加载；失败时抛出异常。</summary>
        NvhMemoryFile Import(string filePath);
    }
}
