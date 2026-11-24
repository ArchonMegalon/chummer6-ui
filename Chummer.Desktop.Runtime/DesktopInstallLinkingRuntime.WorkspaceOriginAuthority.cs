using Chummer.Application.Owners;

namespace Chummer.Desktop.Runtime;

internal sealed record DesktopOriginGrant(string InstallationId, string GrantId, string AccessToken,
    DateTimeOffset ExpiresAt, string SubjectId)
{
    public override string ToString() => nameof(DesktopOriginGrant);
}

public static partial class DesktopInstallLinkingRuntime
{
    // Capture identity AND current credential generation under the existing state
    // lock. Do not nest LoadOrCreateState inside an IOwnerContextLease: that lease
    // already holds this same non-reentrant file lock. No key leaves this method.
    internal static DesktopOriginGrant CaptureWorkspaceOriginGrant(string headId, OwnerContextStamp expected)
    {
        var release = DesktopRuntimeReleaseMetadata.Load(headId);
        var identity = DesktopRuntimePlatformIdentity.Current();
        var paths = DesktopInstallLinkingPaths.Create(headId, identity);
        using var stateLock = DesktopInstallLinkingStateStore.Acquire(paths);
        var state = LoadOrCreateStateUnderLock(release, identity, paths);
        var actual = CreateOwnerContextStamp(state);
        if (expected != new OwnerContextStamp(actual.Owner, actual.AuthorityInstanceId, actual.TransitionRevision)
            || expected.Owner.IsLocalSingleUser || !IsClaimed(state) || string.IsNullOrEmpty(state.GrantId)
            || string.IsNullOrEmpty(state.GrantToken) || string.IsNullOrEmpty(state.SubjectId)
            || state.GrantExpiresAtUtc is not { } expires || expires <= DateTimeOffset.UtcNow)
            throw new UnauthorizedAccessException("origin-owner-or-grant-unavailable");
        return new(state.InstallationId, state.GrantId, state.GrantToken, expires, state.SubjectId);
    }
}
