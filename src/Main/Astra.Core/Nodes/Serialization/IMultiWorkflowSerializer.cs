using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
using System.Threading.Tasks;

namespace Astra.Core.Nodes.Serialization
{
    /// <summary>
    /// 多流程序列化服务接口
    /// 负责多流程的保存、加载、导入和导出
    /// 符合单一职责原则：专门负责多流程数据的持久化
    /// 符合依赖倒置原则：通过接口抽象，支持不同的序列化实现
    /// </summary>
    public interface IMultiWorkflowSerializer
    {
        /// <summary>
        /// 保存多流程数据到文件
        /// </summary>
        /// <param name="data">多流程数据</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>操作结果</returns>
        OperationResult SaveToFile(MultiWorkflowData data, string filePath);

        /// <summary>
        /// 从文件加载多流程数据
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>操作结果，包含加载的多流程数据</returns>
        OperationResult<MultiWorkflowData> LoadFromFile(string filePath);

        /// <summary>
        /// 导出多流程数据为JSON字符串
        /// </summary>
        /// <param name="data">多流程数据</param>
        /// <returns>操作结果，包含JSON字符串</returns>
        OperationResult<string> ExportToJson(MultiWorkflowData data);

        /// <summary>
        /// 从JSON字符串导入多流程数据
        /// </summary>
        /// <param name="json">JSON字符串</param>
        /// <returns>操作结果，包含导入的多流程数据</returns>
        OperationResult<MultiWorkflowData> ImportFromJson(string json);

        /// <summary>
        /// 验证多流程数据的完整性
        /// </summary>
        /// <param name="data">多流程数据</param>
        /// <returns>验证结果</returns>
        OperationResult ValidateData(MultiWorkflowData data);

        /// <summary>
        /// 导出单个子流程到文件
        /// </summary>
        /// <param name="workflow">子流程数据</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>操作结果</returns>
        OperationResult ExportSingleWorkflowToFile(WorkFlowNode workflow, string filePath);

        /// <summary>
        /// 从文件导入单个子流程
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>操作结果，包含导入的子流程数据</returns>
        OperationResult<WorkFlowNode> ImportSingleWorkflowFromFile(string filePath);

        /// <summary>
        /// 导出单个子流程为JSON字符串
        /// </summary>
        /// <param name="workflow">子流程数据</param>
        /// <returns>操作结果，包含JSON字符串</returns>
        OperationResult<string> ExportSingleWorkflowToJson(WorkFlowNode workflow);

        /// <summary>
        /// 从JSON字符串导入单个子流程
        /// </summary>
        /// <param name="json">JSON字符串</param>
        /// <returns>操作结果，包含导入的子流程数据</returns>
        OperationResult<WorkFlowNode> ImportSingleWorkflowFromJson(string json);

        /// <summary>
        /// 从文件导入多个子流程（支持多流程项目文件或单个子流程文件）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>操作结果，包含导入的子流程列表</returns>
        OperationResult<List<WorkFlowNode>> ImportMultipleWorkflowsFromFile(string filePath);
    }
}

