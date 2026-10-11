using System;
using System.Linq;
using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CharacterCreationLifestylesOwnerTests
{
    private static readonly OwnerContextStamp Original = new(new OwnerScope("lifestyle-account-a"), "authority", 10);

    [TestMethod]
    public void Prepare_and_confirm_reject_missing_switched_and_ABA_display_before_service_access()
    {
        var service = new NoAccessService();
        var presenter = new CharacterCreationLifestylesInteractionPresenter(service, service);
        OwnerContextStamp?[] staleDisplays = [null, default(OwnerContextStamp),
            new(new OwnerScope("lifestyle-account-b"), "authority", 11),
            new(Original.Owner, Original.AuthorityInstanceId, 12),
            new(Original.Owner, "different-authority", Original.TransitionRevision)];
        var input = new CharacterCreationLifestyleMutationInput(CharacterCreationLifestyleMutationKinds.Create,
            Guid.NewGuid(), null) { DisplayOwnerContext = Original };
        var prepared = Preview() with { DisplayOwnerContext = Original };
        foreach (var display in staleDisplays)
        {
            var overview = CharacterOverviewState.Empty with { DisplayOwnerContext = display };
            var prepare = presenter.Prepare(overview, input);
            Assert.AreEqual(CharacterCreationLifestyleOutcomes.Conflict, prepare.Outcome);
            Assert.IsNull(prepare.PreparedPreview);
            var confirm = presenter.Confirm(overview, new(prepared, prepared.PreviewDigest,
                prepared.IdempotencyKey, ExplicitlyConfirmed: true));
            Assert.AreEqual(CharacterCreationLifestyleOutcomes.Conflict, confirm.Outcome);
            Assert.IsNull(confirm.Receipt);
            Assert.IsNull(confirm.RefreshedState);
            CollectionAssert.Contains(confirm.Blockers.ToArray(),
                CharacterCreationLifestylesInteractionBlockers.OverviewAuthorityRequired);
        }
    }

    [TestMethod]
    public void Owner_bound_presenter_does_not_admit_ownerless_or_deserialized_intent()
    {
        var service = new NoAccessService();
        var presenter = new CharacterCreationLifestylesInteractionPresenter(service, service);
        var input = new CharacterCreationLifestyleMutationInput(CharacterCreationLifestyleMutationKinds.Create,
            Guid.NewGuid(), null) { DisplayOwnerContext = Original };
        string json = JsonSerializer.Serialize(input);
        Assert.IsFalse(json.Contains("DisplayOwnerContext", StringComparison.Ordinal));
        var restored = JsonSerializer.Deserialize<CharacterCreationLifestyleMutationInput>(json)!;
        Assert.IsNull(restored.DisplayOwnerContext);
        foreach (var overview in new[] { CharacterOverviewState.Empty,
            CharacterOverviewState.Empty with { DisplayOwnerContext = Original } })
        {
            var result = presenter.Prepare(overview, restored);
            Assert.AreEqual(CharacterCreationLifestyleOutcomes.Conflict, result.Outcome);
            Assert.IsNull(result.PreparedPreview);
        }
        var prepared = Preview() with { DisplayOwnerContext = Original };
        string previewJson = JsonSerializer.Serialize(prepared);
        Assert.IsFalse(previewJson.Contains("DisplayOwnerContext", StringComparison.Ordinal));
        Assert.IsNull(JsonSerializer.Deserialize<CharacterCreationLifestylePreparedPreview>(previewJson)!.DisplayOwnerContext);
    }

    // Deliberately incomplete payload: owner rejection must precede all domain
    // reads and validation. Successful real plans are exercised by the native
    // saved-Lifestyle -> starting-cash -> finalization route and Core store tests.
    private static CharacterCreationLifestylePreparedPreview Preview()
        => new("", null!, null!, [], null, null, null!, null!, null!, [], true, true, "owner-test", "");

    private sealed class NoAccessService : ICharacterCreationLifestylesService, IOwnerBoundCharacterCreationLifestylesService
    {
        private static CharacterCreationLifestyleResult<T> Unexpected<T>() where T : class
            => throw new AssertFailedException("An unadmitted Lifestyle intent reached a Core service.");
        public CharacterCreationLifestyleResult<CharacterCreationLifestylesState> Load(CharacterCreationLifestylesLoadRequest request)
            => Unexpected<CharacterCreationLifestylesState>();
        public CharacterCreationLifestyleResult<CharacterCreationLifestylePreview> Preview(CharacterCreationLifestylePreviewRequest request)
            => Unexpected<CharacterCreationLifestylePreview>();
        public CharacterCreationLifestyleResult<CharacterCreationLifestyleReceipt> Confirm(CharacterCreationLifestyleConfirmRequest request)
            => Unexpected<CharacterCreationLifestyleReceipt>();
        public CharacterCreationLifestyleResult<CharacterCreationLifestyleReceipt> LookupReceipt(CharacterCreationLifestyleReceiptLookupRequest request)
            => Unexpected<CharacterCreationLifestyleReceipt>();
        public CharacterCreationLifestyleResult<CharacterCreationLifestylesState> Load(OwnerContextStamp owner, CharacterCreationLifestylesLoadRequest request)
            => Unexpected<CharacterCreationLifestylesState>();
        public CharacterCreationLifestyleResult<CharacterCreationLifestylePreview> Preview(OwnerContextStamp owner, CharacterCreationLifestylePreviewRequest request)
            => Unexpected<CharacterCreationLifestylePreview>();
        public CharacterCreationLifestyleResult<CharacterCreationLifestyleReceipt> Confirm(OwnerContextStamp owner, CharacterCreationLifestyleConfirmRequest request)
            => Unexpected<CharacterCreationLifestyleReceipt>();
        public CharacterCreationLifestyleResult<CharacterCreationLifestyleReceipt> LookupReceipt(OwnerContextStamp owner, CharacterCreationLifestyleReceiptLookupRequest request)
            => Unexpected<CharacterCreationLifestyleReceipt>();
    }
}
