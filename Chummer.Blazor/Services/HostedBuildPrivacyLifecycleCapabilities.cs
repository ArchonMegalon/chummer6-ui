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
/// V2 reports the selected store's shipped behavior without turning implementation
/// into a public launch claim. PostgreSQL has an atomic content-free deletion
/// journal, owner-workspace erasure, and readiness replay; the file store does not.
/// </summary>
public sealed class HostedBuildPrivacyLifecycleCapabilities : IWorkspacePrivacyLifecycleCapabilities
{
    public const string ContractName = "chummer.hosted_build_privacy_lifecycle";
    public const int ContractVersion = 2;
    public const string ReviewRequiredStatus = "review_required";
    public const string DocumentedStatus = "documented";

    public const string ActiveRecordDelete = "active-record-delete";
    public const string MemoryOnlyRecovery = "memory-only-recovery";
    public const string AtomicDeletionJournal = "atomic-deletion-journal";
    public const string AutomaticDeletionReplay = "automatic-deletion-replay";
    public const string OwnerWorkspaceErasure = "owner-workspace-erasure";
    public const string NoDeleteReplay = "no-delete-replay";
    public const string NoOwnerErasure = "no-owner-erasure";
    public const string ProductionRecoveryUnverified = "production-recovery-unverified";

    public const string PermanentDeleteClaim = "permanent-delete";
    public const string DurableRecoveryClaim = "durable-recovery";
    public const string AccountErasureClaim = "account-erasure";

    private static readonly ReadOnlyCollection<HostedBuildPrivacyLifecycleFact> FileStoreFacts =
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

    private static readonly ReadOnlyCollection<HostedBuildPrivacyLifecycleFact> PostgresFacts =
        Array.AsReadOnly<HostedBuildPrivacyLifecycleFact>(
        [
            new(
                ActiveRecordDelete,
                "Active workspace deletion",
                "Delete removes the active workspace row in the same transaction that records its deletion receipt."),
            new(
                AtomicDeletionJournal,
                "Content-free deletion journal",
                "The PostgreSQL store keeps keyed identifiers, timestamps, revision metadata, and a receipt hash. It does not copy character content into the journal."),
            new(
                AutomaticDeletionReplay,
                "Deletion replay before readiness",
                "Retained deletion receipts are replayed before the PostgreSQL store can report ready, including after a stale data restore."),
            new(
                OwnerWorkspaceErasure,
                "Owner workspace erasure",
                "The store can delete every active Hosted Build workspace for one owner and fence recreation during the 35-day replay window. This is not whole-account erasure."),
            new(
                MemoryOnlyRecovery,
                "Memory-only conflict recovery",
                "A complete conflict recovery payload stays in this browser tab's memory unless you explicitly save a file."),
            new(
                ProductionRecoveryUnverified,
                "Independent recovery proof still required",
                "The journal and replay code are tested, but recovery from a production backup has not yet proved that the deletion journal survives independently of the restored content snapshot.")
        ]);

    private static readonly ReadOnlyCollection<string> ProhibitedClaims =
        Array.AsReadOnly(
        [
            PermanentDeleteClaim,
            DurableRecoveryClaim,
            AccountErasureClaim
        ]);

    public static HostedBuildPrivacyLifecycleCapabilities Instance { get; } = new(
        new HostedBuildWorkspaceStoreSelection(
            Provider: "file",
            MultiInstanceSafe: false,
            DurabilityBoundary: "single_instance_local_filesystem"));

    public HostedBuildPrivacyLifecycleSnapshot Current { get; }

    public HostedBuildPrivacyLifecycleCapabilities(HostedBuildWorkspaceStoreSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        bool postgres = string.Equals(
            selection.Provider,
            "postgresql",
            StringComparison.Ordinal);
        Current = new HostedBuildPrivacyLifecycleSnapshot(
            ContractName,
            ContractVersion,
            ReviewRequiredStatus,
            ReviewRequired: true,
            Facts: postgres ? PostgresFacts : FileStoreFacts,
            ProhibitedClaims: ProhibitedClaims,
            Summary: postgres
                ? "PostgreSQL deletes active Hosted Build workspaces with a content-free receipt, replays retained receipts before readiness, and supports owner-workspace erasure. Whole-account deletion and production backup recovery are not yet proven."
                : "The file store deletes only the active workspace record. Conflict recovery is memory-only; deletion replay and owner-workspace erasure are unavailable, and production recovery remains unverified.");
    }
}
