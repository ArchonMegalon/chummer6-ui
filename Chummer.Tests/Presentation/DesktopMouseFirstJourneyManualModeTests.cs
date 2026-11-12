using Microsoft.VisualStudio.TestTools.UnitTesting;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using System.IO;
using System.Threading.Tasks;
using static Chummer.Avalonia.DesktopMouseFirstJourneyRunner;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Re-audit E5 regression tests for the manual-acceptance journey mode
/// validation. Reported separately from the SR5 career-attribute lane.
/// </summary>
[TestClass]
public sealed class DesktopMouseFirstJourneyManualModeTests
{
    private static ManualDraftConfirmation ConfirmedDraft() => new(
        new CharacterWorkspaceId("manual-runner"),
        new OwnerContextStamp(new OwnerScope("install-account:manual-test"), "test-authority", 1),
        ContentRevision: 8, DraftRevision: 2, DraftIdentity: "confirmed-core-draft");

    [TestMethod]
    [DataRow("attributes")]
    [DataRow("qualities")]
    [DataRow("skills")]
    public void Manual_confirmation_rejects_closed_or_cancelled_modal_even_with_matching_authority(string leg)
    {
        ManualDraftConfirmation expected = ConfirmedDraft();
        MustFail(() => RequireManualConfirmation(leg, false, 7, expected, expected));
    }

    [TestMethod]
    [DataRow("attributes")]
    [DataRow("qualities")]
    [DataRow("skills")]
    public void Manual_confirmation_requires_the_exact_reloaded_authoritative_draft_and_revision(string leg)
    {
        ManualDraftConfirmation expected = ConfirmedDraft();
        MustFail(() => RequireManualConfirmation(leg, true, 7, null, expected));
        MustFail(() => RequireManualConfirmation(leg, true, 7, expected, null));
        MustFail(() => RequireManualConfirmation(leg, true, 7, expected, expected with { ContentRevision = 7 }));
        MustFail(() => RequireManualConfirmation(leg, true, 7, expected, expected with { DraftRevision = 1 }));
        MustFail(() => RequireManualConfirmation(leg, true, 7, expected, expected with { DraftIdentity = "another-draft" }));
        MustFail(() => RequireManualConfirmation(leg, true, 7, expected,
            expected with { WorkspaceId = new CharacterWorkspaceId("another-runner") }));
        MustFail(() => RequireManualConfirmation(leg, true, 7, expected, expected with { Owner = null }));
        MustFail(() => RequireManualConfirmation(leg, true, 8, expected, expected));
        MustFail(() => RequireManualConfirmation(leg, true, 7,
            expected with { DraftIdentity = string.Empty }, expected with { DraftIdentity = string.Empty }));
        RequireManualConfirmation(leg, true, 7, expected, expected with { });
    }

    [TestMethod]
    public async Task Optional_screenshot_null_trace_off_or_failure_does_not_abort_required_work()
    {
        foreach (Func<Task<string?>>? capture in new Func<Task<string?>>?[]
        {
            null,
            () => Task.FromResult<string?>(null),
            () => throw new IOException("optional screenshot unavailable"),
            () => Task.FromException<string?>(new IOException("optional screenshot write failed"))
        })
        {
            int requiredRuns = 0;
            DesktopUserJourneyWorkflowEvidence evidence = await RunMandatoryWorkflowAsync(() =>
            {
                requiredRuns++;
                return Task.FromResult(Workflow(saved: true, identity: true));
            }, capture, capture);
            Assert.AreEqual(1, requiredRuns, "save/reload must execute with tracing off or a failed screenshot");
            Assert.AreEqual(0, evidence.ScreenshotPaths.Count);
            RequireWorkflowAssertions([evidence]);
        }
        Assert.AreEqual("frame.png", await CaptureOptionalScreenshotAsync(() => Task.FromResult<string?>("frame.png")));
    }

    [TestMethod]
    public async Task Screenshot_failure_cannot_hide_a_failed_required_save_reload_assertion()
    {
        await MustFailAsync(() => RunMandatoryWorkflowAsync(
            () => Task.FromResult(Workflow(saved: false, identity: true)),
            () => throw new IOException("diagnostic failure"), null));
        await MustFailAsync(() => RunMandatoryWorkflowAsync(
            () => Task.FromResult(Workflow(saved: true, identity: false)), null,
            () => throw new IOException("diagnostic failure")));
        await MustFailAsync(() => RunMandatoryWorkflowAsync(
            () => throw new InvalidOperationException("mandatory reload failed"), null, null));
        MustFail(() => RequireWorkflowAssertions([Workflow(true, true), Workflow(false, false)]));
    }

    private static async Task MustFailAsync(Func<Task<DesktopUserJourneyWorkflowEvidence>> action)
    {
        try { await action(); }
        catch (InvalidOperationException) { return; }
        Assert.Fail("required acceptance failure was swallowed");
    }

    private static DesktopUserJourneyWorkflowEvidence Workflow(bool saved, bool identity) => new(
        "minimal_character_build_save_reload", [], new Dictionary<string, bool>
        {
            ["character_created_saved_reloaded"] = saved,
            ["reload_preserved_character_identity"] = identity
        });

    private static void MustFail(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        Assert.Fail("required acceptance failure was swallowed");
    }

    [TestMethod]
    public void ValidateManualAcceptanceMode_fails_when_both_flags_enabled_and_attributes_unavailable()
    {
        string? failure = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ValidateManualAcceptanceMode(
            "1", "1", attributesAvailable: false);
        Assert.IsNotNull(failure);
        Assert.IsTrue(
            failure.Contains("mutually exclusive", StringComparison.Ordinal),
            "the mutual-exclusion failure must be reported first: " + failure);
    }

    [TestMethod]
    public void ValidateManualAcceptanceMode_fails_manual_only_when_attributes_unavailable()
    {
        string? failure = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ValidateManualAcceptanceMode(
            "1", string.Empty, attributesAvailable: false);
        Assert.IsNotNull(failure);
        Assert.IsTrue(
            failure.Contains("attributes step", StringComparison.Ordinal),
            "the attributes availability failure must be reported: " + failure);
    }

    [TestMethod]
    public void ValidateManualAcceptanceMode_admits_ordinary_manual_only_route()
    {
        Assert.IsNull(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ValidateManualAcceptanceMode(
            "1", string.Empty, attributesAvailable: true));
    }

    [TestMethod]
    public void ValidateManualAcceptanceMode_admits_ai_finish_without_manual_mode()
    {
        Assert.IsNull(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ValidateManualAcceptanceMode(
            string.Empty, "1", attributesAvailable: false));
    }

    [TestMethod]
    public void ValidateManualAcceptanceMode_gates_are_case_insensitive_and_off_by_default()
    {
        Assert.IsNull(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ValidateManualAcceptanceMode(
            "0", string.Empty, attributesAvailable: false));
        Assert.IsNull(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ValidateManualAcceptanceMode(
            string.Empty, "off", attributesAvailable: true));
        Assert.IsNull(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ValidateManualAcceptanceMode(
            "ON", "1", attributesAvailable: true));
    }
}
