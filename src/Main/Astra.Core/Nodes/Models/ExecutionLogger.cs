using System;

namespace Astra.Core.Nodes.Models
{
    public readonly struct ExecutionLogger
    {
        private readonly Action<string, string>? _writer;
        private readonly string _scope;

        internal ExecutionLogger(Action<string, string>? writer, string scope)
        {
            _writer = writer;
            _scope = scope ?? string.Empty;
        }

        public void Write(string level, string message)
        {
            if (_writer == null || string.IsNullOrWhiteSpace(message))
                return;

            var finalMessage = string.IsNullOrWhiteSpace(_scope) ? message : $"[{_scope}] {message}";
            _writer.Invoke(level, finalMessage);
        }

        public void Info(string message) => Write("INFO", message);

        public void Warn(string message) => Write("WARN", message);

        public void Error(string message) => Write("ERROR", message);
    }

    public static class ExecutionLoggerExtensions
    {
        public static ExecutionLogger CreateExecutionLogger(this NodeContext? context, string scope = "")
        {
            var writer = context?.GetMetadata<Action<string, string>>(ExecutionContextMetadataKeys.UiLogWriter);
            return new ExecutionLogger(writer, scope);
        }
    }
}
