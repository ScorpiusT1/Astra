namespace Addins.Manifest
{
    public class Extension
    {
        public string Path { get; set; }
        public string TypeName { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}
