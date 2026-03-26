using System;

namespace Astra.Services.Logging
{
    public interface IUiLogService
    {
        event EventHandler<UiLogEntryEventArgs> LogAdded;

        void Write(string level, string message);

        void Info(string message);

        void Warn(string message);

        void Error(string message);
    }

    public sealed class UiLogEntryEventArgs : EventArgs
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;

        public string Level { get; init; } = "INFO";

        public string Message { get; init; } = string.Empty;
    }
}
