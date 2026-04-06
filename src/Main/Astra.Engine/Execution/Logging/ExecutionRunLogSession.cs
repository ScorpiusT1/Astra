using Astra.Core.Logs;
using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Logging
{
    /// <summary>
    /// 单次执行内：流程边界立即写；节点内缓冲，弹出时成块写出；嵌套子流程块并入父 WorkFlow 节点缓冲。
    /// </summary>
    public sealed class ExecutionRunLogSession : IExecutionRunLogSession
    {
        private readonly IExecutionRunLogChunkSink _sink;
        private readonly AsyncLocal<Stack<Frame>?> _stack = new();
        private readonly object _segmentLock = new();
        private readonly int _maxLinesPerNode;
        private readonly int _maxCharsPerNode;
        private bool _disposed;

        public string ExecutionId { get; }

        public ExecutionRunLogSession(
            string executionId,
            IExecutionRunLogChunkSink sink,
            int maxLinesPerNode = 8000,
            int maxCharsPerNode = 524288)
        {
            ExecutionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _maxLinesPerNode = Math.Max(256, maxLinesPerNode);
            _maxCharsPerNode = Math.Max(4096, maxCharsPerNode);
        }

        public void Write(string level, string message)
        {
            if (_disposed || string.IsNullOrWhiteSpace(message))
                return;

            var lvl = string.IsNullOrWhiteSpace(level) ? "INFO" : level.Trim().ToUpperInvariant();
            var line = $"{FormatTimestamp()} | {lvl} | exec={ExecutionId} | {message}";

            var stack = GetOrCreateStack();
            // 仅当栈顶为节点帧时写入该节点缓冲；栈顶为流程帧时视为「节点间隙」，避免写入父流程节点块。
            var target = GetWritableNodeFrameIfTop(stack);
            if (target != null)
            {
                lock (_segmentLock)
                {
                    if (target.Truncated)
                        return;
                    if (target.LineCount >= _maxLinesPerNode || target.CharBudget <= 0)
                    {
                        target.Segments.Add($"{FormatTimestamp()} | WARN | exec={ExecutionId} | … 节点日志已达上限（行或字节），后续行已丢弃 …");
                        target.Truncated = true;
                        return;
                    }

                    target.Segments.Add(line);
                    target.LineCount++;
                    target.CharBudget -= line.Length + 1;
                }

                return;
            }

            lock (_segmentLock)
                _sink.WriteImmediate(line + Environment.NewLine);
        }

        public void PushWorkflowScope(string workflowId, string workflowName, string workFlowKey)
        {
            if (_disposed)
                return;

            var stack = GetOrCreateStack();
            NodeFrame? owning = null;
            if (stack.Count > 0 && stack.Peek() is NodeFrame nf)
                owning = nf;

            var safeName = string.IsNullOrWhiteSpace(workflowName) ? workflowId : workflowName.Trim();
            var safeKey = workFlowKey?.Trim() ?? string.Empty;
            var header = $"######## WORKFLOW BEGIN id={Escape(workflowId)} name={Escape(safeName)} key={Escape(safeKey)} ########{Environment.NewLine}";

            stack.Push(new WorkflowFrame(owning));

            if (owning != null)
            {
                lock (_segmentLock)
                    owning.Segments.Add(header);
            }
            else
            {
                lock (_segmentLock)
                    _sink.WriteImmediate(header);
            }
        }

        public void PopWorkflowScope()
        {
            if (_disposed)
                return;

            var stack = _stack.Value;
            if (stack == null || stack.Count == 0)
                return;

            if (stack.Peek() is not WorkflowFrame wf)
                return;

            stack.Pop();
            var footer = $"######## WORKFLOW END ########{Environment.NewLine}";

            if (wf.OwningNodeFrame != null)
            {
                lock (_segmentLock)
                    wf.OwningNodeFrame.Segments.Add(footer);
            }
            else
            {
                lock (_segmentLock)
                    _sink.WriteImmediate(footer);
            }
        }

        public void PushNodeScope(Node node)
        {
            if (_disposed || node == null)
                return;

            var stack = GetOrCreateStack();
            stack.Push(new NodeFrame(
                node.Id ?? string.Empty,
                node.Name ?? string.Empty,
                node.NodeType ?? string.Empty,
                _maxCharsPerNode));
        }

        public void PopNodeScope()
        {
            if (_disposed)
                return;

            var stack = _stack.Value;
            if (stack == null || stack.Count == 0)
                return;

            if (stack.Peek() is not NodeFrame finished)
                return;

            stack.Pop();
            var block = BuildNodeBlockText(finished);
            RouteNodeBlock(stack, block);
        }

        public void TryRenameFileWithSerialNumber(string serialNumber)
        {
            if (_disposed || string.IsNullOrWhiteSpace(serialNumber))
                return;
            if (_sink is IRunLogFileRenameSink renameSink)
                renameSink.TryRenameWithSerialNumber(serialNumber.Trim());
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;
            _stack.Value = null;
            lock (_segmentLock)
                _sink.Dispose();
            return ValueTask.CompletedTask;
        }

        private Stack<Frame> GetOrCreateStack()
        {
            var s = _stack.Value;
            if (s == null)
            {
                s = new Stack<Frame>();
                _stack.Value = s;
            }

            return s;
        }

        private static NodeFrame? GetWritableNodeFrameIfTop(Stack<Frame> stack)
        {
            if (stack.Count == 0)
                return null;
            return stack.Peek() is NodeFrame nf ? nf : null;
        }

        private void RouteNodeBlock(Stack<Frame>? stack, string block)
        {
            if (string.IsNullOrEmpty(block))
                return;

            lock (_segmentLock)
            {
                if (stack == null || stack.Count == 0)
                {
                    _sink.WriteNodeBlock(block);
                    return;
                }

                if (stack.Peek() is WorkflowFrame wf && wf.OwningNodeFrame != null)
                {
                    wf.OwningNodeFrame.Segments.Add(block);
                    return;
                }

                _sink.WriteNodeBlock(block);
            }
        }

        private string BuildNodeBlockText(NodeFrame n)
        {
            var sb = new StringBuilder();
            sb.Append("---------- NODE BEGIN id=").Append(Escape(n.NodeId))
                .Append(" name=").Append(Escape(n.NodeName))
                .Append(" type=").Append(Escape(n.NodeType))
                .Append(" ----------").AppendLine();
            lock (_segmentLock)
            {
                foreach (var seg in n.Segments)
                {
                    sb.Append(seg);
                    if (seg.Length == 0 || !seg.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                        sb.AppendLine();
                }
            }

            sb.Append("---------- NODE END id=").Append(Escape(n.NodeId)).Append(" ----------").AppendLine();
            return sb.ToString();
        }

        private static string FormatTimestamp() =>
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);

        private static string Escape(string? s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Replace("|", "^", StringComparison.Ordinal).Replace(Environment.NewLine, " ", StringComparison.Ordinal);
        }

        private abstract class Frame { }

        private sealed class WorkflowFrame : Frame
        {
            public NodeFrame? OwningNodeFrame { get; }

            public WorkflowFrame(NodeFrame? owningNodeFrame) => OwningNodeFrame = owningNodeFrame;
        }

        private sealed class NodeFrame : Frame
        {
            public string NodeId { get; }
            public string NodeName { get; }
            public string NodeType { get; }
            public List<string> Segments { get; } = new();
            public int LineCount { get; set; }
            public int CharBudget { get; set; }
            public bool Truncated { get; set; }

            public NodeFrame(string nodeId, string nodeName, string nodeType, int charBudget)
            {
                NodeId = nodeId;
                NodeName = nodeName;
                NodeType = nodeType;
                CharBudget = charBudget;
            }
        }
    }
}
