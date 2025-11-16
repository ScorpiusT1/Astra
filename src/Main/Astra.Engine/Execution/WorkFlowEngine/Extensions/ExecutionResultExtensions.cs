using Astra.Core.Nodes.Models;
using System.Collections.Generic;

namespace Astra.Engine.Execution.WorkFlowEngine
{
    /// <summary>
    /// 执行结果扩展方法
    /// </summary>
    public static class ExecutionResultExtensions
    {
        /// <summary>
        /// 为执行结果添加输出数据
        /// </summary>
        /// <param name="result">执行结果</param>
        /// <param name="outputs">输出数据字典</param>
        /// <returns>执行结果实例，支持链式调用</returns>
        public static ExecutionResult WithOutputs(this ExecutionResult result, Dictionary<string, object> outputs)
        {
            if (outputs != null)
            {
                foreach (var kvp in outputs)
                {
                    result.OutputData[kvp.Key] = kvp.Value;
                }
            }
            return result;
        }
    }
}

