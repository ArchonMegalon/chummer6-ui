using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Files;
using Chummer.Infrastructure.Xml;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationLoadoutInteractionPresenterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("weapon")]
    [DataRow("armor")]
    [DataRow("both")]
    public void Actual_confirmed_choices_reopen_with_original_instances_and_costs(string kind)
    {
        using var fixture = Ready();
        var service = new Probe(new OwnerBoundCharacterCreationLoadoutService(fixture.Store, fixture.Owners, fixture.Resolver));
        var presenter = new CharacterCreationLoadoutInteractionPresenter(service);
        var display = Overview(fixture, fixture.Owners.Capture());
        var loaded = presenter.Load(display);
        Required(loaded.Value, string.Join(",", loaded.Blockers));
        var selections = Select(loaded.Value, kind);
        var before = fixture.Store.Get(fixture.WorkspaceId).Value!;
        var files = fixture.CaptureFiles();
        var result = presenter.Prepare(display, selections);
        var prepared = Required(result.Prepared, string.Join(",", result.Blockers));
        fixture.AssertFilesUnchanged(files);
        var applied = presenter.Confirm(display, Confirm(prepared));
        var receipt = Required(applied.Receipt, string.Join(",", applied.Blockers));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, applied.Outcome);
        Assert.AreEqual(1, service.ConfirmCalls);
        var saved = fixture.Store.Get(fixture.WorkspaceId).Value!;
        Assert.AreEqual(before.Document.Content, saved.Document.Content);
        Assert.AreEqual(before.ContentRevision + 1, saved.ContentRevision);
        Assert.AreEqual(receipt.Draft.DraftDigest, saved.Document.AuxiliaryState.CharacterCreationLoadoutDraft!.DraftDigest);
        var reopened = presenter.Load(Overview(fixture, fixture.Owners.Capture()));
        Required(reopened.Value);
        CollectionAssert.AreEqual(selections.Weapons.ToArray(), reopened.Value.Inputs.OriginalSelections.Weapons.ToArray());
        CollectionAssert.AreEqual(selections.Armor.ToArray(), reopened.Value.Inputs.OriginalSelections.Armor.ToArray());
        Assert.AreEqual(receipt.Draft.DraftDigest, reopened.Value.Inputs.PendingDraft!.DraftDigest);
        Assert.AreEqual(prepared.Preview.Projection.PurchaseCost, reopened.Value.Inputs.PendingDraft.PurchaseCost);
        foreach (var line in receipt.Draft.Lines)
        {
            Assert.AreEqual(line.UnitCost, loaded.Value.Inputs.Authority.Options.Single(row => row.OptionId == line.OptionId).UnitCost);
            Assert.HasCount(line.Quantity, receipt.Draft.Instances.Single(row => row.OptionId == line.OptionId).InstanceIds);
        }
        var lookup = presenter.LookupReceipt(display, prepared);
        Assert.AreEqual(receipt.ReceiptDigest, lookup.Receipt?.ReceiptDigest);
        presenter.Confirm(display, Confirm(prepared));
        Assert.AreEqual(1, service.ConfirmCalls, "A submitted preview must never replay Confirm.");
    }

    [TestMethod]
    public void Cancel_cloned_preparation_owner_ABA_stale_display_and_price_drift_never_commit()
    {
        using var fixture = Ready();
        var owners = new MutableOwners();
        var service = new Probe(new OwnerBoundCharacterCreationLoadoutService(fixture.Store, owners, fixture.Resolver));
        var presenter = new CharacterCreationLoadoutInteractionPresenter(service);
        var display = Overview(fixture, owners.Capture());
        var selections = Select(Required(presenter.Load(display).Value), "weapon");
        var files = fixture.CaptureFiles();
        CharacterCreationLoadoutPreparedPreview Prepare() => Required(presenter.Prepare(display, selections).Prepared);
        var canceled = Prepare();
        presenter.Cancel(canceled);
        Assert.IsNull(presenter.Confirm(display, Confirm(canceled)).Receipt);
        var issued = Prepare();
        Assert.IsNull(presenter.Confirm(display, Confirm(issued) with { ExplicitlyConfirmed = false }).Receipt);
        Assert.IsNull(presenter.Confirm(display, Confirm(issued) with { PreviewDigest = new string('0', 64) }).Receipt);
        Assert.IsNull(presenter.Confirm(display, Confirm(issued with { })).Receipt);
        Assert.IsNull(presenter.Confirm(display with { }, Confirm(issued)).Receipt);
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Assert.ThrowsExactly<OperationCanceledException>(() => presenter.Confirm(display, Confirm(issued), cancellation.Token));
        }
        service.ChangePreviewPrice = true;
        Assert.IsNull(presenter.Confirm(display, Confirm(issued)).Receipt);
        service.ChangePreviewPrice = false;
        owners.Set(new OwnerScope("loadout-other-owner"));
        owners.Set(OwnerScope.LocalSingleUser);
        Assert.IsNull(presenter.Confirm(display, Confirm(issued)).Receipt, "The original owner value cannot erase its A→B→A history.");
        Assert.AreEqual(0, service.ConfirmCalls);
        fixture.AssertFilesUnchanged(files);
    }

    [TestMethod]
    public void Another_real_purchase_invalidates_the_prior_display_and_saved_rows_cannot_be_removed()
    {
        using var fixture = Ready();
        var service = new Probe(new OwnerBoundCharacterCreationLoadoutService(fixture.Store, fixture.Owners, fixture.Resolver));
        var presenter = new CharacterCreationLoadoutInteractionPresenter(service);
        var display = Overview(fixture, fixture.Owners.Capture());
        var state = Required(presenter.Load(display).Value);
        var pending = Required(presenter.Prepare(display, Select(state, "both")).Prepared);
        var weapon = Required(presenter.Prepare(display, Select(state, "weapon")).Prepared);
        var savedWeapon = Required(presenter.Confirm(display, Confirm(weapon)).Receipt);
        var files = fixture.CaptureFiles();
        Assert.IsNull(presenter.Confirm(display, Confirm(pending)).Receipt);
        var currentDisplay = Overview(fixture, fixture.Owners.Capture());
        Assert.IsNull(presenter.Prepare(currentDisplay, CharacterCreationLoadoutSelections.Empty).Prepared);
        fixture.AssertFilesUnchanged(files);
        var current = Required(presenter.Load(currentDisplay).Value);
        var armor = Select(current, "armor");
        var addition = Required(presenter.Prepare(currentDisplay,
            new(current.Inputs.OriginalSelections.Weapons, armor.Armor)).Prepared);
        var savedBoth = Required(presenter.Confirm(currentDisplay, Confirm(addition)).Receipt);
        Assert.AreEqual(2, service.ConfirmCalls);
        var original = savedWeapon.Draft.Instances.Single();
        CollectionAssert.AreEqual(original.InstanceIds.ToArray(),
            savedBoth.Draft.Instances.Single(row => row.OptionId == original.OptionId).InstanceIds.ToArray());
        Assert.AreEqual(savedWeapon.Draft.Lines.Single().LineDigest,
            savedBoth.Draft.Lines.Single(row => row.OptionId == original.OptionId).LineDigest);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Lost_response_uses_receipt_lookup_without_blind_replay(bool loseBeforeCommit)
    {
        using var fixture = Ready();
        var service = new Probe(new OwnerBoundCharacterCreationLoadoutService(fixture.Store, fixture.Owners, fixture.Resolver))
            { LoseAfterCommit = !loseBeforeCommit, LoseBeforeCommit = loseBeforeCommit };
        var presenter = new CharacterCreationLoadoutInteractionPresenter(service);
        var display = Overview(fixture, fixture.Owners.Capture());
        var state = Required(presenter.Load(display).Value);
        var prepared = Required(presenter.Prepare(display, Select(state, "both")).Prepared);
        var result = presenter.Confirm(display, Confirm(prepared));
        Assert.AreEqual(1, service.ConfirmCalls);
        Assert.AreEqual(1, service.LookupCalls);
        if (loseBeforeCommit)
        {
            Assert.AreEqual(CharacterCreationLoadoutInteractionPresenter.OutcomeUnknown, result.Outcome);
            Assert.IsNull(result.Receipt);
        }
        else Required(result.Receipt);
        presenter.Confirm(display, Confirm(prepared));
        presenter.LookupReceipt(display, prepared);
        Assert.AreEqual(1, service.ConfirmCalls);
        Assert.AreEqual(2, service.LookupCalls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Both_purchase_orders_reopen_genuine_baskets_and_share_the_Core_budget(bool gearFirst)
    {
        using var fixture = Ready();
        var loadout = new CharacterCreationLoadoutInteractionPresenter(
            new OwnerBoundCharacterCreationLoadoutService(fixture.Store, fixture.Owners, fixture.Resolver));
        var gear = new CharacterCreationGearInteractionPresenter(new CharacterCreationGearService(fixture.Store, fixture.Resolver),
            new OwnerBoundCharacterCreationGearService(fixture.Store, fixture.Owners, fixture.Resolver));
        var before = fixture.Store.Get(fixture.WorkspaceId).Value!;
        CharacterCreationLoadoutReceipt? loadoutReceipt = null;
        CharacterCreationGearReceipt? gearReceipt = null;
        void BuyLoadout()
        {
            var display = Overview(fixture, fixture.Owners.Capture());
            var state = Required(loadout.Load(display).Value);
            var prepared = Required(loadout.Prepare(display, Select(state, "both")).Prepared);
            loadoutReceipt = Required(loadout.Confirm(display, Confirm(prepared)).Receipt);
        }
        void BuyGear()
        {
            var display = Overview(fixture, fixture.Owners.Capture());
            var state = Required(gear.Load(display).State);
            var option = state.Authority.Options.Where(row => row.IsSelectable && row.PricingIsExact
                && row.AvailabilityIsExact && row.Blockers.Count == 0 && row.PackageCost > 0)
                .OrderBy(row => row.PackageCost).ThenBy(row => row.OptionId, StringComparer.Ordinal).First();
            var prepared = Required(gear.Prepare(display, [new(option.OptionId, 1)]).PreparedPreview);
            gearReceipt = Required(gear.Confirm(display,
                new(prepared, prepared.Preview.PreviewDigest, prepared.IdempotencyKey, true)).Receipt);
        }
        if (gearFirst) { BuyGear(); BuyLoadout(); } else { BuyLoadout(); BuyGear(); }
        var current = Overview(fixture, fixture.Owners.Capture());
        var savedLoadout = Required(loadout.Load(current).Value);
        var savedGear = Required(gear.Load(current).State);
        var resources = new OwnerBoundCharacterCreationResourcesService(fixture.Store, fixture.Owners, fixture.Resolver)
            .Load(fixture.Owners.Capture(), new(fixture.WorkspaceId));
        var budget = Required(resources.Value).Budget;
        Assert.AreEqual(loadoutReceipt!.Draft.DraftDigest, savedLoadout.Inputs.PendingDraft!.DraftDigest);
        Assert.AreEqual(gearReceipt!.DraftDigest, savedGear.PendingDraft!.DraftDigest);
        Assert.AreEqual(loadoutReceipt.Draft.PurchaseCost + gearReceipt.BasketCost, budget.KnownPurchaseCost);
        Assert.AreEqual(budget.TotalStartingNuyen - budget.KnownPurchaseCost, budget.RemainingNuyen);
        Assert.AreEqual(before.ContentRevision + 2, current.ContentRevision);
        Assert.AreEqual(before.Document.Content, fixture.Store.Get(fixture.WorkspaceId).Value!.Document.Content);
    }

    [TestMethod]
    public void Populated_unpriced_XML_is_never_promoted_to_trusted_manual_choices()
    {
        using var fixture = CompletionRecommendationTestFixture.Create(TestContext,
            originalLoadout: new XElement("weapons", new XElement("weapon", new XElement("name", "Unpriced original"))));
        var service = new OwnerBoundCharacterCreationLoadoutService(fixture.Store, fixture.Owners, fixture.Resolver);
        var files = fixture.CaptureFiles();
        var load = service.Prepare(fixture.Owners.Capture(), fixture.WorkspaceId);
        Assert.IsNull(load.Value);
        CollectionAssert.Contains(load.Blockers.ToArray(), CharacterCreationLoadoutCompletionSchemas.ExistingGraphUnsupported);
        fixture.AssertFilesUnchanged(files);
    }

    private static T Required<T>(T? value, string? message = null) where T : class
    {
        Assert.IsNotNull(value, message);
        return value;
    }

    private CompletionRecommendationTestFixture Ready()
    {
        var fixture = CompletionRecommendationTestFixture.Create(TestContext);
        try { fixture.SaveAttributes([]); fixture.PrepareContacts(); return fixture; }
        catch { fixture.Dispose(); throw; }
    }

    private static CharacterCreationLoadoutSelections Select(CharacterCreationLoadoutState state, string kind)
    {
        string Id(string category) => state.Inputs.Authority.Options.Where(row => row.Kind == category
            && row.IsSelectable && row.PricingIsExact && row.AvailabilityIsExact && row.Blockers.Count == 0 && row.UnitCost > 0)
            .OrderBy(row => row.UnitCost).ThenBy(row => row.OptionId, StringComparer.Ordinal).First().OptionId;
        return new(kind == "armor" ? [] : [new(Id(CharacterCreationLoadoutKinds.Weapon))],
            kind == "weapon" ? [] : [new(Id(CharacterCreationLoadoutKinds.Armor))]);
    }

    private static CharacterCreationLoadoutConfirmation Confirm(CharacterCreationLoadoutPreparedPreview prepared)
        => new(prepared, prepared.Preview.PreviewDigest, prepared.IdempotencyKey, true);

    private static CharacterOverviewState Overview(CompletionRecommendationTestFixture fixture, OwnerContextStamp owner)
    {
        var saved = fixture.Store.Get(fixture.WorkspaceId).Value!;
        ICharacterSectionQueries sections = new XmlCharacterSectionQueries(new CharacterSectionService(fixture.Resolver));
        var projection = sections.ParseOverview(new CharacterDocument(saved.Document.Content));
        var loaded = new WorkspaceOverviewLoadResult(projection.Profile, projection.Progress, projection.Skills,
            projection.Rules, projection.Build, projection.Movement, projection.Awakening,
            saved.ContentRevision, saved.SavedRevision, saved.Document);
        var session = new WorkspaceSessionState(saved.Id,
            [new OpenWorkspaceState(saved.Id, "Manual loadout", "Manual", saved.LastUpdatedUtc,
                RulesetDefaults.Sr5, saved.ContentRevision, saved.SavedRevision)], [saved.Id]) { OwnerContext = owner };
        return new WorkspaceOverviewStateFactory().CreateLoadedState(CharacterOverviewState.Empty,
            saved.Id, session, loaded, null, true) with { DisplayOwnerContext = owner };
    }

    private sealed class Probe(IOwnerBoundCharacterCreationLoadoutService inner) : IOwnerBoundCharacterCreationLoadoutService
    {
        public int ConfirmCalls, LookupCalls;
        public bool LoseBeforeCommit, LoseAfterCommit, ChangePreviewPrice;
        public CharacterCreationFoundationResult<CharacterCreationLoadoutState> Prepare(OwnerContextStamp owner, CharacterWorkspaceId id, CancellationToken ct = default)
            => inner.Prepare(owner, id, ct);
        public CharacterCreationFoundationResult<CharacterCreationLoadoutPreview> Preview(OwnerContextStamp owner, CharacterCreationLoadoutPreviewRequest request, CancellationToken ct = default)
        {
            var result = inner.Preview(owner, request, ct);
            if (!ChangePreviewPrice || result.Value is not { } preview) return result;
            var changed = preview with { TotalPurchaseCost = preview.TotalPurchaseCost + 1m };
            return result with { Value = changed with { PreviewDigest = CharacterCreationLoadoutDraftRules.PreviewDigest(changed) } };
        }
        public CharacterCreationFoundationResult<CharacterCreationLoadoutReceipt> Confirm(OwnerContextStamp owner, CharacterCreationLoadoutConfirmRequest request, CancellationToken ct = default)
        {
            ConfirmCalls++;
            if (LoseBeforeCommit) throw new IOException("synthetic-before-commit");
            var result = inner.Confirm(owner, request, ct);
            if (LoseAfterCommit && result.Value is not null) throw new IOException("synthetic-after-commit");
            return result;
        }
        public CharacterCreationFoundationResult<CharacterCreationLoadoutReceipt> LookupReceipt(OwnerContextStamp owner, CharacterWorkspaceId id, string key)
        { LookupCalls++; return inner.LookupReceipt(owner, id, key); }
    }

    private sealed class MutableOwners : IOwnerContextLeaseAccessor
    {
        private readonly object _gate = new();
        private OwnerContextStamp _stamp = new(OwnerScope.LocalSingleUser, Guid.NewGuid().ToString("N"), 0);
        public OwnerScope Current => Capture().Owner;
        public OwnerContextStamp Capture() { lock (_gate) return _stamp; }
        public void Set(OwnerScope owner) { lock (_gate) _stamp = _stamp with { Owner = owner, TransitionRevision = _stamp.TransitionRevision + 1 }; }
        public bool TryAcquire(OwnerContextStamp expected, [NotNullWhen(true)] out IOwnerContextLease? lease)
        {
            Monitor.Enter(_gate);
            if (expected != _stamp) { Monitor.Exit(_gate); lease = null; return false; }
            lease = new Lease(_gate, _stamp);
            return true;
        }
        private sealed class Lease(object gate, OwnerContextStamp stamp) : IOwnerContextLease
        {
            public OwnerContextStamp Stamp => stamp;
            public void Dispose() => Monitor.Exit(gate);
        }
    }
}
