using Chummer.Contracts.AI;
using Chummer.Contracts.Hub;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Microsoft.AspNetCore.Components;

namespace Chummer.Hub.Web.Components.Pages;

public class HomeBase : ComponentBase
{
    [Inject] protected BrowserHubApiClient HubClient { get; set; } = null!;
    [Inject] protected BrowserHubCoachApiClient CoachClient { get; set; } = null!;

    protected HubCatalogResultPage _catalog = new(new BrowseQuery(string.Empty, new Dictionary<string, IReadOnlyList<string>>(), HubCatalogSortIds.Title), [], [], [], 0);
    protected HubProjectDetailProjection? _selectedDetail;
    protected HubProjectCompatibilityMatrix? _compatibility;
    protected HubProjectInstallPreviewReceipt? _installPreview;
    protected HubPublishDraftList _drafts = new([]);
    protected HubDraftDetailProjection? _selectedDraftDetail;
    protected HubModerationQueue _moderationQueue = new([]);
    protected AiGatewayStatusProjection? _coachStatus;
    protected AiProviderHealthProjection? _coachProvider;
    protected AiConversationAuditSummary? _coachAudit;
    protected string? _statusMessage;
    protected string? _errorMessage;
    protected string? _coachErrorMessage;
    protected bool _isCatalogLoading;
    protected bool _isCoachLoading;
    protected bool _isDetailLoading;
    protected bool _isPreviewLoading;
    protected bool _isDraftsLoading;
    protected bool _isDraftDetailLoading;
    protected bool _isModerationLoading;
    protected bool _canModerate;
    protected string _draftProjectId = string.Empty;
    protected string _draftTitle = string.Empty;
    protected string _draftSummary = string.Empty;
    protected string _draftDescription = string.Empty;
    protected string _submissionNotes = string.Empty;
    protected string _moderationState = string.Empty;
    protected string _moderationNotes = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            LoadCatalogAsync(),
            LoadCoachAsync(),
            LoadModerationCapabilityAsync());
    }

    protected async Task LoadModerationCapabilityAsync()
    {
        _canModerate = false;
        _moderationQueue = new HubModerationQueue([]);
        try
        {
            _canModerate = await HubClient.CanModerateAsync();
        }
        catch
        {
            // A denied, malformed, or unavailable capability probe must never
            // reveal or retain moderator-only controls.
            _canModerate = false;
        }
    }

    protected async Task LoadCatalogAsync()
    {
        _isCatalogLoading = true;
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
        finally
        {
            _isCatalogLoading = false;
        }
    }

    protected async Task LoadCoachAsync()
    {
        _isCoachLoading = true;
        try
        {
            _coachStatus = await CoachClient.GetStatusAsync();
            _coachProvider = (await CoachClient.GetProviderHealthAsync(AiRouteTypes.Coach)).FirstOrDefault();
            _coachAudit = (await CoachClient.GetConversationAuditsAsync(AiRouteTypes.Coach, 3)).FirstOrDefault();
            _coachErrorMessage = null;
        }
        catch (Exception ex)
        {
            _coachProvider = null;
            _coachAudit = null;
            _coachErrorMessage = ex.Message;
        }
        finally
        {
            _isCoachLoading = false;
        }
    }

    protected async Task SelectItemAsync(HubCatalogItem item)
    {
        _isDetailLoading = true;
        try
        {
            _selectedDetail = await HubClient.GetProjectDetailAsync(item.Kind, item.ItemId);
            _compatibility = await HubClient.GetCompatibilityAsync(item.Kind, item.ItemId);
            _installPreview = null;
            _errorMessage = null;
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isDetailLoading = false;
        }
    }

    protected async Task PreviewInstallAsync()
    {
        if (_selectedDetail is null)
        {
            return;
        }

        _isPreviewLoading = true;
        try
        {
            _installPreview = await HubClient.PreviewInstallAsync(_selectedDetail.Summary.Kind, _selectedDetail.Summary.ItemId);
            _errorMessage = null;
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isPreviewLoading = false;
        }
    }

    protected async Task LoadDraftsAsync()
    {
        _isDraftsLoading = true;
        try
        {
            _drafts = await HubClient.ListDraftsAsync();
            _errorMessage = null;
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isDraftsLoading = false;
        }
    }

    protected async Task SelectDraftAsync(string draftId)
    {
        _isDraftDetailLoading = true;
        try
        {
            _selectedDraftDetail = await HubClient.GetDraftDetailAsync(draftId);
            HydrateDraftEditor(_selectedDraftDetail.Draft, _selectedDraftDetail.Description);
            _errorMessage = null;
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isDraftDetailLoading = false;
        }
    }

    protected async Task CreateDraftAsync()
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

    protected async Task SaveDraftAsync()
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

    protected async Task SubmitDraftAsync()
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

    protected async Task ArchiveDraftAsync()
    {
        if (_selectedDraftDetail is null)
        {
            return;
        }

        HubPublishDraftReceipt archived = await HubClient.ArchiveDraftAsync(_selectedDraftDetail.Draft.DraftId);
        _statusMessage = $"Archived draft '{archived.Title}'.";
        _selectedDraftDetail = _selectedDraftDetail with { Draft = archived };
    }

    protected async Task DeleteDraftAsync()
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

    protected async Task LoadModerationQueueAsync()
    {
        if (!_canModerate)
        {
            return;
        }

        _isModerationLoading = true;
        try
        {
            _moderationQueue = await HubClient.ListModerationQueueAsync(_moderationState);
            _errorMessage = null;
        }
        catch (Exception ex)
        {
            RevokeModerationCapability(ex);
        }
        finally
        {
            _isModerationLoading = false;
        }
    }

    protected async Task ApproveModerationAsync(string caseId)
    {
        if (!_canModerate)
        {
            return;
        }

        try
        {
            HubModerationDecisionReceipt receipt = await HubClient.ApproveModerationAsync(
                caseId,
                new HubModerationDecisionRequest(_moderationNotes));
            _statusMessage = $"Approved moderation case '{caseId}'.";
            ReplaceModerationItem(receipt);
        }
        catch (Exception ex)
        {
            RevokeModerationCapability(ex);
        }
    }

    protected async Task RejectModerationAsync(string caseId)
    {
        if (!_canModerate)
        {
            return;
        }

        try
        {
            HubModerationDecisionReceipt receipt = await HubClient.RejectModerationAsync(
                caseId,
                new HubModerationDecisionRequest(_moderationNotes));
            _statusMessage = $"Rejected moderation case '{caseId}'.";
            ReplaceModerationItem(receipt);
        }
        catch (Exception ex)
        {
            RevokeModerationCapability(ex);
        }
    }

    protected void ReplaceModerationItem(HubModerationDecisionReceipt receipt)
    {
        _moderationQueue = new HubModerationQueue(_moderationQueue.Items
            .Select(item => string.Equals(item.CaseId, receipt.CaseId, StringComparison.Ordinal)
                ? item with { State = receipt.State }
                : item)
            .ToArray());
    }

    private void RevokeModerationCapability(Exception exception)
    {
        _canModerate = false;
        _moderationQueue = new HubModerationQueue([]);
        _errorMessage = exception.Message;
    }

    protected void HydrateDraftEditor(HubPublishDraftReceipt draft, string? description)
    {
        _draftProjectId = draft.ProjectId;
        _draftTitle = draft.Title;
        _draftSummary = draft.Summary ?? string.Empty;
        _draftDescription = description ?? string.Empty;
    }

    protected string BuildCoachLaunchUri(string? conversationId, string? runtimeFingerprint, string? rulesetId)
        => AiCoachLaunchQuery.BuildRelativeUri(
            "/coach/",
            new AiCoachLaunchContext(
                RouteType: AiRouteTypes.Coach,
                ConversationId: conversationId,
                RuntimeFingerprint: runtimeFingerprint,
                RulesetId: rulesetId));

    protected string? ResolveRulesetId()
        => _selectedDetail?.Summary.RulesetId
            ?? _catalog.Items.FirstOrDefault()?.RulesetId
            ?? RulesetDefaults.Sr5;

    protected static string FormatTransport(AiProviderHealthProjection provider)
        => $"ready · base {(provider.TransportBaseUrlConfigured ? "yes" : "no")} · model {(provider.TransportModelConfigured ? "yes" : "no")} · keys primary {provider.PrimaryCredentialCount} / fallback {provider.FallbackCredentialCount} · route {provider.LastRouteType} · binding {provider.LastCredentialTier} / slot {provider.LastCredentialSlotIndex}";

    protected static string FormatBudget(AiBudgetSnapshot? budget)
        => budget is null
            ? "n/a"
            : $"{budget.MonthlyConsumed} / {budget.MonthlyAllowance} {budget.BudgetUnit}";

    protected static string FormatRecommendations(AiStructuredAnswer? answer)
        => answer is null || answer.Recommendations.Count == 0
            ? "0"
            : $"{answer.Recommendations.Count} · {answer.Recommendations[0].Title}";

    protected static string FormatEvidence(AiStructuredAnswer? answer)
        => answer is null || answer.Evidence.Count == 0
            ? "0"
            : $"{answer.Evidence.Count} · {answer.Evidence[0].Title}";

    protected static string FormatRisks(AiStructuredAnswer? answer)
        => answer is null || answer.Risks.Count == 0
            ? "0"
            : $"{answer.Risks.Count} · {answer.Risks[0].Title}";

    protected static string FormatSources(AiStructuredAnswer? answer)
        => answer is null
            ? "0 sources / 0 action drafts"
            : $"{answer.Sources.Count} sources / {answer.ActionDrafts.Count} action drafts";
}
