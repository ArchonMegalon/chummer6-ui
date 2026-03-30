namespace Chummer.Presentation.Overview;

public sealed record DesktopHomePortableExchangePreview(
    string CampaignId,
    string CompatibilityState,
    string ContextSummary,
    string ReceiptSummary,
    string NextSafeAction,
    string AssetScopeSummary,
    IReadOnlyList<string> SupportedExchangeFormats,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Watchouts);
