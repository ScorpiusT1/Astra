using System;

namespace Astra.Services.Logging
{
    public sealed class UiLogService : IUiLogService
    {
        public event EventHandler<UiLogEntryEventArgs>? LogAdded;

        public void Write(string level, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var normalizedLevel = string.IsNullOrWhiteSpace(level) ? "INFO" : level.Trim().ToUpperInvariant();
            LogAdded?.Invoke(this, new UiLogEntryEventArgs
            {
                Timestamp = DateTime.Now,
                Level = normalizedLevel,
                Message = message
            });
        }

        public void Info(string message) => Write("INFO", message);

        public void Warn(string message) => Write("WARN", message);

        public void Error(string message) => Write("ERROR", message);
    }
}
