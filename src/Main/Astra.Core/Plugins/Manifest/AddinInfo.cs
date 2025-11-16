namespace Astra.Core.Plugins.Manifest
{
    public class AddinInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Copyright { get; set; }
        public string Website { get; set; }
        public string IconPath { get; set; }
        public RuntimeInfo Runtime { get; set; } = new();
        public List<AddinDependency> Dependencies { get; set; } = new();
        public PermissionsInfo Permissions { get; set; } = new();
    }
}
