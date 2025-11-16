namespace Astra.Core.Nodes.Models
{
    public class NodeContext
    {
        public Dictionary<string, object> InputData { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> GlobalVariables { get; set; } = new Dictionary<string, object>();
        public IServiceProvider ServiceProvider { get; set; }
    }
}
