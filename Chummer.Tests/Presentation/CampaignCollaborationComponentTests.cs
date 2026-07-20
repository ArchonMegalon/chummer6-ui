#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Chummer.Hub.Web;
using Chummer.Hub.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CampaignCollaborationComponentTests
{
    private const string CampaignId = "campaign-alpha";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public void Join_waits_for_existing_character_selection_then_posts_fragment_secret_and_scrubs()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        const string inviteSecret = "fragment-only-super-secret";
        SetupInviteInterop(
            context,
            new CampaignInviteFragmentHandoff
            {
                Status = CampaignInviteHandoffStatuses.Fragment,
                Secret = inviteSecret,
                MustScrub = true
            });
        FakeCampaignCollaborationClient client = new(CreateCampaign(CampaignViewerRoles.Player));
        client.BeforeJoin = () => Assert.IsFalse(
            context.JSInterop.Invocations.Any(invocation =>
                string.Equals(
                    invocation.Identifier,
                    "chummerCampaignJoin.scrubInviteLocation",
                    StringComparison.Ordinal)),
            "History must be scrubbed only after the redemption POST finishes.");
        RegisterCampaignClient(context, client);

        IRenderedComponent<CampaignWorkspace> cut = RenderInvite(context, "invite-alpha");
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Choose an existing character");
            Assert.IsFalse(cut.Markup.Contains(inviteSecret, StringComparison.Ordinal));
            Assert.AreEqual(0, client.JoinCallCount);
            Assert.IsFalse(context.JSInterop.Invocations.Any(invocation =>
                string.Equals(
                    invocation.Identifier,
                    "chummerCampaignJoin.scrubInviteLocation",
                    StringComparison.Ordinal)));
            Assert.IsFalse(cut.Find("input[data-field='grant-gm-edit-authority']").HasAttribute("checked"));
        });

        cut.Find("select[data-field='eligible-character']").Change("dossier-1");
        cut.Find("input[data-field='grant-gm-edit-authority']").Change(true);
        cut.Find("button[data-action='accept-campaign-invite']").Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Campaign invite accepted.");
            Assert.IsFalse(cut.Markup.Contains(inviteSecret, StringComparison.Ordinal));
            StringAssert.Contains(cut.Markup, "Vienna Shadows");
        });
        Assert.AreEqual(1, client.JoinCallCount);
        Assert.AreEqual("invite-alpha", client.LastInviteId);
        Assert.AreEqual(inviteSecret, client.LastJoinRequest?.Secret);
        Assert.AreEqual("dossier-1", client.LastJoinRequest?.DossierId);
        Assert.AreEqual("character-1", client.LastJoinRequest?.AuthoritativeCharacterId);
        Assert.AreEqual(7L, client.LastJoinRequest?.ExpectedCharacterRevision);
        Assert.IsTrue(client.LastJoinRequest?.GrantGmEditAuthority);
        StringAssert.StartsWith(client.LastJoinRequest?.IdempotencyKey, "join-");
        Assert.IsFalse(client.LastJoinRequest!.ToString().Contains(inviteSecret, StringComparison.Ordinal));
        CollectionAssert.AreEqual(
            new[]
            {
                "chummerCampaignJoin.readInviteFragment",
                "chummerCampaignJoin.scrubInviteLocation"
            },
            context.JSInterop.Invocations.Select(invocation => invocation.Identifier).ToArray());
        object? scrubPath = context.JSInterop.Invocations.Last().Arguments.Single();
        Assert.AreEqual("/account/campaigns/campaign-alpha", scrubPath);
    }

    [TestMethod]
    public void Join_rejects_query_secret_and_scrubs_without_posting_it()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        SetupInviteInterop(
            context,
            new CampaignInviteFragmentHandoff
            {
                Status = CampaignInviteHandoffStatuses.RejectedQuery,
                MustScrub = true
            });
        FakeCampaignCollaborationClient client = new(CreateCampaign(CampaignViewerRoles.Player));
        RegisterCampaignClient(context, client);

        IRenderedComponent<CampaignWorkspace> cut = RenderInvite(context, "invite-alpha");

        cut.WaitForAssertion(() =>
            StringAssert.Contains(cut.Markup, "secrets are not accepted in the query string"));
        Assert.AreEqual(0, client.JoinCallCount);
        Assert.IsTrue(context.JSInterop.Invocations.Any(invocation =>
            string.Equals(
                invocation.Identifier,
                "chummerCampaignJoin.scrubInviteLocation",
                StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Join_fails_closed_when_post_succeeds_but_history_scrub_fails()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        const string inviteSecret = "fragment-secret-never-rendered";
        context.JSInterop
            .Setup<CampaignInviteFragmentHandoff>("chummerCampaignJoin.readInviteFragment")
            .SetResult(new CampaignInviteFragmentHandoff
            {
                Status = CampaignInviteHandoffStatuses.Fragment,
                Secret = inviteSecret,
                MustScrub = true
            });
        FakeCampaignCollaborationClient client = new(CreateCampaign(CampaignViewerRoles.Player));
        RegisterCampaignClient(context, client);

        IRenderedComponent<CampaignWorkspace> cut = RenderInvite(context, "invite-alpha");

        cut.WaitForAssertion(() =>
            StringAssert.Contains(cut.Markup, "Choose an existing character"));
        cut.Find("select[data-field='eligible-character']").Change("dossier-1");
        cut.Find("button[data-action='accept-campaign-invite']").Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Browser history cleanup failed");
            Assert.IsFalse(cut.Markup.Contains("Vienna Shadows", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains(inviteSecret, StringComparison.Ordinal));
        });
        Assert.AreEqual(1, client.JoinCallCount);
    }

    [TestMethod]
    public void Join_clears_fragment_without_post_when_no_existing_character_is_eligible()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        SetupInviteInterop(
            context,
            new CampaignInviteFragmentHandoff
            {
                Status = CampaignInviteHandoffStatuses.Fragment,
                Secret = "one-time-secret",
                MustScrub = true
            });
        FakeCampaignCollaborationClient client = new(CreateCampaign(CampaignViewerRoles.Player))
        {
            EligibleCharacters = []
        };
        RegisterCampaignClient(context, client);

        IRenderedComponent<CampaignWorkspace> cut = RenderInvite(context, "invite-alpha");

        cut.WaitForAssertion(() =>
            StringAssert.Contains(cut.Markup, "No eligible existing character"));
        Assert.AreEqual(0, client.JoinCallCount);
        Assert.IsFalse(cut.Markup.Contains("one-time-secret", StringComparison.Ordinal));
        Assert.IsTrue(context.JSInterop.Invocations.Any(invocation =>
            string.Equals(
                invocation.Identifier,
                "chummerCampaignJoin.scrubInviteLocation",
                StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Player_view_shows_published_runsite_but_hides_draft_notes_and_edit_controls()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        SetupInviteInterop(context, new CampaignInviteFragmentHandoff());
        CampaignWorkspaceProjection playerCampaign = CreateCampaign(
            CampaignViewerRoles.Player,
            includeLeakedDraft: true);
        CampaignRosterMemberProjection ownedMember = playerCampaign.Roster.Single();
        PlayerSafeCharacterSheetProjection otherSheet = ownedMember.PlayerSafeSheet! with
        {
            DossierId = "dossier-2",
            RunnerHandle = "RazorGhost",
            DisplayName = "Razor Ghost",
            GmEditAuthorityGranted = false,
            GmAuthorityBindingRevision = 2,
            IsOwnedByViewer = false
        };
        playerCampaign = playerCampaign with
        {
            Roster =
            [
                ownedMember,
                ownedMember with
                {
                    MemberId = "dossier-2",
                    DisplayName = "Sam",
                    AuthoritativeCharacterId = "character-2",
                    GmEditAuthorityGranted = false,
                    GmAuthorityBindingRevision = 2,
                    IsOwnedByViewer = false,
                    PlayerSafeSheet = otherSheet
                }
            ]
        };
        FakeCampaignCollaborationClient client = new(playerCampaign);
        RegisterCampaignClient(context, client);

        IRenderedComponent<CampaignWorkspace> cut = RenderCampaign(context);

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Published player briefing");
            StringAssert.Contains(cut.Markup, "Player-visible rendezvous details.");
            StringAssert.Contains(cut.Markup, "NeonLynx");
            StringAssert.Contains(cut.Markup, "RazorGhost");
            StringAssert.Contains(cut.Markup, "Only the GM can edit campaign character sheets.");
            Assert.IsFalse(cut.Markup.Contains("GM DRAFT MUST STAY PRIVATE", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Secret opposition notes", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("data-testid=\"runsite-editor\"", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("data-action=\"edit-player-safe-sheet\"", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("data-action=\"save-player-safe-sheet\"", StringComparison.Ordinal));
        });
        Assert.AreEqual(0, client.UpdateSheetCallCount);
        Assert.AreEqual(0, client.SaveDraftCallCount);
        Assert.AreEqual(0, client.PublishCallCount);
    }

    [TestMethod]
    public void Character_owner_can_revoke_gm_edit_authority_with_binding_cas_and_idempotency()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        SetupInviteInterop(context, new CampaignInviteFragmentHandoff());
        FakeCampaignCollaborationClient client = new(CreateCampaign(CampaignViewerRoles.Player));
        RegisterCampaignClient(context, client);
        IRenderedComponent<CampaignWorkspace> cut = RenderCampaign(context);
        cut.WaitForAssertion(() =>
            Assert.AreEqual(1, cut.FindAll("button[data-action='change-gm-authority']").Count));

        cut.Find("button[data-action='change-gm-authority']").Click();
        cut.Find("input[data-field='gm-authority-reason']").Change("Owner revokes session access");
        cut.Find("button[data-action='save-gm-authority']").Click();

        cut.WaitForAssertion(() =>
            StringAssert.Contains(cut.Markup, "GM edit authority revoked"));
        Assert.AreEqual(1, client.UpdateAuthorityCallCount);
        Assert.AreEqual("dossier-1", client.LastDossierId);
        Assert.AreEqual(3L, client.LastAuthorityUpdate?.ExpectedBindingRevision);
        Assert.IsFalse(client.LastAuthorityUpdate!.GrantGmEditAuthority);
        Assert.AreEqual("Owner revokes session access", client.LastAuthorityUpdate.Reason);
        StringAssert.StartsWith(client.LastAuthorityUpdate.IdempotencyKey, "gm-authority-");
        Assert.AreEqual(0, client.UpdateSheetCallCount);
    }

    [TestMethod]
    public void Character_owner_can_regrant_gm_edit_authority_after_revocation()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        SetupInviteInterop(context, new CampaignInviteFragmentHandoff());
        CampaignWorkspaceProjection campaign = CreateCampaign(CampaignViewerRoles.Player);
        CampaignRosterMemberProjection member = campaign.Roster.Single();
        campaign = campaign with
        {
            Roster =
            [
                member with
                {
                    GmEditAuthorityGranted = false,
                    GmAuthorityBindingRevision = 4,
                    PlayerSafeSheet = member.PlayerSafeSheet! with
                    {
                        GmEditAuthorityGranted = false,
                        GmAuthorityBindingRevision = 4
                    }
                }
            ]
        };
        FakeCampaignCollaborationClient client = new(campaign)
        {
            UpdateAuthorityResult = new CampaignGmAuthorityReceipt(
                Applied: true,
                BindingRevision: 5,
                CurrentCharacterRevision: 7,
                GmEditAuthorityGranted: true,
                Changed: true)
        };
        RegisterCampaignClient(context, client);
        IRenderedComponent<CampaignWorkspace> cut = RenderCampaign(context);

        cut.WaitForAssertion(() =>
            StringAssert.Contains(
                cut.Find("button[data-action='change-gm-authority']").TextContent,
                "Grant GM editing"));
        cut.Find("button[data-action='change-gm-authority']").Click();
        cut.Find("input[data-field='gm-authority-reason']").Change("Owner restores session access");
        cut.Find("button[data-action='save-gm-authority']").Click();

        cut.WaitForAssertion(() =>
            StringAssert.Contains(cut.Markup, "GM edit authority granted"));
        Assert.AreEqual(4L, client.LastAuthorityUpdate?.ExpectedBindingRevision);
        Assert.IsTrue(client.LastAuthorityUpdate!.GrantGmEditAuthority);
        StringAssert.StartsWith(client.LastAuthorityUpdate.IdempotencyKey, "gm-authority-");
    }

    [TestMethod]
    public void Game_master_edit_sends_reason_and_expected_revision_and_handles_conflict()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        SetupInviteInterop(context, new CampaignInviteFragmentHandoff());
        FakeCampaignCollaborationClient client = new(CreateCampaign(CampaignViewerRoles.Owner))
        {
            UpdateSheetResult = new CampaignMutationReceipt(
                Applied: false,
                Revision: 12,
                Message: "Revision conflict. Reload the player-safe sheet.")
        };
        RegisterCampaignClient(context, client);
        IRenderedComponent<CampaignWorkspace> cut = RenderCampaign(context);
        cut.WaitForAssertion(() =>
            Assert.AreEqual(1, cut.FindAll("button[data-action='edit-player-safe-sheet']").Count));
        cut.Find("button[data-action='edit-player-safe-sheet']").Click();

        cut.Find("input[data-field='runner-handle']").Change("NeonFox");
        cut.Find("input[data-field='display-name']").Change("Neon Fox");
        cut.Find("select[data-field='character-status']").Change("active");
        cut.Find("input[data-field='character-reason']").Change("Session advancement");
        cut.Find("button[data-action='save-player-safe-sheet']").Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Revision conflict");
            StringAssert.Contains(cut.Markup, "Expected revision 12");
        });
        Assert.AreEqual(1, client.UpdateSheetCallCount);
        Assert.AreEqual("dossier-1", client.LastDossierId);
        Assert.AreEqual(7L, client.LastCharacterEdit?.ExpectedRevision);
        StringAssert.StartsWith(client.LastCharacterEdit!.IdempotencyKey, "sheet-");
        Assert.AreEqual("Session advancement", client.LastCharacterEdit?.Reason);
        Assert.AreEqual("NeonFox", client.LastCharacterEdit?.RunnerHandle);
        Assert.AreEqual("Neon Fox", client.LastCharacterEdit?.DisplayName);
        Assert.AreEqual(1, client.LastCharacterEdit?.Sections.Count);
    }

    [TestMethod]
    public void Game_master_edit_is_withheld_until_character_owner_grants_authority()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        SetupInviteInterop(context, new CampaignInviteFragmentHandoff());
        CampaignWorkspaceProjection campaign = CreateCampaign(CampaignViewerRoles.Owner);
        CampaignRosterMemberProjection member = campaign.Roster.Single();
        campaign = campaign with
        {
            Roster =
            [
                member with
                {
                    GmEditAuthorityGranted = false,
                    PlayerSafeSheet = member.PlayerSafeSheet! with
                    {
                        CanManage = false,
                        GmEditAuthorityGranted = false
                    }
                }
            ]
        };
        FakeCampaignCollaborationClient client = new(campaign);
        RegisterCampaignClient(context, client);

        IRenderedComponent<CampaignWorkspace> cut = RenderCampaign(context);

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Owner consent required");
            Assert.AreEqual(0, cut.FindAll("button[data-action='edit-player-safe-sheet']").Count);
        });
        Assert.AreEqual(0, client.UpdateSheetCallCount);
    }

    [TestMethod]
    public void Game_master_saves_draft_then_publishes_revision_bound_runsite()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        SetupInviteInterop(context, new CampaignInviteFragmentHandoff());
        FakeCampaignCollaborationClient client = new(CreateCampaign(CampaignViewerRoles.Owner));
        RegisterCampaignClient(context, client);
        IRenderedComponent<CampaignWorkspace> cut = RenderCampaign(context);
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "data-testid=\"runsite-editor\"");
            StringAssert.Contains(cut.Markup, "GM DRAFT MUST STAY PRIVATE");
        });

        cut.Find("input[data-field='runsite-title']").Change("Operation Glasshouse");
        cut.Find("textarea[data-field='runsite-summary']").Change("Meet at the west entrance.");
        cut.Find("input[data-field='runsite-section-heading']").Change("Rendezvous");
        cut.Find("textarea[data-field='runsite-section-body']").Change("Bring the blue credstick.");
        cut.Find("textarea[data-field='runsite-gm-notes']").Change("Secret opposition notes updated");
        cut.Find("button[data-action='save-runsite-draft']").Click();

        cut.WaitForAssertion(() =>
            StringAssert.Contains(cut.Markup, "Runsite draft saved"));
        Assert.AreEqual("run-1", client.LastDraftSave?.RunId);
        Assert.AreEqual(5L, client.LastDraftSave?.ExpectedRevision);
        Assert.AreEqual("Operation Glasshouse", client.LastDraftSave?.Title);
        Assert.AreEqual("Meet at the west entrance.", client.LastDraftSave?.Summary);
        Assert.AreEqual("Bring the blue credstick.", client.LastDraftSave?.PlayerSections.Single().Body);

        cut.Find("button[data-action='publish-runsite']").Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Runsite published.");
            StringAssert.Contains(cut.Markup, "Operation Glasshouse");
            StringAssert.Contains(cut.Markup, "Meet at the west entrance.");
            StringAssert.Contains(cut.Markup, "Bring the blue credstick.");
        });
        Assert.AreEqual("run-1", client.LastPublish?.RunId);
        Assert.AreEqual(6L, client.LastPublish?.ExpectedRevision);
    }

    [TestMethod]
    public async Task Browser_client_posts_join_secret_only_in_json_body_to_canonical_endpoint()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        const string inviteSecret = "body-only-invite-secret";
        string envelope = JsonSerializer.Serialize(
            new
            {
                status = 200,
                text = JsonSerializer.Serialize(
                    new
                    {
                        campaignId = "campaign alpha",
                        dossierId = "dossier-1",
                        crewId = "crew-1",
                        role = CampaignViewerRoles.Player,
                        binding = new
                        {
                            bindingRevision = 3,
                            currentRevision = 7,
                            gmAuthorityRole = "gm_character_editor"
                        },
                        alreadyJoined = false,
                        joinedAtUtc = "2026-07-20T08:00:00Z"
                    },
                    JsonOptions)
            },
            JsonOptions);
        context.JSInterop
            .Setup<string>(
                "chummerHubApi.send",
                invocation => invocation.Arguments.Count == 3
                    && string.Equals(invocation.Arguments[0]?.ToString(), "/api/v1/campaigns/invites/invite%20alpha/redeem", StringComparison.Ordinal)
                    && string.Equals(invocation.Arguments[1]?.ToString(), "POST", StringComparison.Ordinal))
            .SetResult(envelope);
        BrowserCampaignCollaborationClient client = new(context.JSInterop.JSRuntime);
        CampaignJoinRequest request = new(
            inviteSecret,
            "dossier-1",
            "character-1",
            7,
            grantGmEditAuthority: true,
            "join-browser-test");

        CampaignJoinReceipt receipt = await client.JoinCampaignAsync("invite alpha", request);

        Assert.IsTrue(receipt.Joined);
        var invocation = context.JSInterop.Invocations.Single();
        string path = invocation.Arguments[0]?.ToString() ?? string.Empty;
        string body = invocation.Arguments[2]?.ToString() ?? string.Empty;
        Assert.IsFalse(path.Contains(inviteSecret, StringComparison.Ordinal));
        Assert.IsFalse(path.Contains('?'));
        StringAssert.Contains(body, "\"secret\"");
        StringAssert.Contains(body, inviteSecret);
        StringAssert.Contains(body, "\"dossierId\":\"dossier-1\"");
        StringAssert.Contains(body, "\"authoritativeCharacterId\":\"character-1\"");
        StringAssert.Contains(body, "\"expectedCharacterRevision\":7");
        StringAssert.Contains(body, "\"grantGmEditAuthority\":true");
        StringAssert.Contains(body, "\"idempotencyKey\":\"join-browser-test\"");
        Assert.AreEqual(3L, receipt.BindingRevision);
        Assert.IsTrue(receipt.GmEditAuthorityGranted);
        Assert.IsFalse(request.ToString().Contains(inviteSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Browser_client_gets_authoritative_existing_character_choices_from_canonical_endpoint()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        string envelope = JsonSerializer.Serialize(
            new
            {
                status = 200,
                text = JsonSerializer.Serialize(
                    new[]
                    {
                        new
                        {
                            dossierId = "dossier-1",
                            authorityKind = "hub_character",
                            authoritativeCharacterId = "character-1",
                            runnerHandle = "NeonLynx",
                            displayName = "Neon Lynx",
                            status = "active",
                            currentRevision = 7,
                            updatedAtUtc = "2026-07-20T08:00:00Z"
                        }
                    },
                    JsonOptions)
            },
            JsonOptions);
        context.JSInterop
            .Setup<string>(
                "chummerHubApi.send",
                invocation => invocation.Arguments.Count == 2
                    && string.Equals(
                        invocation.Arguments[0]?.ToString(),
                        "/api/v1/campaigns/eligible-characters",
                        StringComparison.Ordinal)
                    && string.Equals(invocation.Arguments[1]?.ToString(), "GET", StringComparison.Ordinal))
            .SetResult(envelope);
        BrowserCampaignCollaborationClient client = new(context.JSInterop.JSRuntime);

        IReadOnlyList<CampaignEligibleCharacterProjection> characters =
            await client.GetEligibleCharactersAsync();

        Assert.AreEqual(1, characters.Count);
        Assert.AreEqual("dossier-1", characters[0].DossierId);
        Assert.AreEqual("character-1", characters[0].AuthoritativeCharacterId);
        Assert.AreEqual(7L, characters[0].CurrentRevision);
    }

    [TestMethod]
    public async Task Browser_client_uses_canonical_revision_bound_sheet_authority_and_runsite_routes()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Strict;
        SetupBrowserEnvelope(
            context,
            "/api/v1/campaigns/campaign%20alpha/sheets/dossier%201",
            "PUT",
            new { revision = 8 });
        SetupBrowserEnvelope(
            context,
            "/api/v1/campaigns/campaign%20alpha/sheets/dossier%201/gm-authority",
            "PUT",
            new
            {
                bindingRevision = 4,
                currentCharacterRevision = 8,
                gmEditAuthorityGranted = false,
                changed = true
            });
        SetupBrowserEnvelope(
            context,
            "/api/v1/campaigns/campaign%20alpha/runs/run%201/runsite/draft",
            "PUT",
            new
            {
                revision = 6,
                title = "Operation Glasshouse",
                summary = "Player summary",
                playerSections = new[] { new { heading = "Arrival", body = "Use the west door." } },
                gmNotes = "Private",
                updatedAtUtc = "2026-07-20T09:00:00Z"
            });
        SetupBrowserEnvelope(
            context,
            "/api/v1/campaigns/campaign%20alpha/runs/run%201/runsite/publish",
            "POST",
            new
            {
                revision = 6,
                title = "Operation Glasshouse",
                summary = "Player summary",
                sections = new[] { new { heading = "Arrival", body = "Use the west door." } },
                publishedAtUtc = "2026-07-20T09:05:00Z"
            });
        BrowserCampaignCollaborationClient client = new(context.JSInterop.JSRuntime);

        CampaignMutationReceipt sheetReceipt = await client.UpdatePlayerSafeSheetAsync(
            "campaign alpha",
            "dossier 1",
            new CampaignCharacterEditRequest(
                7,
                "sheet-browser-test",
                "NeonLynx",
                "Neon Lynx",
                "active",
                "Session advancement",
                [new CampaignPublicationSafeSectionProjection("projection-1", "summary", "Summary", "Safe text")]));
        CampaignGmAuthorityReceipt authorityReceipt = await client.UpdateGmEditAuthorityAsync(
            "campaign alpha",
            "dossier 1",
            new CampaignGmAuthorityUpdateRequest(
                3,
                GrantGmEditAuthority: false,
                "gm-authority-browser-test",
                "Owner revoked session access"));
        CampaignMutationReceipt draftReceipt = await client.SaveRunsiteDraftAsync(
            "campaign alpha",
            new RunsiteDraftSaveRequest(
                "run 1",
                5,
                "Operation Glasshouse",
                "Player summary",
                [new RunsitePlayerSectionProjection("Arrival", "Use the west door.")],
                "Private"));
        CampaignMutationReceipt publishReceipt = await client.PublishRunsiteAsync(
            "campaign alpha",
            new RunsitePublishRequest("run 1", 6));

        Assert.AreEqual(8L, sheetReceipt.Revision);
        Assert.AreEqual(4L, authorityReceipt.BindingRevision);
        Assert.IsFalse(authorityReceipt.GmEditAuthorityGranted);
        Assert.AreEqual(6L, draftReceipt.Revision);
        Assert.AreEqual(6L, publishReceipt.Revision);
        var invocations = context.JSInterop.Invocations.ToArray();
        Assert.AreEqual("PUT", invocations[0].Arguments[1]?.ToString());
        StringAssert.Contains(invocations[0].Arguments[2]?.ToString() ?? string.Empty, "Session advancement");
        StringAssert.Contains(invocations[0].Arguments[2]?.ToString() ?? string.Empty, "\"expectedRevision\":7");
        StringAssert.Contains(invocations[0].Arguments[2]?.ToString() ?? string.Empty, "\"idempotencyKey\":\"sheet-browser-test\"");
        Assert.AreEqual("PUT", invocations[1].Arguments[1]?.ToString());
        StringAssert.Contains(invocations[1].Arguments[2]?.ToString() ?? string.Empty, "\"expectedBindingRevision\":3");
        StringAssert.Contains(invocations[1].Arguments[2]?.ToString() ?? string.Empty, "\"idempotencyKey\":\"gm-authority-browser-test\"");
        Assert.AreEqual("PUT", invocations[2].Arguments[1]?.ToString());
        Assert.IsFalse((invocations[2].Arguments[2]?.ToString() ?? string.Empty).Contains("\"runId\"", StringComparison.Ordinal));
        Assert.AreEqual("POST", invocations[3].Arguments[1]?.ToString());
        Assert.AreEqual("{\"expectedRevision\":6}", invocations[3].Arguments[2]?.ToString());
    }

    [TestMethod]
    public void Browser_security_scripts_scrub_invites_and_gate_every_mutation_on_antiforgery_pair()
    {
        string repositoryRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string joinScript = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Chummer.Hub.Web",
            "wwwroot",
            "campaign-join.js"));
        string apiScript = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Chummer.Hub.Web",
            "wwwroot",
            "hub-api.js"));
        string appMarkup = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Chummer.Hub.Web",
            "Components",
            "App.razor"));

        StringAssert.Contains(joinScript, "window.location.hash");
        StringAssert.Contains(joinScript, "\"secret\"");
        StringAssert.Contains(joinScript, "rejected-query");
        StringAssert.Contains(joinScript, "window.history.replaceState");
        StringAssert.Contains(joinScript, "window.location.pathname");
        Assert.IsFalse(joinScript.Contains("fetch(", StringComparison.Ordinal));
        Assert.IsFalse(joinScript.Contains("console.", StringComparison.Ordinal));
        Assert.IsFalse(joinScript.Contains("localStorage", StringComparison.Ordinal));
        Assert.IsFalse(joinScript.Contains("sessionStorage", StringComparison.Ordinal));
        Assert.IsFalse(joinScript.Contains("document.cookie", StringComparison.Ordinal));
        StringAssert.Contains(apiScript, "referrerPolicy: \"no-referrer\"");
        StringAssert.Contains(apiScript, "new Set([\"POST\", \"PUT\", \"PATCH\", \"DELETE\"])");
        StringAssert.Contains(apiScript, "fetch(\"/api/v1/antiforgery\"");
        StringAssert.Contains(apiScript, "credentials: \"same-origin\"");
        StringAssert.Contains(apiScript, "tokenPayload.headerName");
        StringAssert.Contains(apiScript, "/^[A-Za-z][A-Za-z0-9-]{0,63}$/");
        StringAssert.Contains(apiScript, "options.headers[headerName] = requestToken");
        Assert.IsFalse(apiScript.Contains("options.headers[\"RequestVerificationToken\"]", StringComparison.Ordinal));
        Assert.IsTrue(
            apiScript.IndexOf("fetch(\"/api/v1/antiforgery\"", StringComparison.Ordinal)
            < apiScript.IndexOf("fetch(path", StringComparison.Ordinal),
            "The cookie/token handoff must finish before any unsafe target request is sent.");
        StringAssert.Contains(appMarkup, "name=\"referrer\" content=\"no-referrer\"");
    }

    private static void RegisterCampaignClient(
        BunitContext context,
        FakeCampaignCollaborationClient client)
        => context.Services.AddSingleton<ICampaignCollaborationClient>(client);

    private static void SetupBrowserEnvelope(
        BunitContext context,
        string path,
        string method,
        object payload)
    {
        string envelope = JsonSerializer.Serialize(
            new
            {
                status = 200,
                text = JsonSerializer.Serialize(payload, JsonOptions)
            },
            JsonOptions);
        context.JSInterop
            .Setup<string>(
                "chummerHubApi.send",
                invocation => invocation.Arguments.Count == 3
                    && string.Equals(invocation.Arguments[0]?.ToString(), path, StringComparison.Ordinal)
                    && string.Equals(invocation.Arguments[1]?.ToString(), method, StringComparison.Ordinal))
            .SetResult(envelope);
    }

    private static void SetupInviteInterop(
        BunitContext context,
        CampaignInviteFragmentHandoff handoff)
    {
        context.JSInterop
            .Setup<CampaignInviteFragmentHandoff>("chummerCampaignJoin.readInviteFragment")
            .SetResult(handoff);
        context.JSInterop
            .SetupVoid(
                "chummerCampaignJoin.scrubInviteLocation",
                invocation => invocation.Arguments.Count == 1)
            .SetVoidResult();
    }

    private static IRenderedComponent<CampaignWorkspace> RenderCampaign(BunitContext context)
        => context.Render<CampaignWorkspace>(parameters =>
            parameters.Add(component => component.CampaignId, CampaignId));

    private static IRenderedComponent<CampaignWorkspace> RenderInvite(
        BunitContext context,
        string inviteId)
        => context.Render<CampaignWorkspace>(parameters =>
            parameters.Add(component => component.InviteId, inviteId));

    private static CampaignWorkspaceProjection CreateCampaign(
        string viewerRole,
        bool includeLeakedDraft = true)
    {
        bool canManage = CampaignViewerRoles.IsGameMaster(viewerRole);
        return new CampaignWorkspaceProjection(
            CampaignId,
            "Vienna Shadows",
            "A player-safe campaign collaboration workspace.",
            viewerRole,
            canManage,
            ActiveRunId: "run-1",
            Roster:
            [
                new CampaignRosterMemberProjection(
                    MemberId: "dossier-1",
                    DisplayName: "Alex",
                    Role: CampaignViewerRoles.Player,
                    AuthorityKind: "hub_character",
                    AuthoritativeCharacterId: "character-1",
                    GmEditAuthorityGranted: true,
                    GmAuthorityBindingRevision: 3,
                    IsOwnedByViewer: string.Equals(viewerRole, CampaignViewerRoles.Player, StringComparison.Ordinal),
                    PlayerSafeSheet: new PlayerSafeCharacterSheetProjection(
                        DossierId: "dossier-1",
                        RunnerHandle: "NeonLynx",
                        DisplayName: "Neon Lynx",
                        Status: "active",
                        Role: CampaignViewerRoles.Player,
                        CanManage: canManage,
                        GmEditAuthorityGranted: true,
                        GmAuthorityBindingRevision: 3,
                        IsOwnedByViewer: string.Equals(viewerRole, CampaignViewerRoles.Player, StringComparison.Ordinal),
                        Revision: 7,
                        RuleEnvironmentFingerprint: "rules-sha256",
                        Sections:
                        [
                            new CampaignPublicationSafeSectionProjection(
                                "projection-1",
                                "summary",
                                "Shared summary",
                                "Player-safe contacts and public loadout.")
                        ]))
            ],
            Runsite: new CampaignRunsiteProjection(
                RunId: "run-1",
                Revision: 5,
                Published: new PublishedRunsiteProjection(
                    Title: "Published player briefing",
                    Summary: "Player-visible rendezvous details.",
                    Sections:
                    [
                        new RunsitePlayerSectionProjection("Arrival", "Use the north entrance.")
                    ],
                    Revision: 4,
                    PublishedAtUtc: new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero)),
                Draft: includeLeakedDraft
                    ? new RunsiteDraftProjection(
                        Title: "Unpublished ambush notes",
                        Summary: "GM DRAFT MUST STAY PRIVATE",
                        PlayerSections:
                        [
                            new RunsitePlayerSectionProjection("Draft approach", "Unpublished player text")
                        ],
                        GmNotes: "Secret opposition notes",
                        Revision: 5,
                        UpdatedAtUtc: new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero))
                    : null));
    }

    private sealed class FakeCampaignCollaborationClient : ICampaignCollaborationClient
    {
        public FakeCampaignCollaborationClient(CampaignWorkspaceProjection campaign)
        {
            Campaign = campaign;
        }

        public CampaignWorkspaceProjection Campaign { get; private set; }

        public IReadOnlyList<CampaignEligibleCharacterProjection> EligibleCharacters { get; set; } =
        [
            new CampaignEligibleCharacterProjection(
                "dossier-1",
                "hub_character",
                "character-1",
                "NeonLynx",
                "Neon Lynx",
                "active",
                7,
                new DateTimeOffset(2026, 7, 20, 7, 0, 0, TimeSpan.Zero))
        ];

        public Action? BeforeJoin { get; set; }

        public CampaignMutationReceipt UpdateSheetResult { get; set; }
            = new(Applied: true, Revision: 8);

        public CampaignGmAuthorityReceipt UpdateAuthorityResult { get; set; }
            = new(
                Applied: true,
                BindingRevision: 4,
                CurrentCharacterRevision: 7,
                GmEditAuthorityGranted: false,
                Changed: true);

        public int JoinCallCount { get; private set; }

        public int UpdateSheetCallCount { get; private set; }

        public int UpdateAuthorityCallCount { get; private set; }

        public int SaveDraftCallCount { get; private set; }

        public int PublishCallCount { get; private set; }

        public string? LastInviteId { get; private set; }

        public CampaignJoinRequest? LastJoinRequest { get; private set; }

        public string? LastDossierId { get; private set; }

        public CampaignCharacterEditRequest? LastCharacterEdit { get; private set; }

        public CampaignGmAuthorityUpdateRequest? LastAuthorityUpdate { get; private set; }

        public RunsiteDraftSaveRequest? LastDraftSave { get; private set; }

        public RunsitePublishRequest? LastPublish { get; private set; }

        public Task<IReadOnlyList<CampaignEligibleCharacterProjection>> GetEligibleCharactersAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(EligibleCharacters);

        public Task<CampaignWorkspaceProjection> GetCampaignAsync(
            string campaignId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Campaign);

        public Task<CampaignJoinReceipt> JoinCampaignAsync(
            string inviteId,
            CampaignJoinRequest request,
            CancellationToken cancellationToken = default)
        {
            BeforeJoin?.Invoke();
            JoinCallCount++;
            LastInviteId = inviteId;
            LastJoinRequest = request;
            return Task.FromResult(new CampaignJoinReceipt(
                Joined: true,
                CampaignId: Campaign.CampaignId,
                DossierId: request.DossierId,
                ViewerRole: Campaign.ViewerRole,
                AlreadyJoined: false,
                BindingRevision: 3,
                CurrentCharacterRevision: request.ExpectedCharacterRevision,
                GmEditAuthorityGranted: request.GrantGmEditAuthority));
        }

        public Task<CampaignMutationReceipt> UpdatePlayerSafeSheetAsync(
            string campaignId,
            string dossierId,
            CampaignCharacterEditRequest request,
            CancellationToken cancellationToken = default)
        {
            UpdateSheetCallCount++;
            LastDossierId = dossierId;
            LastCharacterEdit = request;
            return Task.FromResult(UpdateSheetResult);
        }

        public Task<CampaignGmAuthorityReceipt> UpdateGmEditAuthorityAsync(
            string campaignId,
            string dossierId,
            CampaignGmAuthorityUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            UpdateAuthorityCallCount++;
            LastDossierId = dossierId;
            LastAuthorityUpdate = request;
            return Task.FromResult(UpdateAuthorityResult);
        }

        public Task<CampaignMutationReceipt> SaveRunsiteDraftAsync(
            string campaignId,
            RunsiteDraftSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            SaveDraftCallCount++;
            LastDraftSave = request;
            long revision = request.ExpectedRevision + 1;
            Campaign = Campaign with
            {
                Runsite = Campaign.Runsite with
                {
                    Revision = revision,
                    Draft = new RunsiteDraftProjection(
                        request.Title,
                        request.Summary,
                        request.PlayerSections,
                        request.GmNotes,
                        revision,
                        new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero))
                }
            };
            return Task.FromResult(new CampaignMutationReceipt(true, revision));
        }

        public Task<CampaignMutationReceipt> PublishRunsiteAsync(
            string campaignId,
            RunsitePublishRequest request,
            CancellationToken cancellationToken = default)
        {
            PublishCallCount++;
            LastPublish = request;
            RunsiteDraftProjection draft = Campaign.Runsite.Draft
                ?? throw new InvalidOperationException("A draft is required for this test.");
            Campaign = Campaign with
            {
                Runsite = Campaign.Runsite with
                {
                    Revision = request.ExpectedRevision,
                    Published = new PublishedRunsiteProjection(
                        draft.Title,
                        draft.Summary,
                        draft.PlayerSections,
                        request.ExpectedRevision,
                        new DateTimeOffset(2026, 7, 20, 10, 5, 0, TimeSpan.Zero))
                }
            };
            return Task.FromResult(new CampaignMutationReceipt(true, request.ExpectedRevision));
        }
    }
}
