using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Astra.Core.Nodes.Serialization
{
    /// <summary>
    /// 多流程序列化服务实现（使用 Newtonsoft.Json）
    /// 符合单一职责原则：专门负责多流程数据的序列化和反序列化
    /// 符合开闭原则：通过配置选项支持扩展
    /// 支持循环引用序列化和反序列化
    /// </summary>
    public class MultiWorkflowSerializer : IMultiWorkflowSerializer
    {
        private readonly JsonSerializerSettings _jsonSettings;

        public MultiWorkflowSerializer(JsonSerializerSettings settings = null)
        {
            _jsonSettings = settings ?? CreateDefaultSettings();
        }

        /// <summary>
        /// 创建默认的JSON序列化设置
        /// </summary>
        private static JsonSerializerSettings CreateDefaultSettings()
        {
            return new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                // TypeNameHandling.Auto 会自动在序列化时添加 $type 信息，反序列化时根据 $type 自动创建正确的类型
                // 这样就能自动支持多态序列化，无需自定义转换器
                TypeNameHandling = TypeNameHandling.Auto,
                // 处理循环引用：序列化循环引用，保留对象引用
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Local
            };
        }

        /// <summary>
        /// 保存多流程数据到文件
        /// </summary>
        public OperationResult SaveToFile(MultiWorkflowData data, string filePath)
        {
            try
            {
                if (data == null)
                    return OperationResult.Failure("多流程数据不能为空", ErrorCodes.InvalidData);

                if (string.IsNullOrWhiteSpace(filePath))
                    return OperationResult.Failure("文件路径不能为空", ErrorCodes.InvalidData);

                // 验证数据完整性
                var validationResult = ValidateData(data);
                if (!validationResult.Success)
                    return validationResult;

                // 更新修改时间
                data.ModifiedAt = DateTime.Now;

                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 序列化为JSON
                var json = JsonConvert.SerializeObject(data, _jsonSettings);

                // 写入文件
                File.WriteAllText(filePath, json);

                // 更新主流程的文件路径
                if (data.MasterWorkflow != null)
                {
                    data.MasterWorkflow.FilePath = filePath;
                }

                return OperationResult.Succeed($"多流程数据已保存到: {filePath}");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"保存多流程数据失败: {ex.Message}", ex, ErrorCodes.FileSaveFailed);
            }
        }

        /// <summary>
        /// 从文件加载多流程数据
        /// </summary>
        public OperationResult<MultiWorkflowData> LoadFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return OperationResult<MultiWorkflowData>.Failure("文件路径不能为空", ErrorCodes.InvalidData);

                if (!File.Exists(filePath))
                    return OperationResult<MultiWorkflowData>.Failure($"文件不存在: {filePath}", ErrorCodes.FileNotFound);

                // 读取文件内容
                var json = File.ReadAllText(filePath);

                // 反序列化
                var result = ImportFromJson(json);
                if (!result.Success)
                    return result;

                // 更新主流程的文件路径
                if (result.Data.MasterWorkflow != null)
                {
                    result.Data.MasterWorkflow.FilePath = filePath;
                }

                return result;
            }
            catch (Exception ex)
            {
                return OperationResult<MultiWorkflowData>.Fail($"加载多流程数据失败: {ex.Message}", ex, ErrorCodes.FileLoadFailed);
            }
        }

        /// <summary>
        /// 导出多流程数据为JSON字符串
        /// </summary>
        public OperationResult<string> ExportToJson(MultiWorkflowData data)
        {
            try
            {
                if (data == null)
                    return OperationResult<string>.Failure("多流程数据不能为空", ErrorCodes.InvalidData);

                // 验证数据完整性
                var validationResult = ValidateData(data);
                if (!validationResult.Success)
                    return OperationResult<string>.Failure(validationResult.ErrorMessage, validationResult.ErrorCode);

                // 序列化为JSON
                var json = JsonConvert.SerializeObject(data, _jsonSettings);

                return OperationResult<string>.Succeed(json, "导出成功");
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail($"导出多流程数据失败: {ex.Message}", ex, ErrorCodes.SerializationFailed);
            }
        }

        /// <summary>
        /// 从JSON字符串导入多流程数据
        /// </summary>
        public OperationResult<MultiWorkflowData> ImportFromJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return OperationResult<MultiWorkflowData>.Failure("JSON字符串不能为空", ErrorCodes.InvalidData);

                // 反序列化
                var data = JsonConvert.DeserializeObject<MultiWorkflowData>(json, _jsonSettings);

                if (data == null)
                    return OperationResult<MultiWorkflowData>.Failure("JSON反序列化失败，数据为空", ErrorCodes.DeserializationFailed);

                // 验证数据完整性
                var validationResult = ValidateData(data);
                if (!validationResult.Success)
                    return OperationResult<MultiWorkflowData>.Failure(validationResult.ErrorMessage, validationResult.ErrorCode);

                // 重建对象关系（如果需要）
                RebuildRelationships(data);

                return OperationResult<MultiWorkflowData>.Succeed(data, "导入成功");
            }
            catch (JsonException ex)
            {
                return OperationResult<MultiWorkflowData>.Fail($"JSON格式错误: {ex.Message}", ex, ErrorCodes.InvalidJson);
            }
            catch (Exception ex)
            {
                return OperationResult<MultiWorkflowData>.Fail($"导入多流程数据失败: {ex.Message}", ex, ErrorCodes.DeserializationFailed);
            }
        }

        /// <summary>
        /// 验证多流程数据的完整性
        /// </summary>
        public OperationResult ValidateData(MultiWorkflowData data)
        {
            if (data == null)
                return OperationResult.Failure("多流程数据不能为空", ErrorCodes.InvalidData);

            if (data.MasterWorkflow == null)
                return OperationResult.Failure("主流程数据不能为空", ErrorCodes.InvalidData);

            if (data.SubWorkflows == null)
                return OperationResult.Failure("子流程字典不能为空", ErrorCodes.InvalidData);

            if (data.GlobalVariables == null)
                return OperationResult.Failure("全局变量池不能为空", ErrorCodes.InvalidData);

            // 验证主流程中的子流程引用是否都存在
            if (data.MasterWorkflow.SubWorkflowReferences != null)
            {
                foreach (var reference in data.MasterWorkflow.SubWorkflowReferences)
                {
                    if (string.IsNullOrWhiteSpace(reference.SubWorkflowId))
                    {
                        return OperationResult.Failure("主流程中存在子流程引用ID为空", ErrorCodes.InvalidData);
                    }

                    if (!data.SubWorkflows.ContainsKey(reference.SubWorkflowId))
                    {
                        return OperationResult.Failure($"主流程中引用的子流程不存在: {reference.SubWorkflowId}", ErrorCodes.InvalidData);
                    }
                }

                // 验证主流程中的连线是否有效
                if (data.MasterWorkflow.Edges != null)
                {
                    foreach (var edge in data.MasterWorkflow.Edges)
                    {
                        var sourceRefExists = data.MasterWorkflow.SubWorkflowReferences.Any(r => r.Id == edge.SourceNodeId);
                        var targetRefExists = data.MasterWorkflow.SubWorkflowReferences.Any(r => r.Id == edge.TargetNodeId);

                        if (!sourceRefExists)
                        {
                            return OperationResult.Failure($"主流程连线中的源节点不存在: {edge.SourceNodeId}", ErrorCodes.InvalidData);
                        }

                        if (!targetRefExists)
                        {
                            return OperationResult.Failure($"主流程连线中的目标节点不存在: {edge.TargetNodeId}", ErrorCodes.InvalidData);
                        }
                    }
                }
            }

            // 验证子流程中的节点和连接是否有效
            foreach (var subWorkflow in data.SubWorkflows.Values)
            {
                if (subWorkflow.Nodes == null)
                    continue;

                if (subWorkflow.Connections != null)
                {
                    foreach (var connection in subWorkflow.Connections)
                    {
                        var sourceNodeExists = subWorkflow.Nodes.Any(n => n.Id == connection.SourceNodeId);
                        var targetNodeExists = subWorkflow.Nodes.Any(n => n.Id == connection.TargetNodeId);

                        if (!sourceNodeExists)
                        {
                            return OperationResult.Failure($"子流程 '{subWorkflow.Name}' 中的连接源节点不存在: {connection.SourceNodeId}", ErrorCodes.InvalidData);
                        }

                        if (!targetNodeExists)
                        {
                            return OperationResult.Failure($"子流程 '{subWorkflow.Name}' 中的连接目标节点不存在: {connection.TargetNodeId}", ErrorCodes.InvalidData);
                        }
                    }
                }
            }

            return OperationResult.Succeed();
        }

        /// <summary>
        /// 重建对象关系（反序列化后调用）
        /// </summary>
        private void RebuildRelationships(MultiWorkflowData data)
        {
            // 重建子流程中的节点关系
            foreach (var subWorkflow in data.SubWorkflows.Values)
            {
                if (subWorkflow != null)
                {
                    subWorkflow.RebuildRelationships();
                }
            }

            // 重建主流程中的节点关系（如果有节点的话）
            // 注意：主流程通常只包含引用，不包含实际节点，所以这里可能不需要
        }

        /// <summary>
        /// 导出单个子流程到文件
        /// </summary>
        public OperationResult ExportSingleWorkflowToFile(WorkFlowNode workflow, string filePath)
        {
            try
            {
                if (workflow == null)
                    return OperationResult.Failure("子流程数据不能为空", ErrorCodes.InvalidData);

                if (string.IsNullOrWhiteSpace(filePath))
                    return OperationResult.Failure("文件路径不能为空", ErrorCodes.InvalidData);

                // 创建单个子流程数据对象
                var data = new SingleWorkflowData
                {
                    WorkflowName = workflow.Name ?? "未命名流程",
                    WorkflowDescription = workflow.Description,
                    Workflow = workflow,
                    ModifiedAt = DateTime.Now
                };

                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 序列化为JSON
                var json = JsonConvert.SerializeObject(data, _jsonSettings);

                // 写入文件
                File.WriteAllText(filePath, json);

                return OperationResult.Succeed($"子流程已导出到: {filePath}");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"导出子流程失败: {ex.Message}", ex, ErrorCodes.FileSaveFailed);
            }
        }

        /// <summary>
        /// 从文件导入单个子流程
        /// </summary>
        public OperationResult<WorkFlowNode> ImportSingleWorkflowFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return OperationResult<WorkFlowNode>.Failure("文件路径不能为空", ErrorCodes.InvalidData);

                if (!File.Exists(filePath))
                    return OperationResult<WorkFlowNode>.Failure($"文件不存在: {filePath}", ErrorCodes.FileNotFound);

                // 读取文件内容
                var json = File.ReadAllText(filePath);

                // 尝试解析为单个子流程数据
                try
                {
                    var singleData = JsonConvert.DeserializeObject<SingleWorkflowData>(json, _jsonSettings);
                    if (singleData != null && singleData.Workflow != null)
                    {
                        // 重建关系
                        singleData.Workflow.RebuildRelationships();
                        return OperationResult<WorkFlowNode>.Succeed(singleData.Workflow, "导入成功");
                    }
                }
                catch
                {
                    // 如果不是单个子流程格式，尝试解析为多流程格式
                }

                // 尝试解析为多流程数据（可能包含多个子流程）
                var multiData = JsonConvert.DeserializeObject<MultiWorkflowData>(json, _jsonSettings);
                if (multiData != null && multiData.SubWorkflows != null && multiData.SubWorkflows.Count > 0)
                {
                    // 返回第一个子流程
                    var firstWorkflow = multiData.SubWorkflows.Values.First();
                    firstWorkflow.RebuildRelationships();
                    return OperationResult<WorkFlowNode>.Succeed(firstWorkflow, "导入成功（从多流程文件中提取第一个子流程）");
                }

                return OperationResult<WorkFlowNode>.Failure("文件格式不正确，无法识别为子流程文件", ErrorCodes.InvalidData);
            }
            catch (JsonException ex)
            {
                return OperationResult<WorkFlowNode>.Fail($"JSON格式错误: {ex.Message}", ex, ErrorCodes.InvalidJson);
            }
            catch (Exception ex)
            {
                return OperationResult<WorkFlowNode>.Fail($"导入子流程失败: {ex.Message}", ex, ErrorCodes.FileLoadFailed);
            }
        }

        /// <summary>
        /// 导出单个子流程为JSON字符串
        /// </summary>
        public OperationResult<string> ExportSingleWorkflowToJson(WorkFlowNode workflow)
        {
            try
            {
                if (workflow == null)
                    return OperationResult<string>.Failure("子流程数据不能为空", ErrorCodes.InvalidData);

                // 创建单个子流程数据对象
                var data = new SingleWorkflowData
                {
                    WorkflowName = workflow.Name ?? "未命名流程",
                    WorkflowDescription = workflow.Description,
                    Workflow = workflow
                };

                // 序列化为JSON
                var json = JsonConvert.SerializeObject(data, _jsonSettings);

                return OperationResult<string>.Succeed(json, "导出成功");
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail($"导出子流程失败: {ex.Message}", ex, ErrorCodes.SerializationFailed);
            }
        }

        /// <summary>
        /// 从JSON字符串导入单个子流程
        /// </summary>
        public OperationResult<WorkFlowNode> ImportSingleWorkflowFromJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return OperationResult<WorkFlowNode>.Failure("JSON字符串不能为空", ErrorCodes.InvalidData);

                // 尝试解析为单个子流程数据
                try
                {
                    var singleData = JsonConvert.DeserializeObject<SingleWorkflowData>(json, _jsonSettings);
                    if (singleData != null && singleData.Workflow != null)
                    {
                        // 重建关系
                        singleData.Workflow.RebuildRelationships();
                        return OperationResult<WorkFlowNode>.Succeed(singleData.Workflow, "导入成功");
                    }
                }
                catch
                {
                    // 如果不是单个子流程格式，尝试解析为多流程格式
                }

                // 尝试解析为多流程数据（可能包含多个子流程）
                var multiData = JsonConvert.DeserializeObject<MultiWorkflowData>(json, _jsonSettings);
                if (multiData != null && multiData.SubWorkflows != null && multiData.SubWorkflows.Count > 0)
                {
                    // 返回第一个子流程
                    var firstWorkflow = multiData.SubWorkflows.Values.First();
                    firstWorkflow.RebuildRelationships();
                    return OperationResult<WorkFlowNode>.Succeed(firstWorkflow, "导入成功（从多流程JSON中提取第一个子流程）");
                }

                return OperationResult<WorkFlowNode>.Failure("JSON格式不正确，无法识别为子流程数据", ErrorCodes.InvalidData);
            }
            catch (JsonException ex)
            {
                return OperationResult<WorkFlowNode>.Fail($"JSON格式错误: {ex.Message}", ex, ErrorCodes.InvalidJson);
            }
            catch (Exception ex)
            {
                return OperationResult<WorkFlowNode>.Fail($"导入子流程失败: {ex.Message}", ex, ErrorCodes.DeserializationFailed);
            }
        }

        /// <summary>
        /// 从文件导入多个子流程（支持多流程项目文件或单个子流程文件）
        /// </summary>
        public OperationResult<List<WorkFlowNode>> ImportMultipleWorkflowsFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return OperationResult<List<WorkFlowNode>>.Failure("文件路径不能为空", ErrorCodes.InvalidData);

                if (!File.Exists(filePath))
                    return OperationResult<List<WorkFlowNode>>.Failure($"文件不存在: {filePath}", ErrorCodes.FileNotFound);

                // 读取文件内容
                var json = File.ReadAllText(filePath);
                var workflows = new List<WorkFlowNode>();

                // 尝试解析为多流程数据
                try
                {
                    var multiData = JsonConvert.DeserializeObject<MultiWorkflowData>(json, _jsonSettings);
                    if (multiData != null && multiData.SubWorkflows != null && multiData.SubWorkflows.Count > 0)
                    {
                        foreach (var workflow in multiData.SubWorkflows.Values)
                        {
                            workflow.RebuildRelationships();
                            workflows.Add(workflow);
                        }
                        return OperationResult<List<WorkFlowNode>>.Succeed(workflows, $"成功导入 {workflows.Count} 个子流程");
                    }
                }
                catch
                {
                    // 如果不是多流程格式，尝试解析为单个子流程格式
                }

                // 尝试解析为单个子流程数据
                try
                {
                    var singleData = JsonConvert.DeserializeObject<SingleWorkflowData>(json, _jsonSettings);
                    if (singleData != null && singleData.Workflow != null)
                    {
                        singleData.Workflow.RebuildRelationships();
                        workflows.Add(singleData.Workflow);
                        return OperationResult<List<WorkFlowNode>>.Succeed(workflows, "成功导入 1 个子流程");
                    }
                }
                catch
                {
                    // 格式不正确
                }

                return OperationResult<List<WorkFlowNode>>.Failure("文件格式不正确，无法识别为流程文件", ErrorCodes.InvalidData);
            }
            catch (JsonException ex)
            {
                return OperationResult<List<WorkFlowNode>>.Fail($"JSON格式错误: {ex.Message}", ex, ErrorCodes.InvalidJson);
            }
            catch (Exception ex)
            {
                return OperationResult<List<WorkFlowNode>>.Fail($"导入子流程失败: {ex.Message}", ex, ErrorCodes.FileLoadFailed);
            }
        }
    }

    /// <summary>
    /// 错误码定义（用于多流程序列化）
    /// </summary>
    public static class ErrorCodes
    {
        public const int InvalidData = 1001;
        public const int FileNotFound = 1002;
        public const int FileSaveFailed = 1003;
        public const int FileLoadFailed = 1004;
        public const int SerializationFailed = 1005;
        public const int DeserializationFailed = 1006;
        public const int InvalidJson = 1007;
    }
}
