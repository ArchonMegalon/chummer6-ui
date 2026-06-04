using Chummer.Contracts.AI;
using Chummer.Contracts.Hub;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Microsoft.AspNetCore.Components;

namespace Chummer.Hub.Web.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject] private BrowserHubApiClient HubClient { get; set; } = null!;
    [Inject] private BrowserHubCoachApiClient CoachClient { get; set; } = null!;

    private HubCatalogResultPage _catalog = new(new BrowseQuery(string.Empty, new Dictionary<string, IReadOnlyList<string>>(), HubCatalogSortIds.Title), [], [], [], 0);
    private HubProjectDetailProjection? _selectedDetail;
    private HubProjectCompatibilityMatrix? _compatibility;
    private HubProjectInstallPreviewReceipt? _installPreview;
    private HubPublishDraftList _drafts = new([]);
    private HubDraftDetailProjection? _selectedDraftDetail;
    private HubModerationQueue _moderationQueue = new([]);
    private AiGatewayStatusProjection? _coachStatus;
    private AiProviderHealthProjection? _coachProvider;
    private AiConversationAuditSummary? _coachAudit;
    private string? _statusMessage;
    private string? _errorMessage;
    private string _draftProjectId = string.Empty;
    private string _draftTitle = string.Empty;
    private string _draftSummary = string.Empty;
    private string _draftDescription = string.Empty;
    private string _submissionNotes = string.Empty;
    private string _moderationState = string.Empty;
    private string _moderationNotes = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadCatalogAsync(), LoadCoachAsync());
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            _catalog = await HubClient.SearchAsync(new BrowseQuery(string.Empty, new Dictionary<string, IReadOnlyList<string>>(), HubCatalogSortIds.Title));
            _errorMessage = null;
        }
        catch (Exception ex)
        {
            _catalog = new HubCatalogResultPage(new BrowseQuery(string.Empty, new Dictionary<string, IReadOnlyList<string>>(), HubCatalogSortIds.Title), [], [], [], 0);
            _errorMessage = ex.Message;
        }
    }

    private async Task LoadCoachAsync()
    {
        _coachStatus = await CoachClient.GetStatusAsync();
        _coachProvider = (await CoachClient.GetProviderHealthAsync(AiRouteTypes.Coach)).FirstOrDefault();
        _coachAudit = (await CoachClient.GetConversationAuditsAsync(AiRouteTypes.Coach, 3)).FirstOrDefault();
    }

    private async Task SelectItemAsync(HubCatalogItem item)
    {
        _selectedDetail = await HubClient.GetProjectDetailAsync(item.Kind, item.ItemId);
        _compatibility = await HubClient.GetCompatibilityAsync(item.Kind, item.ItemId);
        _installPreview = null;
    }

    private async Task PreviewInstallAsync()
    {
        if (_selectedDetail is null)
        {
            return;
        }

        _installPreview = await HubClient.PreviewInstallAsync(_selectedDetail.Summary.Kind, _selectedDetail.Summary.ItemId);
    }

    private async Task LoadDraftsAsync()
    {
        _drafts = await HubClient.ListDraftsAsync();
    }

    private async Task SelectDraftAsync(string draftId)
    {
        _selectedDraftDetail = await HubClient.GetDraftDetailAsync(draftId);
        HydrateDraftEditor(_selectedDraftDetail.Draft, _selectedDraftDetail.Description);
    }

    private async Task CreateDraftAsync()
    {
        HubPublishDraftReceipt created = await HubClient.CreateDraftAsync(new HubPublishDraftRequest(
            ProjectKind: HubCatalogItemKinds.RuleProfile,
            ProjectId: _draftProjectId,
            RulesetId: RulesetDefaults.Sr5,
            Title: _draftTitle,
            Summary: _draftSummary,
            Description: _draftDescription));
        _statusMessage = $"Created draft '{created.Title}'.";
        _selectedDraftDetail = new HubDraftDetailProjection(created, null, _draftDescription);
        _drafts = new HubPublishDraftList([created]);
    }

    private async Task SaveDraftAsync()
    {
        if (_selectedDraftDetail is null)
        {
            return;
        }

        HubPublishDraftReceipt updated = await HubClient.UpdateDraftAsync(_selectedDraftDetail.Draft.DraftId, new HubUpdateDraftRequest(
            Title: _draftTitle,
            Summary: _draftSummary,
            Description: _draftDescription,
            PublisherId: _selectedDraftDetail.Draft.PublisherId));
        _statusMessage = $"Saved draft '{updated.Title}'.";
        _drafts = await HubClient.ListDraftsAsync();
        _selectedDraftDetail = _selectedDraftDetail with
        {
            Draft = updated,
            Description = _draftDescription
        };
    }

    private async Task SubmitDraftAsync()
    {
        if (_selectedDraftDetail is null)
        {
            return;
        }

        HubPublishDraftReceipt draft = _selectedDraftDetail.Draft;
        await HubClient.SubmitDraftAsync(draft.ProjectKind, draft.ProjectId, draft.RulesetId, new HubSubmitProjectRequest(_submissionNotes, draft.PublisherId));
        _selectedDraftDetail = await HubClient.GetDraftDetailAsync(draft.DraftId);
        _statusMessage = $"Submitted draft '{draft.ProjectId}' for review.";
    }

    private async Task ArchiveDraftAsync()
    {
        if (_selectedDraftDetail is null)
        {
            return;
        }

        HubPublishDraftReceipt archived = await HubClient.ArchiveDraftAsync(_selectedDraftDetail.Draft.DraftId);
        _statusMessage = $"Archived draft '{archived.Title}'.";
        _selectedDraftDetail = _selectedDraftDetail with { Draft = archived };
    }

    private async Task DeleteDraftAsync()
    {
        if (_selectedDraftDetail is null)
        {
            return;
        }

        string deletedTitle = _selectedDraftDetail.Draft.Title;
        await HubClient.DeleteDraftAsync(_selectedDraftDetail.Draft.DraftId);
        _statusMessage = $"Deleted draft '{deletedTitle}'.";
        _selectedDraftDetail = null;
        _drafts = new HubPublishDraftList([]);
    }

    private async Task LoadModerationQueueAsync()
    {
        _moderationQueue = await HubClient.ListModerationQueueAsync(_moderationState);
    }

    private async Task ApproveModerationAsync(string caseId)
    {
        HubModerationDecisionReceipt receipt = await HubClient.ApproveModerationAsync(caseId, new HubModerationDecisionRequest(_moderationNotes));
        _statusMessage = $"Approved moderation case '{caseId}'.";
        ReplaceModerationItem(receipt);
    }

    private async Task RejectModerationAsync(string caseId)
    {
        HubModerationDecisionReceipt receipt = await HubClient.RejectModerationAsync(caseId, new HubModerationDecisionRequest(_moderationNotes));
        _statusMessage = $"Rejected moderation case '{caseId}'.";
        ReplaceModerationItem(receipt);
    }

    private void ReplaceModerationItem(HubModerationDecisionReceipt receipt)
    {
        _moderationQueue = new HubModerationQueue(_moderationQueue.Items
            .Select(item => string.Equals(item.CaseId, receipt.CaseId, StringComparison.Ordinal)
                ? item with { State = receipt.State }
                : item)
            .ToArray());
    }

    private void HydrateDraftEditor(HubPublishDraftReceipt draft, string? description)
    {
        _draftProjectId = draft.ProjectId;
        _draftTitle = draft.Title;
        _draftSummary = draft.Summary ?? string.Empty;
        _draftDescription = description ?? string.Empty;
    }

    private string BuildCoachLaunchUri(string? conversationId, string? runtimeFingerprint, string? rulesetId)
        => AiCoachLaunchQuery.BuildRelativeUri(
            "/coach/",
            new AiCoachLaunchContext(
                RouteType: AiRouteTypes.Coach,
                ConversationId: conversationId,
                RuntimeFingerprint: runtimeFingerprint,
                RulesetId: rulesetId));

    private string? ResolveRulesetId()
        => _selectedDetail?.Summary.RulesetId
            ?? _catalog.Items.FirstOrDefault()?.RulesetId
            ?? RulesetDefaults.Sr5;

    private static string FormatTransport(AiProviderHealthProjection provider)
        => $"ready · base {(provider.TransportBaseUrlConfigured ? "yes" : "no")} · model {(provider.TransportModelConfigured ? "yes" : "no")} · keys primary {provider.PrimaryCredentialCount} / fallback {provider.FallbackCredentialCount} · route {provider.LastRouteType} · binding {provider.LastCredentialTier} / slot {provider.LastCredentialSlotIndex}";

    private static string FormatBudget(AiBudgetSnapshot? budget)
        => budget is null
            ? "n/a"
            : $"{budget.MonthlyConsumed} / {budget.MonthlyAllowance} {budget.BudgetUnit}";

    private static string FormatRecommendations(AiStructuredAnswer? answer)
        => answer is null || answer.Recommendations.Count == 0
            ? "0"
            : $"{answer.Recommendations.Count} · {answer.Recommendations[0].Title}";

    private static string FormatEvidence(AiStructuredAnswer? answer)
        => answer is null || answer.Evidence.Count == 0
            ? "0"
            : $"{answer.Evidence.Count} · {answer.Evidence[0].Title}";

    private static string FormatRisks(AiStructuredAnswer? answer)
        => answer is null || answer.Risks.Count == 0
            ? "0"
            : $"{answer.Risks.Count} · {answer.Risks[0].Title}";

    private static string FormatSources(AiStructuredAnswer? answer)
        => answer is null
            ? "0 sources / 0 action drafts"
            : $"{answer.Sources.Count} sources / {answer.ActionDrafts.Count} action drafts";
}
