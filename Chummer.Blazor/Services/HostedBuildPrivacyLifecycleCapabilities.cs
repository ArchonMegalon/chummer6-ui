using System.Collections.ObjectModel;

namespace Chummer.Blazor.Services;

/// <summary>
/// Runtime authority for the Hosted Build privacy lifecycle statements that may
/// be shown to a user. This is deliberately a capability-and-limitation
/// contract, not a roadmap: a claim becomes stronger only when the underlying
/// storage and recovery behavior has shipped and been verified.
/// </summary>
public interface IWorkspacePrivacyLifecycleCapabilities
{
    HostedBuildPrivacyLifecycleSnapshot Current { get; }
}

public sealed record HostedBuildPrivacyLifecycleFact(
    string Id,
    string Label,
    string Disclosure);

public sealed record HostedBuildPrivacyLifecycleSnapshot(
    string ContractName,
    int ContractVersion,
    string Status,
    bool ReviewRequired,
    IReadOnlyList<HostedBuildPrivacyLifecycleFact> Facts,
    IReadOnlyList<string> ProhibitedClaims,
    string Summary)
{
    public bool BlocksLaunch =>
        ReviewRequired
        || !string.Equals(Status, HostedBuildPrivacyLifecycleCapabilities.DocumentedStatus, StringComparison.Ordinal);
}

/// <summary>
/// V1 reflects the currently deployed Hosted Build lifecycle boundary:
/// application-record deletion and a bounded in-memory recovery handoff exist;
/// deletion replay, whole-owner erasure, and production-proven recovery do not.
/// </summary>
public sealed class HostedBuildPrivacyLifecycleCapabilities : IWorkspacePrivacyLifecycleCapabilities
{
    public const string ContractName = "chummer.hosted_build_privacy_lifecycle";
    public const int ContractVersion = 1;
    public const string ReviewRequiredStatus = "review_required";
    public const string DocumentedStatus = "documented";

    public const string ActiveRecordDelete = "active-record-delete";
    public const string MemoryOnlyRecovery = "memory-only-recovery";
    public const string NoDeleteReplay = "no-delete-replay";
    public const string NoOwnerErasure = "no-owner-erasure";
    public const string ProductionRecoveryUnverified = "production-recovery-unverified";

    public const string PermanentDeleteClaim = "permanent-delete";
    public const string DurableRecoveryClaim = "durable-recovery";
    public const string AccountErasureClaim = "account-erasure";

    private static readonly ReadOnlyCollection<HostedBuildPrivacyLifecycleFact> CurrentFacts =
        Array.AsReadOnly<HostedBuildPrivacyLifecycleFact>(
        [
            new(
                ActiveRecordDelete,
                "Active workspace record deletion",
                "Delete removes the active workspace record from the application store; it is not a claim that backups, logs, or provider recovery copies are erased."),
            new(
                MemoryOnlyRecovery,
                "Memory-only conflict recovery",
                "A complete conflict recovery payload can be retained only in this browser tab's memory until you explicitly save a file."),
            new(
                NoDeleteReplay,
                "No automatic deletion replay",
                "A deletion with an unknown outcome is not automatically replayed."),
            new(
                NoOwnerErasure,
                "No whole-owner erasure workflow",
                "Hosted Build does not yet provide whole-owner or account erasure."),
            new(
                ProductionRecoveryUnverified,
                "Production recovery unverified",
                "Backup and point-in-time recovery have not been verified in production.")
        ]);

    private static readonly ReadOnlyCollection<string> CurrentProhibitedClaims =
        Array.AsReadOnly(
        [
            PermanentDeleteClaim,
            DurableRecoveryClaim,
            AccountErasureClaim
        ]);

    public static HostedBuildPrivacyLifecycleCapabilities Instance { get; } = new();

    public HostedBuildPrivacyLifecycleSnapshot Current { get; } = new(
        ContractName,
        ContractVersion,
        ReviewRequiredStatus,
        ReviewRequired: true,
        Facts: CurrentFacts,
        ProhibitedClaims: CurrentProhibitedClaims,
        Summary:
            "Hosted Build currently deletes only the active workspace record. Conflict recovery is memory-only until you save it; deletion replay and whole-owner erasure are unavailable, and production recovery remains unverified.");

    private HostedBuildPrivacyLifecycleCapabilities()
    {
    }
}
