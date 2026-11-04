using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Chummer.Avalonia.Creation;
using Chummer.Presentation.Overview;
using Chummer.Contracts.Characters;
using Chummer.Tests.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

public sealed partial class CharacterCreationSkillsInteractionPresenterTests
{
    [TestMethod]
    public void Skill_ui_refreshes_result_and_cost_from_the_Core_preview()
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var fixture = CreateFixture();
            var service = new FakeSkillsService(fixture.State);
            var overview = CreateOverview(fixture.State, service);
            var presenter = new CharacterCreationSkillsInteractionPresenter(service);
            var state = presenter.Load(overview).State!;
            var window = new CreationSkillsAllocationWindow(presenter, state, () => overview);
            window.Show();
            FindSkillControl<Button>(window, CreationSkillsAllocationWindow.SkillUpAutomationIdPrefix + "pistols")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var display = FindSkillControl<TextBlock>(window, "creation-skills-projection-pistols");
            Assert.Contains("Result 1", display.Text!);
            Assert.Contains("point cost 1", display.Text!);
            Assert.AreEqual(0, service.ConfirmCalls);
            FindSkillControl<Button>(window, CreationSkillsAllocationWindow.ConfirmButtonAutomationId)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsTrue(window.Confirmed);
            Assert.AreEqual(1, service.ConfirmCalls);
        });
    }

    [TestMethod]
    [DataRow("owner")]
    [DataRow("workspace")]
    [DataRow("revision")]
    public void Skill_ui_discards_an_open_editor_after_context_changes(string transition)
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var fixture = CreateFixture();
            var service = new FakeSkillsService(fixture.State);
            var overview = CreateOverview(fixture.State, service);
            var presenter = new CharacterCreationSkillsInteractionPresenter(service);
            var state = presenter.Load(overview).State!;
            var window = new CreationSkillsAllocationWindow(presenter, state, () => overview);
            window.Show();
            var confirm = FindSkillControl<Button>(window, CreationSkillsAllocationWindow.ConfirmButtonAutomationId);
            int previews = service.PreviewCalls;
            overview = ChangeContext(overview, fixture.State, service, transition);
            window.InvalidateIfContextChanged();
            Assert.IsFalse(window.IsVisible);
            Assert.IsNull(window.Content);
            Assert.IsFalse(confirm.IsEnabled);
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, service.ConfirmCalls);
            Assert.AreEqual(previews, service.PreviewCalls);
        });
    }

    [TestMethod]
    [DataRow("owner")]
    [DataRow("workspace")]
    [DataRow("revision")]
    public void Skill_ui_rejects_a_load_that_finishes_after_context_changes(string transition)
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var fixture = CreateFixture();
            using var started = new ManualResetEventSlim();
            using var resume = new ManualResetEventSlim();
            var service = new FakeSkillsService(fixture.State);
            var overview = CreateOverview(fixture.State, service);
            var original = overview;
            service.BeforeLoad = () => { started.Set(); Assert.IsTrue(resume.Wait(TimeSpan.FromSeconds(5))); };
            var presenter = new CharacterCreationSkillsInteractionPresenter(service);
            var loading = Task.Run(() => presenter.Load(original));
            try
            {
                Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(5)));
                overview = ChangeContext(overview, fixture.State, service, transition);
            }
            finally { resume.Set(); }
            var state = loading.GetAwaiter().GetResult().State!;
            Assert.IsNotNull(state);
            Assert.IsFalse(presenter.IsCurrentContext(overview));
            Assert.Throws<InvalidOperationException>(() =>
                new CreationSkillsAllocationWindow(presenter, state, () => overview));
            Assert.AreEqual(0, service.PreviewCalls);
            Assert.AreEqual(0, service.ConfirmCalls);
        });
    }

    [TestMethod]
    public void Skill_editor_preserves_specializations_native_and_unshown_allocations()
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var original = CreateFixture().State;
            CharacterCreationSkillAllocation[] selected = [new("pistols", "active", 4, "semi-auto", false), new("security-design", "knowledge", null, null, true), new("retained-hidden", "active", 2, "retained-spec", false)];
            var draft = new CharacterCreationSkillsDraft("draft", original.Binding.WorkspaceId, 1, 7, "raw", 0, "prerequisite", "authority", 1, "attributes", "skills", "runtime", "inputs", 46, 4, 10, 2, 0, 0, 0,
                selected, [new("firearms", 2)], original.Skills, original.SkillGroups, [], [], false, "key", "preview", "command", "draft");
            var state = original with { PendingDraft = draft };
            var service = new FakeSkillsService(state); var overview = CreateOverview(state, service);
            var presenter = new CharacterCreationSkillsInteractionPresenter(service);
            var window = new CreationSkillsAllocationWindow(presenter, presenter.Load(overview).State!, () => overview);
            window.Show();
            CollectionAssert.AreEqual(selected, service.LastPreviewAllocations!.ToArray());
            FindSkillControl<Button>(window, CreationSkillsAllocationWindow.SkillUpAutomationIdPrefix + "pistols").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual("semi-auto", service.LastPreviewAllocations!.Single(x => x.SourceSkillId == "pistols").SpecializationOptionId);
            Assert.AreEqual(5, service.LastPreviewAllocations!.Single(x => x.SourceSkillId == "pistols").Rating);
            Assert.AreEqual(selected[1], service.LastPreviewAllocations!.Single(x => x.SourceSkillId == "security-design"));
            Assert.AreEqual(selected[2], service.LastPreviewAllocations!.Single(x => x.SourceSkillId == "retained-hidden"));
            Assert.AreEqual(2, service.LastPreviewGroupAllocations!.Single().Rating);
            window.Close(); Assert.AreEqual(0, service.ConfirmCalls);
        });
    }

    [TestMethod]
    public void Skill_editor_collects_specialization_and_native_language_choices()
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var original = CreateFixture().State;
            var state = original with { Authority = original.Authority with {
                ActiveSkills = [new("pistols", "active", "Pistols", "Combat", "AGI", "Firearms", false, "source", [new("semi-auto", "Semi-automatic", "source")], [])],
                KnowledgeSkills = [new CharacterCreationSkillCatalogEntry("security-design", "knowledge", "Test language", "Language", "INT", null, false, "source", [], []) { CanBeNativeLanguage = true }]
            }};
            var service = new FakeSkillsService(state); var overview = CreateOverview(state, service);
            var presenter = new CharacterCreationSkillsInteractionPresenter(service);
            var window = new CreationSkillsAllocationWindow(presenter, presenter.Load(overview).State!, () => overview);
            window.Show();
            FindSkillControl<Button>(window, CreationSkillsAllocationWindow.SkillUpAutomationIdPrefix + "pistols").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            FindSkillControl<ComboBox>(window, "creation-skills-specialization-pistols").SelectedIndex = 1;
            FindSkillControl<CheckBox>(window, "creation-skills-native-security-design").IsChecked = true;
            Assert.AreEqual("semi-auto", service.LastPreviewAllocations!.Single(x => x.SourceSkillId == "pistols").SpecializationOptionId);
            var native = service.LastPreviewAllocations!.Single(x => x.SourceSkillId == "security-design");
            Assert.IsTrue(native.IsNativeLanguage); Assert.IsNull(native.Rating);
            FindSkillControl<ComboBox>(window, "creation-skills-specialization-pistols").SelectedIndex = 0;
            Assert.IsNull(service.LastPreviewAllocations!.Single(x => x.SourceSkillId == "pistols").SpecializationOptionId);
            Assert.AreEqual(0, service.ConfirmCalls);
            window.Close();
        });
    }

    private static T FindSkillControl<T>(Control root, string id) where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);
}
