using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace Astra.Services.Logging
{
    /// <summary>
    /// 批次内多子流程共享同一运行日志文件的写入端：按路径引用计数，最后一处 Dispose 时关闭流。
    /// </summary>
    internal static class ExecutionBatchRunLogFileHub
    {
        private sealed class State
        {
            public readonly object Gate = new();
            public StreamWriter? Writer;
            public int RefCount;
        }

        private static readonly ConcurrentDictionary<string, State> States = new(StringComparer.OrdinalIgnoreCase);

        public static void EnterSession(string filePath, string executionId, string? workFlowKey, string? serialNumber)
        {
            var key = NormalizeKey(filePath);
            var state = States.GetOrAdd(key, _ => new State());
            lock (state.Gate)
            {
                if (state.Writer == null)
                {
                    var dir = Path.GetDirectoryName(key);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    state.Writer = new StreamWriter(
                            new FileStream(key, FileMode.Create, FileAccess.ReadWrite, FileShare.Read),
                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                        { AutoFlush = true };

                    state.Writer.WriteLine("# Astra Batch Workflow Run Log v1");
                    state.Writer.WriteLine($"# sn={ExecutionRunLogFileChunkSink.FormatSnForLog(serialNumber)}");
                    state.Writer.WriteLine($"# batch_log_file={key}");
                    state.Writer.WriteLine($"# batch_started_utc={DateTimeOffset.Now:O}");
                    state.Writer.WriteLine();
                }
                else
                {
                    state.Writer.WriteLine();
                    state.Writer.WriteLine();
                    state.Writer.WriteLine("########## 下一子流程 / 执行分段 ##########");
                }

                state.Writer.WriteLine($"# segment_start_utc={DateTimeOffset.Now:O}");
                state.Writer.WriteLine($"# execution_id={(executionId ?? string.Empty).Trim()}");
                var wf = string.IsNullOrWhiteSpace(workFlowKey) ? null : workFlowKey.Trim();
                if (wf != null)
                    state.Writer.WriteLine($"# workflow_key={wf}");
                state.Writer.WriteLine();
                state.Writer.Flush();
                state.RefCount++;
            }
        }

        public static void Append(string filePath, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var key = NormalizeKey(filePath);
            if (!States.TryGetValue(key, out var state))
                return;

            lock (state.Gate)
            {
                if (state.Writer == null)
                    return;
                state.Writer.Write(text);
                if (!text.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                    state.Writer.WriteLine();
                state.Writer.Flush();
            }
        }

        public static void LeaveSession(string filePath)
        {
            var key = NormalizeKey(filePath);
            if (!States.TryGetValue(key, out var state))
                return;

            lock (state.Gate)
            {
                state.RefCount--;
                if (state.RefCount > 0)
                    return;

                try
                {
                    state.Writer?.Flush();
                    state.Writer?.Dispose();
                }
                catch
                {
                    // ignore
                }

                state.Writer = null;
                States.TryRemove(key, out _);
            }
        }

        private static string NormalizeKey(string filePath)
        {
            try
            {
                return Path.GetFullPath((filePath ?? string.Empty).Trim());
            }
            catch
            {
                return filePath.Trim();
            }
        }
    }
}
