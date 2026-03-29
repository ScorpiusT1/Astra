using Astra.UI.Services;

namespace Astra.Services.Home;

public sealed class ManualBarcodeContext : IManualBarcodeContext
{
    public string? PendingBarcode { get; set; }
}
