using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopInstallLinkingRuntimeTests
{
    [TestMethod]
    public void BuildSupportPortalRelativePathForInstall_includes_install_prefill_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForInstall(CreateState());

        StringAssert.Contains(path, "/contact?", StringComparison.Ordinal);
        StringAssert.Contains(path, "kind=install_help", StringComparison.Ordinal);
        StringAssert.Contains(path, "installationId=ins-avalonia-1", StringComparison.Ordinal);
        StringAssert.Contains(path, "releaseChannel=preview", StringComparison.Ordinal);
        StringAssert.Contains(path, "headId=avalonia", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForUpdate(
            CreateState(),
            new DesktopUpdateClientStatus(
                HeadId: "avalonia",
                InstalledVersion: "6.0.1-preview",
                ChannelId: "preview",
                Platform: "linux",
                Arch: "x64",
                UpdatesEnabled: true,
                AutoApply: false,
                ManifestLocation: "http://127.0.0.1:8091/downloads/manifest.json",
                LastCheckedAtUtc: DateTimeOffset.Parse("2026-03-28T14:00:00+00:00"),
                LastManifestVersion: "6.0.2-preview",
                LastManifestPublishedAtUtc: DateTimeOffset.Parse("2026-03-28T13:55:00+00:00"),
                LastError: "Manifest signature mismatch.",
                Status: "attention_required",
                RecommendedAction: "Review the promoted preview and route support before retrying.",
                RolloutState: "local_docker_preview",
                SupportabilityState: "local_docker_proven",
                SupportabilitySummary: "Local proof passed for install, build, and support closure.",
                KnownIssueSummary: "Portable artifact is still preview-only on this channel.",
                FixAvailabilitySummary: "Only verify fixes after this install can see the promoted archive.",
                ProofStatus: "passed",
                ProofGeneratedAtUtc: DateTimeOffset.Parse("2026-03-28T13:56:00+00:00")));

        StringAssert.Contains(path, "title=Desktop%20update%20posture%20needs%20review%20for%20avalonia", StringComparison.Ordinal);
        StringAssert.Contains(path, "Manifest%20signature%20mismatch.", StringComparison.Ordinal);
        StringAssert.Contains(path, "applicationVersion=6.0.1-preview", StringComparison.Ordinal);
        StringAssert.Contains(path, "Supportability%3A%20local_docker_proven", StringComparison.Ordinal);
        StringAssert.Contains(path, "Local%20release%20proof%3A%20passed", StringComparison.Ordinal);
        StringAssert.Contains(path, "Fix%20availability%3A%20Only%20verify%20fixes%20after%20this%20install%20can%20see%20the%20promoted%20archive.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForWorkspace_includes_workspace_follow_through_context()
    {
        WorkspaceListItem workspace = new(
            Id: new CharacterWorkspaceId("workspace-redmond"),
            Summary: new CharacterFileSummary(
                Name: "Redmond Edge",
                Alias: "Edge",
                Metatype: "Human",
                BuildMethod: "Priority",
                CreatedVersion: "SR6",
                AppVersion: "6.0.1-preview",
                Karma: 24,
                Nuyen: 18000,
                Created: true),
            LastUpdatedUtc: DateTimeOffset.Parse("2026-03-28T14:10:00+00:00"),
            RulesetId: "sr6.preview.v1",
            HasSavedWorkspace: true);

        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForWorkspace(CreateState(), workspace);

        StringAssert.Contains(path, "kind=bug_report", StringComparison.Ordinal);
        StringAssert.Contains(path, "Workspace%20follow-through%20needs%20help%20for%20Redmond%20Edge", StringComparison.Ordinal);
        StringAssert.Contains(path, "workspace-redmond", StringComparison.Ordinal);
        StringAssert.Contains(path, "sr6.preview.v1", StringComparison.Ordinal);
    }

    private static DesktopInstallLinkingState CreateState()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-03-28T14:00:00+00:00");
        return new DesktopInstallLinkingState(
            InstallationId: "ins-avalonia-1",
            HeadId: "avalonia",
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "linux",
            Arch: "x64",
            Status: "claimed",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 3,
            LastStartedAtUtc: now,
            ClaimedAtUtc: now,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public-key",
            PrivateKey: "private-key",
            ClaimTicketId: "ticket-1",
            LastClaimCode: "CLAIM1",
            LastClaimMessage: "This copy is now linked to your Hub account.",
            LastClaimError: null,
            LastClaimAttemptUtc: now,
            GrantId: "grant-1",
            GrantToken: "token-1",
            GrantIssuedAtUtc: now,
            GrantExpiresAtUtc: now.AddDays(30),
            UserId: "user-1",
            SubjectId: "subject-1");
    }
}
