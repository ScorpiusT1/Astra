using System.Text.Json.Serialization;

namespace Astra.Core.Nodes.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NodeExecutionState
    {
        Idle,
        Running,
        Paused,
        Success,
        Failed,
        Cancelled,
        Skipped
    }
}
