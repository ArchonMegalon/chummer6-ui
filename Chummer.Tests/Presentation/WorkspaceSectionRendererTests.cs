#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class WorkspaceSectionRendererTests
{
    [TestMethod]
    public async Task RenderSectionAsync_projects_json_rows_and_selection()
    {
        WorkspaceSectionRenderer renderer = new();
        SectionRendererClientStub client = new();

        WorkspaceSectionRenderResult result = await renderer.RenderSectionAsync(
            client,
            new CharacterWorkspaceId("ws-section"),
            sectionId: "profile",
            tabId: "tab-info",
            actionId: "tab-info.profile",
            currentTabId: null,
            currentActionId: null,
            ct: CancellationToken.None);

        Assert.AreEqual("tab-info", result.ActiveTabId);
        Assert.AreEqual("tab-info.profile", result.ActiveActionId);
        Assert.AreEqual("profile", result.ActiveSectionId);
        StringAssert.Contains(result.ActiveSectionJson, "\"sectionId\": \"profile\"");
        Assert.IsGreaterThan(0, result.ActiveSectionRows.Count);
        Assert.IsNull(result.ActiveBuildLab);
        Assert.IsNull(result.ActiveBrowseWorkspace);
        Assert.IsNull(result.ActiveNpcPersonaStudio);
    }

    [TestMethod]
    public async Task RenderSummaryAsync_projects_summary_payload()
    {
        WorkspaceSectionRenderer renderer = new();
        SectionRendererClientStub client = new();
        WorkspaceSurfaceActionDefinition action = new(
            Id: "tab-info.summary",
            Label: "Summary",
            TabId: "tab-info",
            Kind: WorkspaceSurfaceActionKind.Summary,
            TargetId: "summary",
            RequiresOpenCharacter: true,
            EnabledByDefault: true,
            RulesetId: RulesetDefaults.Sr5);

        WorkspaceSectionRenderResult result = await renderer.RenderSummaryAsync(
            client,
            new CharacterWorkspaceId("ws-section"),
            action,
            CancellationToken.None);

        Assert.AreEqual("tab-info", result.ActiveTabId);
        Assert.AreEqual("tab-info.summary", result.ActiveActionId);
        Assert.AreEqual("summary", result.ActiveSectionId);
        StringAssert.Contains(result.ActiveSectionJson, "\"Name\": \"Summary Neo\"");
        Assert.IsGreaterThan(0, result.ActiveSectionRows.Count);
        Assert.IsNull(result.ActiveBuildLab);
        Assert.IsNull(result.ActiveBrowseWorkspace);
        Assert.IsNull(result.ActiveNpcPersonaStudio);
    }

    [TestMethod]
    public async Task RenderValidationAsync_projects_validation_payload()
    {
        WorkspaceSectionRenderer renderer = new();
        SectionRendererClientStub client = new();
        WorkspaceSurfaceActionDefinition action = new(
            Id: "tab-info.validate",
            Label: "Validate",
            TabId: "tab-info",
            Kind: WorkspaceSurfaceActionKind.Validate,
            TargetId: "validate",
            RequiresOpenCharacter: true,
            EnabledByDefault: true,
            RulesetId: RulesetDefaults.Sr5);

        WorkspaceSectionRenderResult result = await renderer.RenderValidationAsync(
            client,
            new CharacterWorkspaceId("ws-section"),
            action,
            CancellationToken.None);

        Assert.AreEqual("tab-info", result.ActiveTabId);
        Assert.AreEqual("tab-info.validate", result.ActiveActionId);
        Assert.AreEqual("validate", result.ActiveSectionId);
        StringAssert.Contains(result.ActiveSectionJson, "\"IsValid\": true");
        Assert.IsGreaterThan(0, result.ActiveSectionRows.Count);
        Assert.IsNull(result.ActiveBuildLab);
        Assert.IsNull(result.ActiveBrowseWorkspace);
        Assert.IsNull(result.ActiveNpcPersonaStudio);
    }

    [TestMethod]
    public async Task RenderSectionAsync_projects_build_lab_state_from_contract_payload()
    {
        WorkspaceSectionRenderer renderer = new();
        BuildLabSectionRendererClientStub client = new();

        WorkspaceSectionRenderResult result = await renderer.RenderSectionAsync(
            client,
            new CharacterWorkspaceId("ws-build-lab"),
            sectionId: "build-lab",
            tabId: "tab-create",
            actionId: "tab-create.intake",
            currentTabId: null,
            currentActionId: null,
            ct: CancellationToken.None);

        Assert.IsNotNull(result.ActiveBuildLab);
        Assert.AreEqual("lab-intake", result.ActiveBuildLab.WorkspaceId);
        Assert.AreEqual("Street Face", result.ActiveBuildLab.IntakeFields[0].Value);
        Assert.AreEqual("ops-first", result.ActiveBuildLab.ProvenanceBadges[0].BadgeId);
        Assert.AreEqual("next-variants", result.ActiveBuildLab.Actions[0].ActionId);
        Assert.AreEqual("variant.social", result.ActiveBuildLab.Variants[0].VariantId);
        Assert.AreEqual(100, result.ActiveBuildLab.ProgressionTimelines[0].Steps[^1].KarmaTarget);
        Assert.AreEqual("payload.social", result.ActiveBuildLab.ExportPayloads[0].PayloadId);
        Assert.AreEqual("target.build-idea-card", result.ActiveBuildLab.ExportTargets[0].TargetId);
        Assert.IsNull(result.ActiveBrowseWorkspace);
        Assert.IsNull(result.ActiveNpcPersonaStudio);
    }

    [TestMethod]
    public async Task RenderSectionAsync_projects_browse_workspace_state_from_contract_payload()
    {
        WorkspaceSectionRenderer renderer = new();
        BrowseSectionRendererClientStub client = new();

        WorkspaceSectionRenderResult result = await renderer.RenderSectionAsync(
            client,
            new CharacterWorkspaceId("ws-browse"),
            sectionId: "browse",
            tabId: "tab-browse",
            actionId: "tab-browse.catalog",
            currentTabId: null,
            currentActionId: null,
            ct: CancellationToken.None);

        Assert.IsNotNull(result.ActiveBrowseWorkspace);
        Assert.AreEqual("browse-gear", result.ActiveBrowseWorkspace.WorkspaceId);
        Assert.AreEqual("official", result.ActiveBrowseWorkspace.SourceFacets[0].SelectedOptions[0].Value);
        Assert.AreEqual("street", result.ActiveBrowseWorkspace.PackFacets[0].SelectedOptions[0].Value);
        Assert.AreEqual("preset.street", result.ActiveBrowseWorkspace.Presets.Single(preset => preset.IsActive).PresetId);
        Assert.AreEqual(0, result.ActiveBrowseWorkspace.QueryOffset);
        Assert.AreEqual(50, result.ActiveBrowseWorkspace.QueryLimit);
        Assert.IsNull(result.ActiveNpcPersonaStudio);
    }

    [TestMethod]
    public async Task RenderSectionAsync_projects_npc_persona_studio_state_from_contract_payload()
    {
        WorkspaceSectionRenderer renderer = new();
        NpcPersonaSectionRendererClientStub client = new();

        WorkspaceSectionRenderResult result = await renderer.RenderSectionAsync(
            client,
            new CharacterWorkspaceId("ws-persona"),
            sectionId: "persona-studio",
            tabId: "tab-npc",
            actionId: "tab-npc.persona",
            currentTabId: null,
            currentActionId: null,
            ct: CancellationToken.None);

        Assert.IsNotNull(result.ActiveNpcPersonaStudio);
        Assert.AreEqual("decker-contact", result.ActiveNpcPersonaStudio.DefaultPersonaId);
        Assert.AreEqual("decker-contact", result.ActiveNpcPersonaStudio.SelectedPersonaId);
        Assert.AreEqual("decker-contact evidence-first", result.ActiveNpcPersonaStudio.PromptPolicy);
        Assert.AreEqual(2, result.ActiveNpcPersonaStudio.Personas.Count);
        Assert.AreEqual("coach", result.ActiveNpcPersonaStudio.Policies[0].RouteType);
        Assert.IsTrue(result.ActiveNpcPersonaStudio.HasDraftPolicies);
        Assert.IsTrue(result.ActiveNpcPersonaStudio.HasApprovedPolicies);
        Assert.IsTrue(result.ActiveNpcPersonaStudio.EvidenceLines.Any(line => line.Contains("Prompt policy", StringComparison.Ordinal)));
        Assert.IsNull(result.ActiveBuildLab);
        Assert.IsNull(result.ActiveBrowseWorkspace);
    }

    private class SectionRendererClientStub : IChummerClient
    {
        public Task<ShellPreferences> GetShellPreferencesAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task SaveShellPreferencesAsync(ShellPreferences preferences, CancellationToken ct) => throw new NotImplementedException();

        public Task<ShellSessionState> GetShellSessionAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task SaveShellSessionAsync(ShellSessionState session, CancellationToken ct) => throw new NotImplementedException();

        public Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<RuntimeInspectorProjection?> GetRuntimeInspectorProfileAsync(string profileId, string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<DesktopBuildPathSuggestion>> GetBuildPathSuggestionsAsync(string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<DesktopBuildPathPreview?> GetBuildPathPreviewAsync(string buildKitId, CharacterWorkspaceId workspaceId, string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<AppCommandDefinition>> GetCommandsAsync(string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<NavigationTabDefinition>> GetNavigationTabsAsync(string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task<AccountCampaignSummary?> GetAccountCampaignSummaryAsync(CancellationToken ct)
            => Task.FromResult<AccountCampaignSummary?>(null);

        public Task<IReadOnlyList<CampaignWorkspaceDigestProjection>> GetCampaignWorkspaceDigestsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CampaignWorkspaceDigestProjection>>(Array.Empty<CampaignWorkspaceDigestProjection>());

        public Task<IReadOnlyList<DesktopHomeSupportDigest>> GetDesktopHomeSupportDigestsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DesktopHomeSupportDigest>>([]);

        public Task<WorkspaceImportResult> ImportAsync(WorkspaceImportDocument document, CancellationToken ct) => throw new NotImplementedException();

        public Task<bool> CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public virtual Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct)
        {
            JsonObject section = new()
            {
                ["workspaceId"] = id.Value,
                ["sectionId"] = sectionId
            };
            return Task.FromResult<JsonNode>(section);
        }

        public Task<CharacterFileSummary> GetSummaryAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            return Task.FromResult(new CharacterFileSummary(
                Name: "Summary Neo",
                Alias: "SUM",
                Metatype: "Human",
                BuildMethod: "Priority",
                CreatedVersion: "1.0",
                AppVersion: "1.0",
                Karma: 7m,
                Nuyen: 1000m,
                Created: true));
        }

        public Task<CharacterValidationResult> ValidateAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            return Task.FromResult(new CharacterValidationResult(
                IsValid: true,
                Issues: []));
        }

        public Task<CharacterProfileSection> GetProfileAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterProgressSection> GetProgressAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterSkillsSection> GetSkillsAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterRulesSection> GetRulesAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterBuildSection> GetBuildAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterMovementSection> GetMovementAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterAwakeningSection> GetAwakeningAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<CharacterProfileSection>> UpdateMetadataAsync(CharacterWorkspaceId id, UpdateWorkspaceMetadata command, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class BrowseSectionRendererClientStub : SectionRendererClientStub
    {
        public override Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct)
        {
            JsonObject payload = new()
            {
                ["WorkspaceId"] = "browse-gear",
                ["WorkflowId"] = "workflow.browse",
                ["Results"] = new JsonObject
                {
                    ["Query"] = new JsonObject
                    {
                        ["QueryText"] = "armor",
                        ["FacetSelections"] = new JsonObject
                        {
                            ["source"] = new JsonArray("official"),
                            ["pack"] = new JsonArray("street")
                        },
                        ["SortId"] = "name",
                        ["SortDirection"] = "asc",
                        ["Offset"] = 0,
                        ["Limit"] = 50
                    },
                    ["Items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["ItemId"] = "armor-jacket",
                            ["Title"] = "Armor Jacket",
                            ["ColumnValues"] = new JsonObject { ["Availability"] = "8R" },
                            ["FacetValues"] = new JsonArray("source:official", "pack:street"),
                            ["IsSelectable"] = true
                        }
                    },
                    ["Columns"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["ColumnId"] = "availability",
                            ["Label"] = "Availability",
                            ["ValueKind"] = "availability"
                        }
                    },
                    ["Facets"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["FacetId"] = "source",
                            ["Label"] = "Source",
                            ["Kind"] = "multi-select",
                            ["MultiSelect"] = true,
                            ["Options"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["Value"] = "official",
                                    ["Label"] = "Official",
                                    ["Count"] = 1,
                                    ["Selected"] = true
                                }
                            }
                        },
                        new JsonObject
                        {
                            ["FacetId"] = "pack",
                            ["Label"] = "Pack",
                            ["Kind"] = "multi-select",
                            ["MultiSelect"] = true,
                            ["Options"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["Value"] = "street",
                                    ["Label"] = "Street",
                                    ["Count"] = 1,
                                    ["Selected"] = true
                                }
                            }
                        }
                    },
                    ["Sorts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["SortId"] = "name",
                            ["Label"] = "Name",
                            ["Direction"] = "asc",
                            ["IsDefault"] = true
                        }
                    },
                    ["ViewPresets"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["PresetId"] = "preset.street",
                            ["Label"] = "Street",
                            ["Shared"] = false,
                            ["Query"] = new JsonObject
                            {
                                ["QueryText"] = "armor",
                                ["FacetSelections"] = new JsonObject
                                {
                                    ["source"] = new JsonArray("official"),
                                    ["pack"] = new JsonArray("street")
                                },
                                ["SortId"] = "name",
                                ["SortDirection"] = "asc",
                                ["Offset"] = 0,
                                ["Limit"] = 50
                            }
                        }
                    },
                    ["DisableReasons"] = new JsonArray(),
                    ["TotalCount"] = 1
                },
                ["Sections"] = new JsonArray(),
                ["SelectedItems"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["ItemId"] = "armor-jacket",
                        ["Title"] = "Armor Jacket",
                        ["Detail"] = "8R"
                    }
                },
                ["ActiveDetail"] = new JsonObject
                {
                    ["ItemId"] = "armor-jacket",
                    ["Title"] = "Armor Jacket",
                    ["SummaryLines"] = new JsonArray("Armored clothing"),
                    ["ExplainEntryId"] = "explain.armor_jacket"
                }
            };

            return Task.FromResult<JsonNode>(payload);
        }
    }

    private sealed class NpcPersonaSectionRendererClientStub : SectionRendererClientStub
    {
        public override Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct)
        {
            JsonObject payload = new()
            {
                ["Status"] = "scaffolded",
                ["PromptPolicy"] = "decker-contact evidence-first",
                ["DefaultPersonaId"] = "decker-contact",
                ["Personas"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["PersonaId"] = "decker-contact",
                        ["DisplayName"] = "Decker Contact",
                        ["EvidenceFirst"] = true,
                        ["Summary"] = "Grounded NPC helper.",
                        ["Provenance"] = "persona.registry/decker-contact",
                        ["ApprovalState"] = "approved"
                    },
                    new JsonObject
                    {
                        ["PersonaId"] = "street-fixer",
                        ["DisplayName"] = "Street Fixer",
                        ["EvidenceFirst"] = true,
                        ["Summary"] = "Fallback downtime persona.",
                        ["Provenance"] = "persona.registry/street-fixer",
                        ["ApprovalState"] = "draft"
                    }
                },
                ["RoutePolicies"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["RouteType"] = "coach",
                        ["RouteClassId"] = "grounded_rules_chat",
                        ["PersonaId"] = "decker-contact",
                        ["PrimaryProviderId"] = "aimagicx",
                        ["ToolingEnabled"] = true,
                        ["ApprovalState"] = "approved",
                        ["AllowedTools"] = new JsonArray
                        {
                            new JsonObject { ["ToolId"] = "create_apply_preview" }
                        }
                    },
                    new JsonObject
                    {
                        ["RouteType"] = "chat",
                        ["RouteClassId"] = "cross_repo_contract",
                        ["PersonaId"] = "street-fixer",
                        ["PrimaryProviderId"] = "oneminai",
                        ["ToolingEnabled"] = false,
                        ["ApprovalState"] = "draft",
                        ["AllowedTools"] = new JsonArray()
                    }
                }
            };

            return Task.FromResult<JsonNode>(payload);
        }
    }

    private sealed class BuildLabSectionRendererClientStub : SectionRendererClientStub
    {
        public override Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct)
        {
            JsonObject payload = new()
            {
                ["WorkspaceId"] = "lab-intake",
                ["WorkflowId"] = "workflow.build-lab",
                ["Title"] = "Build Lab Intake",
                ["Summary"] = "Capture concept, table constraints, and role intent before variant generation.",
                ["RulesetId"] = RulesetDefaults.Sr5,
                ["BuildMethod"] = "Priority",
                ["CanContinue"] = true,
                ["IntakeFields"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["FieldId"] = "concept",
                        ["Label"] = "Concept",
                        ["Kind"] = BuildLabFieldKinds.Text,
                        ["Value"] = "Street Face",
                        ["Required"] = true
                    },
                    new JsonObject
                    {
                        ["FieldId"] = "notes",
                        ["Label"] = "Table Constraints",
                        ["Kind"] = BuildLabFieldKinds.Multiline,
                        ["Value"] = "Keep matrix load light."
                    }
                },
                ["RoleBadges"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["BadgeId"] = "face",
                        ["Label"] = "Face",
                        ["Kind"] = BuildLabBadgeKinds.Role,
                        ["Emphasized"] = true
                    }
                },
                ["ConstraintBadges"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["BadgeId"] = "low-magic",
                        ["Label"] = "Low Magic",
                        ["Kind"] = BuildLabBadgeKinds.Constraint
                    }
                },
                ["ProvenanceBadges"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["BadgeId"] = "ops-first",
                        ["Label"] = "Ops-first table profile",
                        ["Kind"] = BuildLabBadgeKinds.Provenance,
                        ["Emphasized"] = true
                    }
                },
                ["Variants"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["VariantId"] = "variant.social",
                        ["Label"] = "Social Operator",
                        ["Summary"] = "Fastest route to the table.",
                        ["TableFit"] = "Best for ops-first tables",
                        ["RoleBadges"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["BadgeId"] = "face",
                                ["Label"] = "Face",
                                ["Kind"] = BuildLabBadgeKinds.Role,
                                ["Emphasized"] = true
                            }
                        },
                        ["Metrics"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["MetricId"] = "bookkeeping",
                                ["Label"] = "Bookkeeping",
                                ["Value"] = "Low"
                            }
                        },
                        ["Warnings"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["WarningId"] = "astral-gap",
                                ["Label"] = "Astral gap",
                                ["Detail"] = "Needs astral backup.",
                                ["Kind"] = BuildLabWarningKinds.Trap,
                                ["Emphasized"] = true
                            }
                        },
                        ["OverlapBadges"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["BadgeId"] = "face-overlap",
                                ["Label"] = "Light face overlap",
                                ["Kind"] = BuildLabBadgeKinds.Overlap
                            }
                        },
                        ["Actions"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["ActionId"] = "inspect-social",
                                ["Label"] = "Inspect Timeline",
                                ["SurfaceId"] = BuildLabSurfaceIds.ProgressionTimelineRail,
                                ["Enabled"] = true
                            }
                        },
                        ["ExplainEntryId"] = "buildlab.variant.social"
                    }
                },
                ["ProgressionTimelines"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["TimelineId"] = "timeline.social",
                        ["Title"] = "Social Operator Ladder",
                        ["Summary"] = "25 / 50 / 100 Karma checkpoints.",
                        ["VariantId"] = "variant.social",
                        ["SourceDocumentId"] = "source.timeline",
                        ["Steps"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["StepId"] = "social-25",
                                ["KarmaTarget"] = 25,
                                ["Label"] = "Opener",
                                ["Summary"] = "Table-ready lead.",
                                ["Outcomes"] = new JsonArray
                                {
                                    new JsonObject
                                    {
                                        ["MetricId"] = "prep",
                                        ["Label"] = "Prep speed",
                                        ["Value"] = "Fast"
                                    }
                                },
                                ["MilestoneBadges"] = new JsonArray
                                {
                                    new JsonObject
                                    {
                                        ["BadgeId"] = "25",
                                        ["Label"] = "25 Karma",
                                        ["Kind"] = BuildLabBadgeKinds.Milestone,
                                        ["Emphasized"] = true
                                    }
                                },
                                ["RiskBadges"] = new JsonArray(),
                                ["ExplainEntryId"] = "buildlab.timeline.social-25"
                            },
                            new JsonObject
                            {
                                ["StepId"] = "social-50",
                                ["KarmaTarget"] = 50,
                                ["Label"] = "Reliability",
                                ["Summary"] = "Fallback lanes solidify.",
                                ["Outcomes"] = new JsonArray(),
                                ["MilestoneBadges"] = new JsonArray(),
                                ["RiskBadges"] = new JsonArray(),
                                ["ExplainEntryId"] = "buildlab.timeline.social-50"
                            },
                            new JsonObject
                            {
                                ["StepId"] = "social-100",
                                ["KarmaTarget"] = 100,
                                ["Label"] = "Anchor",
                                ["Summary"] = "Campaign-ready anchor.",
                                ["Outcomes"] = new JsonArray(),
                                ["MilestoneBadges"] = new JsonArray(),
                                ["RiskBadges"] = new JsonArray
                                {
                                    new JsonObject
                                    {
                                        ["BadgeId"] = "blur",
                                        ["Label"] = "Role blur",
                                        ["Kind"] = BuildLabBadgeKinds.Risk
                                    }
                                },
                                ["ExplainEntryId"] = "buildlab.timeline.social-100"
                            }
                        }
                    }
                },
                ["ExportPayloads"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["PayloadId"] = "payload.social",
                        ["Title"] = "Ops-first Social Operator",
                        ["Summary"] = "Hand-off payload for downstream Build Idea Card or template flows.",
                        ["PayloadKind"] = "build-lab-handoff",
                        ["VariantId"] = "variant.social",
                        ["TimelineId"] = "timeline.social",
                        ["QueryText"] = "street face ops-first",
                        ["SourceDocumentId"] = "source.timeline",
                        ["Fields"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["FieldId"] = "concept",
                                ["Label"] = "Concept",
                                ["Value"] = "Street Face"
                            },
                            new JsonObject
                            {
                                ["FieldId"] = "table-fit",
                                ["Label"] = "Table fit",
                                ["Value"] = "Ops-first",
                                ["Emphasized"] = true
                            }
                        }
                    }
                },
                ["ExportTargets"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["TargetId"] = "target.build-idea-card",
                        ["Label"] = "Build Idea Card",
                        ["TargetKind"] = BuildLabExportTargetKinds.BuildIdeaCard,
                        ["WorkflowId"] = "workflow.coach.build-ideas",
                        ["Enabled"] = true,
                        ["Description"] = "Open grounded Build Idea Card search with the current intake payload.",
                        ["PayloadId"] = "payload.social",
                        ["ActionId"] = "handoff-build-idea",
                        ["Badges"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["BadgeId"] = "build-idea",
                                ["Label"] = "Searchable",
                                ["Kind"] = BuildLabBadgeKinds.Export,
                                ["Emphasized"] = true
                            }
                        }
                    }
                },
                ["Actions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["ActionId"] = "next-variants",
                        ["Label"] = "Hand Off",
                        ["SurfaceId"] = BuildLabSurfaceIds.ExportRail,
                        ["Enabled"] = true,
                        ["TargetId"] = "target.build-idea-card"
                    }
                },
                ["ExplainEntryId"] = "buildlab.intake.concept",
                ["SourceDocumentId"] = "house-rules.table-profile"
            };

            return Task.FromResult<JsonNode>(payload);
        }
    }
}
