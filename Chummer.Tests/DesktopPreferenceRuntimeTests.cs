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
                VisibleChromePolicy = " Compact shell only "
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
