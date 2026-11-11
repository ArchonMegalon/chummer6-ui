using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Re-audit E5 regression tests for the manual-acceptance journey mode
/// validation. Reported separately from the SR5 career-attribute lane.
/// </summary>
[TestClass]
public sealed class DesktopMouseFirstJourneyManualModeTests
{
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
