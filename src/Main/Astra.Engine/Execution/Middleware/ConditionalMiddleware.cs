using Astra.Core.Nodes.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Middleware
{
    /// <summary>
    /// 条件执行中间件（根据条件跳过执行）
    /// 根据节点启用状态和条件表达式决定是否执行节点
    /// </summary>
    public class ConditionalMiddleware : INodeMiddleware
    {
        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public async Task<ExecutionResult> InvokeAsync(
            Node node,
            NodeContext context,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<ExecutionResult>> next)
        {
            // 检查节点是否启用
            if (!node.IsEnabled)
            {
                return ExecutionResult.Skip($"节点 {node.Name} 已禁用");
            }

            // 检查是否有条件参数
            if (node.Parameters.TryGetValue("condition", out var conditionObj)
                && conditionObj is string condition)
            {
                if (!EvaluateCondition(condition, context, node))
                {
                    return ExecutionResult.Skip($"节点 {node.Name} 不满足条件: {condition}");
                }
            }

            return await next(cancellationToken);
        }

        /// <summary>
        /// 评估条件表达式
        /// 轻量表达式求值器：支持 ==, !=, >, >=, <, <=, null 判定 与 And/Or（用 &&/||）
        /// 变量来源：InputData、GlobalVariables、Node.Parameters
        /// </summary>
        private bool EvaluateCondition(string condition, NodeContext context, Node node)
        {
            // 轻量表达式求值器：支持 ==, !=, >, >=, <, <=, null 判定 与 And/Or（用 &&/||）
            // 变量来源：InputData、GlobalVariables、Node.Parameters
            try
            {
                bool EvalSingle(string expr)
                {
                    expr = expr.Trim();
                    // null 检测：key == null / key != null
                    if (expr.Contains("=="))
                    {
                        var parts = expr.Split(new[] { "==" }, StringSplitOptions.None);
                        var left = parts[0].Trim();
                        var right = parts[1].Trim();
                        var lv = ResolveValue(left, context, out bool found);
                        if (right.Equals("null", StringComparison.OrdinalIgnoreCase))
                            return found && lv == null;
                        var rv = ParseLiteral(right);
                        return Equals(lv, rv);
                    }
                    if (expr.Contains("!="))
                    {
                        var parts = expr.Split(new[] { "!=" }, StringSplitOptions.None);
                        var left = parts[0].Trim();
                        var right = parts[1].Trim();
                        var lv = ResolveValue(left, context, out bool found);
                        if (right.Equals("null", StringComparison.OrdinalIgnoreCase))
                            return !(found && lv == null);
                        var rv = ParseLiteral(right);
                        return !Equals(lv, rv);
                    }
                    // 比较运算
                    bool Cmp(string op, Func<int, bool> pred)
                    {
                        var parts = expr.Split(new[] { op }, StringSplitOptions.None);
                        if (parts.Length != 2) return false;
                        var left = parts[0].Trim();
                        var right = parts[1].Trim();
                        var lvObj = ResolveValue(left, context, out _);
                        var rvObj = ParseLiteral(right);
                        if (lvObj == null || rvObj == null) return false;
                        var lc = Convert.ToDouble(lvObj);
                        var rc = Convert.ToDouble(rvObj);
                        return pred(lc.CompareTo(rc));
                    }
                    if (expr.Contains(">=")) return Cmp(">=", c => c >= 0);
                    if (expr.Contains("<=")) return Cmp("<=", c => c <= 0);
                    if (expr.Contains(">")) return Cmp(">", c => c > 0);
                    if (expr.Contains("<")) return Cmp("<", c => c < 0);

                    // 单变量真值判断：存在且为真
                    var v = ResolveValue(expr, context, out bool exists);
                    if (!exists) return false;
                    if (v is bool b) return b;
                    if (v is string s) return !string.IsNullOrWhiteSpace(s);
                    if (v is IConvertible) return Convert.ToDouble(v) != 0.0;
                    return v != null;
                }

                // 支持 || 优先级低于 &&
                bool EvalAnd(string s)
                {
                    var andParts = s.Split(new[] { "&&" }, StringSplitOptions.None);
                    foreach (var p in andParts)
                    {
                        if (!EvalSingle(p)) return false;
                    }
                    return true;
                }

                var orParts = condition.Split(new[] { "||" }, StringSplitOptions.None);
                foreach (var part in orParts)
                {
                    if (EvalAnd(part)) return true;
                }
                return false;
            }
            catch
            {
                // 解析异常视为条件不满足，避免误执行
                return false;
            }

            object ParseLiteral(string token)
            {
                token = token.Trim().Trim('\"');
                if (double.TryParse(token, out var d)) return d;
                if (bool.TryParse(token, out var b)) return b;
                return token;
            }

            object ResolveValue(string key, NodeContext ctx, out bool found)
            {
                found = false;
                key = key.Trim();
                if (ctx.InputData != null && ctx.InputData.TryGetValue(key, out var v1)) { found = true; return v1; }
                if (ctx.GlobalVariables != null && ctx.GlobalVariables.TryGetValue(key, out var v2)) { found = true; return v2; }
                if (node.Parameters != null && node.Parameters.TryGetValue(key, out var v3)) { found = true; return v3; }
                return null;
            }
        }
    }
}

