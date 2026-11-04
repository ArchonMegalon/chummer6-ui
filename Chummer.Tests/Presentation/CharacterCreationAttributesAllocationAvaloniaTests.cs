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
using Chummer.Tests.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

public sealed partial class CharacterCreationAttributesInteractionPresenterTests
{
    [TestMethod]
    public void Attribute_ui_refreshes_result_and_cost_from_the_Core_preview()
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var fixture = CreateFixture();
            var service = new FakeAttributesService(fixture.State);
            var overview = CreateOverview(fixture.State, service);
            var presenter = new CharacterCreationAttributesInteractionPresenter(service);
            var state = presenter.Load(overview).State!;
            var window = new CreationAttributesAllocationWindow(presenter, state, () => overview);
            window.Show();
            FindAttributeControl<Button>(window, CreationAttributesAllocationWindow.PriorityUpAutomationIdPrefix + "BOD")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var display = FindAttributeControl<TextBlock>(window, CreationAttributesAllocationWindow.ProjectionAutomationIdPrefix + "BOD");
            Assert.Contains("Result 2", display.Text!);
            Assert.Contains("priority cost 1", display.Text!);
            Assert.AreEqual(0, service.ConfirmCalls);
            FindAttributeControl<Button>(window, CreationAttributesAllocationWindow.AllocateButtonAutomationId)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsTrue(window.Confirmed);
            Assert.AreEqual(1, service.ConfirmCalls);
        });
    }

    [TestMethod]
    [DataRow("owner")]
    [DataRow("workspace")]
    [DataRow("revision")]
    public void Attribute_ui_discards_an_open_editor_after_context_changes(string transition)
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var fixture = CreateFixture();
            var service = new FakeAttributesService(fixture.State);
            var overview = CreateOverview(fixture.State, service);
            var presenter = new CharacterCreationAttributesInteractionPresenter(service);
            var state = presenter.Load(overview).State!;
            var window = new CreationAttributesAllocationWindow(presenter, state, () => overview);
            window.Show();
            var confirm = FindAttributeControl<Button>(window, CreationAttributesAllocationWindow.AllocateButtonAutomationId);
            int previews = service.PreviewCalls;
            overview = ChangeContext(overview, fixture.State, service, transition);
            window.InvalidateIfContextChanged();
            Assert.IsFalse(window.IsVisible);
            Assert.IsInstanceOfType<TextBlock>(window.Content);
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
    public void Attribute_ui_rejects_a_load_that_finishes_after_context_changes(string transition)
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var fixture = CreateFixture();
            using var started = new ManualResetEventSlim();
            using var resume = new ManualResetEventSlim();
            var service = new FakeAttributesService(fixture.State);
            var overview = CreateOverview(fixture.State, service);
            var original = overview;
            service.BeforeLoad = () => { started.Set(); Assert.IsTrue(resume.Wait(TimeSpan.FromSeconds(5))); };
            var presenter = new CharacterCreationAttributesInteractionPresenter(service);
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
                new CreationAttributesAllocationWindow(presenter, state, () => overview));
            Assert.AreEqual(0, service.PreviewCalls);
            Assert.AreEqual(0, service.ConfirmCalls);
        });
    }

    private static T FindAttributeControl<T>(Control root, string id) where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);
}
