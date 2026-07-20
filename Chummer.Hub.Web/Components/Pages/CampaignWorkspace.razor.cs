using Chummer.Hub.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Chummer.Hub.Web.Components.Pages;

public class CampaignWorkspaceBase : ComponentBase
{
    private const int MinimumReasonLength = 8;
    private const int MaximumInviteSecretLength = 256;
    private const int MaximumRunsiteSections = 64;

    [Parameter]
    public string? CampaignId { get; set; }

    [Parameter]
    public string? InviteId { get; set; }

    [Inject]
    protected ICampaignCollaborationClient CampaignClient { get; set; } = null!;

    [Inject]
    protected IJSRuntime JsRuntime { get; set; } = null!;

    protected CampaignWorkspaceProjection? _campaign;
    protected bool _isLoading = true;
    protected bool _isMutating;
    protected string? _inviteMessage;
    protected string? _statusMessage;
    protected string? _errorMessage;
    protected IReadOnlyList<CampaignEligibleCharacterProjection> _eligibleCharacters = [];
    protected string? _selectedEligibleDossierId;
    protected bool _grantGmEditAuthority;
    protected string? _editingDossierId;
    protected string _editingRunnerHandle = string.Empty;
    protected string _editingDisplayName = string.Empty;
    protected string _editingStatus = string.Empty;
    protected string _characterEditReason = string.Empty;
    protected long _editingCharacterRevision;
    protected IReadOnlyList<CampaignPublicationSafeSectionProjection> _editingSections = [];
    protected string? _authorityDossierId;
    protected bool _authorityGrant;
    protected long _authorityBindingRevision;
    protected string _authorityReason = string.Empty;
    protected string _runsiteTitle = string.Empty;
    protected string _runsiteSummary = string.Empty;
    protected string _runsiteGmNotes = string.Empty;
    protected List<RunsiteSectionEditor> _runsiteSections = [];
    protected long _runsiteRevision;
    private string? _pendingInviteSecret;
    private string? _joinIdempotencyKey;
    private string? _characterEditIdempotencyKey;
    private string? _authorityIdempotencyKey;

    protected bool HasPendingInvite
        => !string.IsNullOrWhiteSpace(_pendingInviteSecret)
            && _eligibleCharacters.Count > 0;

    protected bool IsGameMaster
        => _campaign?.CanManage == true
            && CampaignViewerRoles.IsGameMaster(_campaign.ViewerRole);

    protected string RoleLabel
        => IsGameMaster
            ? "Game Master"
            : string.Equals(_campaign?.ViewerRole, CampaignViewerRoles.Player, StringComparison.Ordinal)
                ? "Player"
                : "Read only";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(InviteId))
        {
            await PrepareInviteFragmentAsync();
        }
        else
        {
            await RejectUnexpectedInviteSecretAsync();
        }

        if (string.IsNullOrWhiteSpace(CampaignId))
        {
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        await LoadCampaignAsync(resetEditors: true);
        _isLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    protected void BeginCharacterEdit(PlayerSafeCharacterSheetProjection sheet)
    {
        if (!IsGameMaster || !sheet.CanManage || !sheet.GmEditAuthorityGranted)
        {
            _errorMessage = "The character owner must grant GM edit authority before this sheet can be changed.";
            return;
        }

        _editingDossierId = sheet.DossierId;
        _editingRunnerHandle = sheet.RunnerHandle;
        _editingDisplayName = sheet.DisplayName;
        _editingStatus = sheet.Status;
        _editingCharacterRevision = sheet.Revision;
        _editingSections = sheet.Sections;
        _characterEditReason = string.Empty;
        _characterEditIdempotencyKey = $"sheet-{Guid.NewGuid():N}";
        ClearMutationMessages();
    }

    protected async Task SaveCharacterEditAsync()
    {
        if (!IsGameMaster
            || string.IsNullOrWhiteSpace(_editingDossierId)
            || string.IsNullOrWhiteSpace(_characterEditIdempotencyKey))
        {
            _errorMessage = "Only the GM can edit campaign character sheets.";
            return;
        }

        if (!TryNormalizeReason(_characterEditReason, out string reason))
        {
            _errorMessage = $"Provide a change reason of at least {MinimumReasonLength} characters.";
            return;
        }

        await RunMutationAsync(async () =>
        {
            CampaignMutationReceipt receipt = await CampaignClient.UpdatePlayerSafeSheetAsync(
                CampaignId!,
                _editingDossierId,
                new CampaignCharacterEditRequest(
                    _editingCharacterRevision,
                    _characterEditIdempotencyKey,
                    _editingRunnerHandle.Trim(),
                    _editingDisplayName.Trim(),
                    _editingStatus.Trim(),
                    reason,
                    _editingSections));
            if (!receipt.Applied)
            {
                _editingCharacterRevision = receipt.Revision;
                _errorMessage = receipt.Message ?? "Revision conflict. Reload the sheet before saving again.";
                return;
            }

            _editingDossierId = null;
            _characterEditReason = string.Empty;
            _characterEditIdempotencyKey = null;
            _statusMessage = "Player-safe sheet saved.";
            await LoadCampaignAsync(resetEditors: false);
        });
    }

    protected void BeginGmAuthorityUpdate(PlayerSafeCharacterSheetProjection sheet)
    {
        if (!sheet.IsOwnedByViewer)
        {
            _errorMessage = "Only the authoritative character owner can change GM edit authority.";
            return;
        }

        _authorityDossierId = sheet.DossierId;
        _authorityBindingRevision = sheet.GmAuthorityBindingRevision;
        _authorityGrant = !sheet.GmEditAuthorityGranted;
        _authorityReason = string.Empty;
        _authorityIdempotencyKey = $"gm-authority-{Guid.NewGuid():N}";
        ClearMutationMessages();
    }

    protected async Task SaveGmAuthorityUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(_authorityDossierId)
            || string.IsNullOrWhiteSpace(_authorityIdempotencyKey)
            || _campaign?.Roster
                .Select(member => member.PlayerSafeSheet)
                .FirstOrDefault(sheet => sheet is not null
                    && sheet.IsOwnedByViewer
                    && string.Equals(
                        sheet.DossierId,
                        _authorityDossierId,
                        StringComparison.OrdinalIgnoreCase)) is null)
        {
            _errorMessage = "Only the authoritative character owner can change GM edit authority.";
            return;
        }

        if (!TryNormalizeReason(_authorityReason, out string reason))
        {
            _errorMessage = $"Provide a consent change reason of at least {MinimumReasonLength} characters.";
            return;
        }

        await RunMutationAsync(async () =>
        {
            CampaignGmAuthorityReceipt receipt = await CampaignClient.UpdateGmEditAuthorityAsync(
                CampaignId!,
                _authorityDossierId,
                new CampaignGmAuthorityUpdateRequest(
                    _authorityBindingRevision,
                    _authorityGrant,
                    _authorityIdempotencyKey,
                    reason));
            if (!receipt.Applied)
            {
                _authorityBindingRevision = receipt.BindingRevision;
                _errorMessage = receipt.Message ?? "Authority revision conflict. Reload before trying again.";
                return;
            }

            bool granted = receipt.GmEditAuthorityGranted;
            _authorityDossierId = null;
            _authorityReason = string.Empty;
            _authorityIdempotencyKey = null;
            _statusMessage = granted
                ? "GM edit authority granted. You can revoke it at any time."
                : "GM edit authority revoked.";
            await LoadCampaignAsync(resetEditors: false);
        });
    }

    protected void AddRunsiteSection()
    {
        if (IsGameMaster && _runsiteSections.Count < MaximumRunsiteSections)
        {
            _runsiteSections.Add(new RunsiteSectionEditor());
        }
    }

    protected void RemoveRunsiteSection(RunsiteSectionEditor section)
    {
        if (IsGameMaster)
        {
            _runsiteSections.Remove(section);
        }
    }

    protected async Task SaveRunsiteDraftAsync()
    {
        if (!IsGameMaster || string.IsNullOrWhiteSpace(_campaign?.ActiveRunId))
        {
            _errorMessage = "Only the GM can edit a campaign Runsite draft.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_runsiteTitle)
            || string.IsNullOrWhiteSpace(_runsiteSummary)
            || _runsiteSections.Any(section =>
                string.IsNullOrWhiteSpace(section.Heading)
                || string.IsNullOrWhiteSpace(section.Body)))
        {
            _errorMessage = "Provide a title, player summary, and complete player sections before saving.";
            return;
        }

        await RunMutationAsync(async () =>
        {
            CampaignMutationReceipt receipt = await CampaignClient.SaveRunsiteDraftAsync(
                CampaignId!,
                new RunsiteDraftSaveRequest(
                    _campaign.ActiveRunId,
                    _runsiteRevision,
                    _runsiteTitle.Trim(),
                    _runsiteSummary.Trim(),
                    _runsiteSections
                        .Select(section => new RunsitePlayerSectionProjection(
                            section.Heading.Trim(),
                            section.Body.Trim()))
                        .ToArray(),
                    NullIfWhiteSpace(_runsiteGmNotes)));
            if (!receipt.Applied)
            {
                _runsiteRevision = receipt.Revision;
                _errorMessage = receipt.Message ?? "Revision conflict. Reload the Runsite draft before saving again.";
                return;
            }

            _runsiteRevision = receipt.Revision;
            _statusMessage = "Runsite draft saved. Players still see only the published page.";
            await LoadCampaignAsync(resetEditors: false);
        });
    }

    protected async Task PublishRunsiteAsync()
    {
        if (!IsGameMaster || string.IsNullOrWhiteSpace(_campaign?.ActiveRunId))
        {
            _errorMessage = "Only the GM can publish a campaign Runsite.";
            return;
        }

        await RunMutationAsync(async () =>
        {
            CampaignMutationReceipt receipt = await CampaignClient.PublishRunsiteAsync(
                CampaignId!,
                new RunsitePublishRequest(_campaign.ActiveRunId, _runsiteRevision));
            if (!receipt.Applied)
            {
                _runsiteRevision = receipt.Revision;
                _errorMessage = receipt.Message ?? "Revision conflict. Reload the Runsite before publishing.";
                return;
            }

            _runsiteRevision = receipt.Revision;
            _statusMessage = "Runsite published.";
            await LoadCampaignAsync(resetEditors: true);
        });
    }

    private async Task PrepareInviteFragmentAsync()
    {
        CampaignInviteFragmentHandoff? handoff;
        try
        {
            handoff = await JsRuntime.InvokeAsync<CampaignInviteFragmentHandoff>(
                "chummerCampaignJoin.readInviteFragment");
        }
        catch
        {
            _errorMessage = "The invite handoff could not be inspected. Close this tab and request a new invite.";
            await TryScrubInviteLocationAsync();
            return;
        }

        if (handoff is null || !handoff.MustScrub)
        {
            _errorMessage = "This campaign invite is missing its one-time secret. Request a new invite from the GM.";
            return;
        }

        if (string.Equals(handoff.Status, CampaignInviteHandoffStatuses.RejectedQuery, StringComparison.Ordinal))
        {
            handoff.Secret = null;
            _errorMessage = "This invite was rejected because secrets are not accepted in the query string.";
            await TryScrubInviteLocationAsync();
            return;
        }

        if (!handoff.HasUsableFragmentSecret
            || handoff.Secret!.Length > MaximumInviteSecretLength)
        {
            handoff.Secret = null;
            _errorMessage = "The campaign invite fragment is invalid. Request a new invite from the GM.";
            await TryScrubInviteLocationAsync();
            return;
        }

        _pendingInviteSecret = handoff.Secret;
        handoff.Secret = null;
        try
        {
            _eligibleCharacters = await CampaignClient.GetEligibleCharactersAsync();
        }
        catch
        {
            _errorMessage = "Existing characters could not be loaded. The invite was cleared; request a new link before trying again.";
            await ClearPendingInviteAsync();
            return;
        }

        if (_eligibleCharacters.Count == 0)
        {
            _errorMessage = "No eligible existing character is available. Create or restore your character, then request a new invite.";
            await ClearPendingInviteAsync();
        }
    }

    protected async Task AcceptPendingInviteAsync()
    {
        if (_isMutating)
        {
            return;
        }

        CampaignEligibleCharacterProjection? selected = _eligibleCharacters.FirstOrDefault(character =>
            string.Equals(
                character.DossierId,
                _selectedEligibleDossierId,
                StringComparison.OrdinalIgnoreCase));
        if (selected is null
            || string.IsNullOrWhiteSpace(_pendingInviteSecret)
            || string.IsNullOrWhiteSpace(InviteId))
        {
            _errorMessage = "Select one of your existing authoritative characters before joining.";
            return;
        }

        ClearMutationMessages();
        _isMutating = true;
        _joinIdempotencyKey ??= $"join-{Guid.NewGuid():N}";
        CampaignJoinReceipt? receipt = null;
        string? acceptedCampaignId = null;
        bool scrubbed;
        try
        {
            receipt = await CampaignClient.JoinCampaignAsync(
                InviteId,
                new CampaignJoinRequest(
                    _pendingInviteSecret,
                    selected.DossierId,
                    selected.AuthoritativeCharacterId,
                    selected.CurrentRevision,
                    _grantGmEditAuthority,
                    _joinIdempotencyKey));
            if (!receipt.Joined
                || string.IsNullOrWhiteSpace(receipt.CampaignId)
                || receipt.CampaignId.Length > 128
                || !string.Equals(receipt.DossierId, selected.DossierId, StringComparison.Ordinal))
            {
                _errorMessage = "The campaign invite could not be accepted.";
            }
            else
            {
                acceptedCampaignId = receipt.CampaignId;
            }
        }
        catch
        {
            _errorMessage = "The campaign invite could not be accepted. Request a new invite if it has expired.";
        }
        finally
        {
            _pendingInviteSecret = null;
            _eligibleCharacters = [];
            _selectedEligibleDossierId = null;
            _grantGmEditAuthority = false;
            string? safePath = acceptedCampaignId is null
                ? null
                : $"/account/campaigns/{Uri.EscapeDataString(acceptedCampaignId)}";
            scrubbed = await TryScrubInviteLocationAsync(safePath);
            _isMutating = false;
        }

        _joinIdempotencyKey = null;
        if (!scrubbed || acceptedCampaignId is null || receipt is null)
        {
            return;
        }

        CampaignId = acceptedCampaignId;
        _inviteMessage = receipt.AlreadyJoined
            ? "Campaign membership confirmed. The invite secret was removed from browser history."
            : "Campaign invite accepted. The secret was removed from browser history.";
        _isLoading = true;
        await LoadCampaignAsync(resetEditors: true);
        _isLoading = false;
    }

    protected async Task CancelPendingInviteAsync()
    {
        _inviteMessage = null;
        _errorMessage = "Campaign invite cancelled. The secret was removed from browser history.";
        await ClearPendingInviteAsync();
    }

    private async Task ClearPendingInviteAsync()
    {
        _pendingInviteSecret = null;
        _eligibleCharacters = [];
        _selectedEligibleDossierId = null;
        _grantGmEditAuthority = false;
        _joinIdempotencyKey = null;
        await TryScrubInviteLocationAsync();
    }

    private async Task RejectUnexpectedInviteSecretAsync()
    {
        CampaignInviteFragmentHandoff? handoff;
        try
        {
            handoff = await JsRuntime.InvokeAsync<CampaignInviteFragmentHandoff>(
                "chummerCampaignJoin.readInviteFragment");
        }
        catch
        {
            return;
        }

        if (handoff is null || !handoff.MustScrub)
        {
            return;
        }

        handoff.Secret = null;
        _errorMessage = string.Equals(
            handoff.Status,
            CampaignInviteHandoffStatuses.RejectedQuery,
            StringComparison.Ordinal)
            ? "Invite secrets are not accepted in the query string. Use the original GM-issued invite link."
            : "An invite secret is only accepted on its exact campaign invite route.";
        await TryScrubInviteLocationAsync();
    }

    private async Task<bool> TryScrubInviteLocationAsync(string? safePath = null)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("chummerCampaignJoin.scrubInviteLocation", safePath);
            return true;
        }
        catch
        {
            _errorMessage = "Browser history cleanup failed. Close this tab before continuing.";
            return false;
        }
    }

    private async Task LoadCampaignAsync(bool resetEditors)
    {
        try
        {
            CampaignWorkspaceProjection campaign = await CampaignClient.GetCampaignAsync(CampaignId!);
            if (!string.Equals(campaign.CampaignId, CampaignId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Campaign authority mismatch.");
            }

            _campaign = campaign;
            if (IsGameMaster)
            {
                RunsiteDraftProjection? draft = campaign.Runsite.Draft;
                _runsiteTitle = draft?.Title ?? campaign.Runsite.Published?.Title ?? string.Empty;
                _runsiteSummary = draft?.Summary ?? campaign.Runsite.Published?.Summary ?? string.Empty;
                _runsiteGmNotes = draft?.GmNotes ?? string.Empty;
                _runsiteSections = (draft?.PlayerSections
                        ?? campaign.Runsite.Published?.Sections
                        ?? [])
                    .Select(section => new RunsiteSectionEditor(section.Heading, section.Body))
                    .ToList();
                _runsiteRevision = campaign.Runsite.Revision;
                if (resetEditors)
                {
                    _editingDossierId = null;
                    _characterEditIdempotencyKey = null;
                    _authorityDossierId = null;
                    _authorityIdempotencyKey = null;
                }
            }
            else
            {
                // Fail closed even if an upstream projection accidentally includes GM-only draft data.
                _editingDossierId = null;
                _characterEditIdempotencyKey = null;
                _authorityDossierId = null;
                _authorityIdempotencyKey = null;
                _editingSections = [];
                _runsiteTitle = string.Empty;
                _runsiteSummary = string.Empty;
                _runsiteGmNotes = string.Empty;
                _runsiteSections.Clear();
                _runsiteRevision = 0;
            }
        }
        catch
        {
            _campaign = null;
            _errorMessage ??= "Campaign workspace is unavailable for this account.";
        }
    }

    private async Task RunMutationAsync(Func<Task> mutation)
    {
        if (_isMutating)
        {
            return;
        }

        ClearMutationMessages();
        _isMutating = true;
        try
        {
            await mutation();
        }
        catch (CampaignCollaborationException exception) when (exception.StatusCode == 409)
        {
            _errorMessage = "Revision conflict. The current campaign state has been reloaded.";
            await LoadCampaignAsync(resetEditors: true);
        }
        catch
        {
            _errorMessage = "The campaign change could not be applied.";
        }
        finally
        {
            _isMutating = false;
        }
    }

    private void ClearMutationMessages()
    {
        _statusMessage = null;
        _errorMessage = null;
    }

    private static bool TryNormalizeReason(string value, out string reason)
    {
        reason = (value ?? string.Empty).Trim();
        return reason.Length >= MinimumReasonLength && reason.Length <= 500;
    }

    private static string? NullIfWhiteSpace(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected sealed class RunsiteSectionEditor
    {
        public RunsiteSectionEditor()
            : this(string.Empty, string.Empty)
        {
        }

        public RunsiteSectionEditor(string heading, string body)
        {
            Heading = heading;
            Body = body;
        }

        public string Heading { get; set; }

        public string Body { get; set; }
    }
}
