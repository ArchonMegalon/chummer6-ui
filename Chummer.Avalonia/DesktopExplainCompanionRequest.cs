using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed record DesktopExplainCompanionRequest(
    string Title,
    string SurfaceId,
    string SurfaceLabel,
    IReadOnlyList<DesktopTrustReceiptSection> Sections,
    string? SurfaceFamilyId = null,
    string? RulesetId = null,
    string? WorkspaceId = null,
    string? RuntimeFingerprint = null,
    string? LaunchUri = null);
