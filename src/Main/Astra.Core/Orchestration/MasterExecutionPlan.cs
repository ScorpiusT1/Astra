using Astra.Core.Nodes.Models;
using System.Collections.Generic;

namespace Astra.Core.Orchestration
{
    public sealed class SubWorkflowEntry
    {
        public string RefId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public WorkFlowNode Workflow { get; init; } = default!;
        public bool IsEnabled { get; init; } = true;
        public bool ContinueOnFailure { get; init; }
        public bool ExecuteLast { get; init; }
        public List<string> PredecessorIds { get; } = new();
    }

    public sealed class MasterExecutionPlan
    {
        public Dictionary<string, SubWorkflowEntry> Entries { get; init; } = new();
        public List<SubWorkflowEntry> OrderedEntries { get; init; } = new();
        public Dictionary<string, List<string>> Successors { get; init; } = new();
        public bool HasDependencies { get; init; }
    }
}
