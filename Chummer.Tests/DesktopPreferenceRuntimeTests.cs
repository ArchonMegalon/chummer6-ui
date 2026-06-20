#nullable enable annotations

using System;
using System.IO;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopPreferenceRuntimeTests
{
    [TestMethod]
    public void LoadOrCreateState_creates_default_preferences_when_missing()
    {
        using TestStateRootScope scope = new();

        DesktopPreferenceState loaded = DesktopPreferenceRuntime.LoadOrCreateState("avalonia");

        Assert.AreEqual(DesktopPreferenceStateRuntime.Normalize(DesktopPreferenceState.Default), loaded);
        Assert.IsTrue(File.Exists(scope.GetPreferenceStatePath("avalonia")));
    }

    [TestMethod]
    public void TryLoadState_returns_false_without_creating_default_preferences_when_missing()
    {
        using TestStateRootScope scope = new();

        bool loaded = DesktopPreferenceRuntime.TryLoadState("avalonia", out DesktopPreferenceState state);

        Assert.IsFalse(loaded);
        Assert.AreEqual(DesktopPreferenceState.Default, state);
        Assert.IsFalse(File.Exists(scope.GetPreferenceStatePath("avalonia")));
    }

    [TestMethod]
    public void SaveState_roundtrips_normalized_preferences()
    {
        using TestStateRootScope scope = new();

        DesktopPreferenceRuntime.SaveState(
            "avalonia",
            DesktopPreferenceState.Default with
            {
                UiScalePercent = 125,
                Theme = " dark-steel ",
                Language = "DE-DE",
                CompactMode = true,
                CharacterPriority = " SumToTen ",
                CharacterNotes = "Desk notes",
                StartupBehavior = " Restore roster ",
                UpdateChannel = " Preview weekly ",
                CharacterRosterPath = " /Tmp/Roster ",
                PdfViewerPath = " /usr/bin/zathura ",
                VisibleChromePolicy = " Compact shell only ",
                AnalyticsOptIn = true,
                DisableAiFeatures = true
            });

        DesktopPreferenceState loaded = DesktopPreferenceRuntime.LoadOrCreateState("avalonia");

        Assert.AreEqual(125, loaded.UiScalePercent);
        Assert.AreEqual("dark-steel", loaded.Theme);
        Assert.AreEqual("de-de", loaded.Language);
        Assert.IsTrue(loaded.CompactMode);
        Assert.AreEqual("SumToTen", loaded.CharacterPriority);
        Assert.AreEqual("Desk notes", loaded.CharacterNotes);
        Assert.AreEqual("Restore roster", loaded.StartupBehavior);
        Assert.AreEqual("Preview weekly", loaded.UpdateChannel);
        Assert.AreEqual("/Tmp/Roster", loaded.CharacterRosterPath);
        Assert.AreEqual("/usr/bin/zathura", loaded.PdfViewerPath);
        Assert.AreEqual("Compact shell only", loaded.VisibleChromePolicy);
        Assert.AreEqual("de-de", loaded.SheetLanguage);
        Assert.IsTrue(loaded.AnalyticsOptIn);
        Assert.IsTrue(loaded.DisableAiFeatures);
    }

    [TestMethod]
    public void GetCurrentLanguage_prefers_explicit_override()
    {
        try
        {
            DesktopLocalizationCatalog.SetCurrentLanguageOverride("de-de");

            Assert.AreEqual("de-de", DesktopLocalizationCatalog.GetCurrentLanguage());
        }
        finally
        {
            DesktopLocalizationCatalog.SetCurrentLanguageOverride(null);
        }
    }

    [TestMethod]
    public void Ai_feature_filter_prefers_current_preference_without_hiding_normal_critter_options()
    {
        DesktopPreferenceState previous = DesktopPreferenceStateRuntime.Current;
        try
        {
            DesktopPreferenceStateRuntime.SetCurrent(DesktopPreferenceState.Default with { DisableAiFeatures = true });

            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.AreAiCharacterOptionsDisabled());
            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "A.I."));
            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "A.I. - 6 Depth"));
            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "Metasapient A.I."));
            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "4e A.I.s"));
            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "AIs"));
            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "AIs - 6 Depth"));
            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "Metasapient AIs"));
            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "E-Ghost"));
            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "Xenosapient"));
            Assert.IsFalse(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "New Critter"));
            Assert.IsFalse(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "Critter Powers"));
            Assert.IsFalse(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "Metasapient"));
            Assert.IsFalse(DesktopAiFeaturePreferenceFilter.ShouldHideCharacterOrCompanionOption(true, "Human"));
        }
        finally
        {
            DesktopPreferenceStateRuntime.SetCurrent(previous);
        }
    }

    [TestMethod]
    public void Ai_feature_filter_reads_saved_winforms_preference_for_legacy_dialogs()
    {
        using TestStateRootScope scope = new();
        DesktopPreferenceState previous = DesktopPreferenceStateRuntime.Current;
        try
        {
            DesktopPreferenceStateRuntime.SetCurrent(DesktopPreferenceState.Default);
            DesktopPreferenceRuntime.SaveState(
                "winforms",
                DesktopPreferenceState.Default with { DisableAiFeatures = true });

            Assert.IsTrue(DesktopAiFeaturePreferenceFilter.AreAiCharacterOptionsDisabled());
        }
        finally
        {
            DesktopPreferenceStateRuntime.SetCurrent(previous);
        }
    }

    private sealed class TestStateRootScope : IDisposable
    {
        private readonly string _tempRoot;
        private readonly string? _priorRoot;

        public TestStateRootScope()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"desktop-preference-runtime-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);
            _priorRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", _tempRoot);
        }

        public string GetPreferenceStatePath(string headId)
            => Path.Combine(_tempRoot, "Chummer6", "preferences", headId, "state.json");

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", _priorRoot);
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
    }
}
