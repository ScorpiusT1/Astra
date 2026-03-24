namespace Astra.Models
{
    public class ConfigTypeSelectionItem
    {
        public string DisplayName { get; set; } = string.Empty;

        public string Icon { get; set; } = "📄";

        public required Type ConfigType { get; set; }
    }
}
