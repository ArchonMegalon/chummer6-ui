using System.Security.Cryptography;
using System.Text.Json;
using Chummer.Hub.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Chummer.Hub.Web.Components.Pages;

public class CampaignWorkspaceBase : ComponentBase, IDisposable
{
    private const int MinimumReasonLength = 8;
    private const int MaximumInviteSecretLength = 256;
    private const int MaximumRunsiteSections = 64;
    private static readonly JsonSerializerOptions IdempotencyPayloadJsonOptions =
        new(JsonSerializerDefaults.Web);

    [Parameter]
    public string? CampaignId { get; set; }

    [Parameter]
    public string? InviteId { get; set; }

    [Inject]
    protected ICampaignCollaborationClient CampaignClient { get; set; } = null!;

    [Inject]
    protected IJSRuntime JsRuntime { get; set; } = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = null!;

    protected CampaignWorkspaceProjection? _campaign;
    protected bool _isLoading = true;
    protected bool _isMutating;
    protected string? _inviteMessage;
    protected string? _statusMessage;
    protected string? _errorMessage;
    protected IReadOnlyList<CampaignEligibleCharacterProjection> _eligibleCharacters = [];
    protected IReadOnlyList<CampaignListItemProjection> _campaigns = [];
    protected string? _selectedEligibleDossierId;
    protected bool _grantGmEditAuthority;
    protected string _joinCode = string.Empty;
    protected string _newCampaignName = string.Empty;
    protected string _newCampaignSummary = string.Empty;
    protected string _newCampaignRunTitle = string.Empty;
    protected int _inviteExpiresInMinutes = 1440;
    protected int _inviteMaxUses = 1;
    protected CampaignInviteSecretProjection? _issuedInvite;
    protected string? _issuedInviteAbsoluteLink;
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
    private PendingMutationKey? _campaignCreateMutationKey;
    private PendingMutationKey? _campaignInviteMutationKey;
    private PendingMutationKey? _runsiteDraftMutationKey;
    private PendingMutationKey? _runsitePublishMutationKey;
    private bool _hasObservedRoute;
    private string? _observedCampaignId;
    private string? _observedInviteId;
    private long _routeVersion;
    private bool _routeInitializationPending;
    private CancellationTokenSource? _routeCancellation;
    private bool _disposed;

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

    protected override void OnParametersSet()
    {
        if (_hasObservedRoute
            && string.Equals(_observedCampaignId, CampaignId, StringComparison.Ordinal)
            && string.Equals(_observedInviteId, InviteId, StringComparison.Ordinal))
        {
            return;
        }

        BeginRouteTransition(CampaignId, InviteId);
        _routeInitializationPending = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_routeInitializationPending || _disposed)
        {
            return;
        }

        _routeInitializationPending = false;
        long routeVersion = _routeVersion;
        string? campaignId = _observedCampaignId;
        string? inviteId = _observedInviteId;
        CancellationToken cancellationToken = _routeCancellation?.Token ?? CancellationToken.None;

        await InitializeRouteAsync(
            routeVersion,
            campaignId,
            inviteId,
            cancellationToken);
        if (!IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
        {
            return;
        }

        _isLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        _disposed = true;
        _routeCancellation?.Cancel();
        _routeCancellation?.Dispose();
        _routeCancellation = null;
        GC.SuppressFinalize(this);
    }

    private async Task InitializeRouteAsync(
        long routeVersion,
        string? campaignId,
        string? inviteId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(campaignId)
            && !string.IsNullOrWhiteSpace(inviteId))
        {
            if (IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
            {
                _errorMessage = "The campaign route is invalid. Return to your campaign list and try again.";
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(inviteId))
        {
            await PrepareInviteFragmentAsync(
                routeVersion,
                campaignId,
                inviteId,
                cancellationToken);
            return;
        }

        await RejectUnexpectedInviteSecretAsync(
            routeVersion,
            campaignId,
            inviteId,
            cancellationToken);
        if (!IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(campaignId))
        {
            await LoadCampaignIndexAsync(
                routeVersion,
                campaignId,
                inviteId,
                cancellationToken);
            return;
        }

        await LoadCampaignAsync(
            campaignId,
            resetEditors: true,
            routeVersion,
            inviteId,
            cancellationToken);
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

        string title = _runsiteTitle.Trim();
        string summary = _runsiteSummary.Trim();
        string? gmNotes = NullIfWhiteSpace(_runsiteGmNotes);
        RunsitePlayerSectionProjection[] playerSections = _runsiteSections
            .Select(section => new RunsitePlayerSectionProjection(
                section.Heading.Trim(),
                section.Body.Trim()))
            .ToArray();
        string idempotencyKey = AcquireMutationKey(
            ref _runsiteDraftMutationKey,
            "runsite-draft",
            new
            {
                CampaignId,
                RunId = _campaign.ActiveRunId,
                ExpectedRevision = _runsiteRevision,
                Title = title,
                Summary = summary,
                PlayerSections = playerSections,
                GmNotes = gmNotes
            });

        await RunMutationAsync(async () =>
        {
            CampaignMutationReceipt receipt = await CampaignClient.SaveRunsiteDraftAsync(
                CampaignId!,
                new RunsiteDraftSaveRequest(
                    _campaign.ActiveRunId,
                    _runsiteRevision,
                    idempotencyKey,
                    title,
                    summary,
                    playerSections,
                    gmNotes));
            if (!receipt.Applied)
            {
                _runsiteDraftMutationKey = null;
                _runsiteRevision = receipt.Revision;
                _errorMessage = receipt.Message ?? "Revision conflict. Reload the Runsite draft before saving again.";
                return;
            }

            _runsiteDraftMutationKey = null;
            _runsiteRevision = receipt.Revision;
            _statusMessage = "Runsite draft saved. Players still see only the published page.";
            await LoadCampaignAsync(resetEditors: false);
        }, onTerminalFailure: () => _runsiteDraftMutationKey = null);
    }

    protected async Task PublishRunsiteAsync()
    {
        if (!IsGameMaster || string.IsNullOrWhiteSpace(_campaign?.ActiveRunId))
        {
            _errorMessage = "Only the GM can publish a campaign Runsite.";
            return;
        }

        string idempotencyKey = AcquireMutationKey(
            ref _runsitePublishMutationKey,
            "runsite-publish",
            new
            {
                CampaignId,
                RunId = _campaign.ActiveRunId,
                ExpectedRevision = _runsiteRevision
            });

        await RunMutationAsync(async () =>
        {
            CampaignMutationReceipt receipt = await CampaignClient.PublishRunsiteAsync(
                CampaignId!,
                new RunsitePublishRequest(
                    _campaign.ActiveRunId,
                    _runsiteRevision,
                    idempotencyKey));
            if (!receipt.Applied)
            {
                _runsitePublishMutationKey = null;
                _runsiteRevision = receipt.Revision;
                _errorMessage = receipt.Message ?? "Revision conflict. Reload the Runsite before publishing.";
                return;
            }

            _runsitePublishMutationKey = null;
            _runsiteRevision = receipt.Revision;
            _statusMessage = "Runsite published.";
            await LoadCampaignAsync(resetEditors: true);
        }, onTerminalFailure: () => _runsitePublishMutationKey = null);
    }

    protected async Task CreateCampaignAsync()
    {
        string name = _newCampaignName.Trim();
        string? summary = NullIfWhiteSpace(_newCampaignSummary);
        string? runTitle = NullIfWhiteSpace(_newCampaignRunTitle);
        if (name.Length is < 1 or > 160
            || summary?.Length > 4000
            || runTitle?.Length > 160)
        {
            _errorMessage = "Provide a campaign name and keep campaign details within their stated limits.";
            return;
        }

        string idempotencyKey = AcquireMutationKey(
            ref _campaignCreateMutationKey,
            "campaign-create",
            new
            {
                Name = name,
                Summary = summary,
                Visibility = "private",
                InitialRunTitle = runTitle
            });

        await RunMutationAsync(async () =>
        {
            CampaignWorkspaceProjection created = await CampaignClient.CreateCampaignAsync(
                new CampaignCreateRequest(
                    name,
                    idempotencyKey,
                    summary,
                    "private",
                    runTitle));
            if (string.IsNullOrWhiteSpace(created.CampaignId)
                || !CampaignViewerRoles.IsGameMaster(created.ViewerRole)
                || !created.CanManage)
            {
                throw new InvalidOperationException("Campaign creation authority was not confirmed.");
            }

            _campaignCreateMutationKey = null;
            BeginRouteTransition(created.CampaignId, inviteId: null);
            _routeInitializationPending = false;
            _campaign = created;
            _isLoading = false;
            _newCampaignName = string.Empty;
            _newCampaignSummary = string.Empty;
            _newCampaignRunTitle = string.Empty;
            _statusMessage = "Campaign created. You can now issue a one-time link or join code.";
            Navigation.NavigateTo(
                $"/account/campaigns/{Uri.EscapeDataString(created.CampaignId)}",
                replace: true);
        }, onTerminalFailure: () => _campaignCreateMutationKey = null);
    }

    protected async Task AcceptJoinCodeAsync()
    {
        CampaignEligibleCharacterProjection? selected = _eligibleCharacters.FirstOrDefault(character =>
            string.Equals(
                character.DossierId,
                _selectedEligibleDossierId,
                StringComparison.OrdinalIgnoreCase));
        string code = _joinCode.Trim();
        if (selected is null || code.Length is < 1 or > 64)
        {
            _errorMessage = "Enter the GM-issued join code and choose one of your existing characters.";
            return;
        }

        _joinIdempotencyKey ??= $"join-code-{Guid.NewGuid():N}";
        await RunMutationAsync(async () =>
        {
            CampaignJoinReceipt receipt = await CampaignClient.JoinCampaignByCodeAsync(
                new CampaignJoinCodeRequest(
                    code,
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
                throw new InvalidOperationException("Campaign join authority was not confirmed.");
            }

            string acceptedCampaignId = receipt.CampaignId;
            BeginRouteTransition(acceptedCampaignId, inviteId: null);
            _routeInitializationPending = false;
            _inviteMessage = receipt.AlreadyJoined
                ? "Campaign membership confirmed."
                : "Campaign join code accepted.";
            await LoadCampaignAsync(resetEditors: true);
            _isLoading = false;
            Navigation.NavigateTo(
                $"/account/campaigns/{Uri.EscapeDataString(acceptedCampaignId)}",
                replace: true);
        });
        _joinCode = string.Empty;
    }

    protected async Task IssueCampaignInviteAsync()
    {
        if (!IsGameMaster || string.IsNullOrWhiteSpace(CampaignId))
        {
            _errorMessage = "Only the GM can issue campaign invites.";
            return;
        }

        if (_inviteExpiresInMinutes is < 5 or > 43200
            || _inviteMaxUses is < 1 or > 100)
        {
            _errorMessage = "Invite expiry must be 5–43,200 minutes and uses must be 1–100.";
            return;
        }

        ClearIssuedInvite();
        string idempotencyKey = AcquireMutationKey(
            ref _campaignInviteMutationKey,
            "campaign-invite",
            new
            {
                CampaignId,
                ExpiresInMinutes = _inviteExpiresInMinutes,
                MaxUses = _inviteMaxUses
            });

        await RunMutationAsync(async () =>
        {
            CampaignInviteSecretProjection issued = await CampaignClient.CreateCampaignInviteAsync(
                CampaignId,
                new CampaignInviteCreateRequest(
                    idempotencyKey,
                    _inviteExpiresInMinutes,
                    _inviteMaxUses));
            if (!string.Equals(issued.CampaignId, CampaignId, StringComparison.Ordinal)
                || !issued.JoinPath.StartsWith("/join/campaign/", StringComparison.Ordinal)
                || issued.JoinPath.Contains('?')
                || issued.JoinPath.Count(static character => character == '#') != 1)
            {
                throw new InvalidOperationException("Campaign invite authority was not confirmed.");
            }

            _campaignInviteMutationKey = null;
            Uri origin = new(Navigation.BaseUri, UriKind.Absolute);
            _issuedInvite = issued;
            _issuedInviteAbsoluteLink = $"{origin.GetLeftPart(UriPartial.Authority)}{issued.JoinPath}";
            _statusMessage = "Invite issued. Copy the link or code now; clear it when the handoff is complete.";
        }, onTerminalFailure: () => _campaignInviteMutationKey = null);
    }

    protected void ClearIssuedInvite()
    {
        _issuedInvite = null;
        _issuedInviteAbsoluteLink = null;
    }

    private async Task PrepareInviteFragmentAsync(
        long routeVersion,
        string? campaignId,
        string? inviteId,
        CancellationToken cancellationToken)
    {
        CampaignInviteFragmentHandoff? handoff;
        try
        {
            handoff = await JsRuntime.InvokeAsync<CampaignInviteFragmentHandoff>(
                "chummerCampaignJoin.readInviteFragment",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            if (IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
            {
                _errorMessage = "The invite handoff could not be inspected. Close this tab and request a new invite.";
                await TryScrubInviteLocationAsync(
                    routeVersion: routeVersion,
                    campaignId: campaignId,
                    inviteId: inviteId,
                    cancellationToken: cancellationToken);
            }

            return;
        }

        if (!IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
        {
            if (handoff is not null)
            {
                handoff.Secret = null;
            }

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
            await TryScrubInviteLocationAsync(
                routeVersion: routeVersion,
                campaignId: campaignId,
                inviteId: inviteId,
                cancellationToken: cancellationToken);
            return;
        }

        if (!handoff.HasUsableFragmentSecret
            || handoff.Secret!.Length > MaximumInviteSecretLength)
        {
            handoff.Secret = null;
            _errorMessage = "The campaign invite fragment is invalid. Request a new invite from the GM.";
            await TryScrubInviteLocationAsync(
                routeVersion: routeVersion,
                campaignId: campaignId,
                inviteId: inviteId,
                cancellationToken: cancellationToken);
            return;
        }

        _pendingInviteSecret = handoff.Secret;
        handoff.Secret = null;
        try
        {
            IReadOnlyList<CampaignEligibleCharacterProjection> eligibleCharacters =
                await CampaignClient.GetEligibleCharactersAsync(cancellationToken);
            if (!IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
            {
                return;
            }

            _eligibleCharacters = eligibleCharacters;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            if (IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
            {
                _errorMessage = "Existing characters could not be loaded. The invite was cleared; request a new link before trying again.";
                await ClearPendingInviteAsync(
                    routeVersion,
                    campaignId,
                    inviteId,
                    cancellationToken);
            }

            return;
        }

        if (_eligibleCharacters.Count == 0)
        {
            _errorMessage = "No eligible existing character is available. Create or restore your character, then request a new invite.";
            await ClearPendingInviteAsync(
                routeVersion,
                campaignId,
                inviteId,
                cancellationToken);
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

        BeginRouteTransition(acceptedCampaignId, inviteId: null);
        _routeInitializationPending = false;
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

    private async Task ClearPendingInviteAsync(
        long? routeVersion = null,
        string? campaignId = null,
        string? inviteId = null,
        CancellationToken cancellationToken = default)
    {
        _pendingInviteSecret = null;
        _eligibleCharacters = [];
        _selectedEligibleDossierId = null;
        _grantGmEditAuthority = false;
        _joinIdempotencyKey = null;
        await TryScrubInviteLocationAsync(
            routeVersion: routeVersion,
            campaignId: campaignId,
            inviteId: inviteId,
            cancellationToken: cancellationToken);
    }

    private async Task RejectUnexpectedInviteSecretAsync(
        long routeVersion,
        string? campaignId,
        string? inviteId,
        CancellationToken cancellationToken)
    {
        CampaignInviteFragmentHandoff? handoff;
        try
        {
            handoff = await JsRuntime.InvokeAsync<CampaignInviteFragmentHandoff>(
                "chummerCampaignJoin.readInviteFragment",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            return;
        }

        if (!IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
        {
            if (handoff is not null)
            {
                handoff.Secret = null;
            }

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
        await TryScrubInviteLocationAsync(
            routeVersion: routeVersion,
            campaignId: campaignId,
            inviteId: inviteId,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> TryScrubInviteLocationAsync(
        string? safePath = null,
        long? routeVersion = null,
        string? campaignId = null,
        string? inviteId = null,
        CancellationToken cancellationToken = default)
    {
        if (routeVersion.HasValue
            && !IsCurrentRoute(
                routeVersion.Value,
                campaignId,
                inviteId,
                cancellationToken))
        {
            return false;
        }

        try
        {
            await JsRuntime.InvokeVoidAsync(
                "chummerCampaignJoin.scrubInviteLocation",
                cancellationToken,
                safePath);
            return !routeVersion.HasValue
                || IsCurrentRoute(
                    routeVersion.Value,
                    campaignId,
                    inviteId,
                    cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            if (!routeVersion.HasValue
                || IsCurrentRoute(
                    routeVersion.Value,
                    campaignId,
                    inviteId,
                    cancellationToken))
            {
                _errorMessage = "Browser history cleanup failed. Close this tab before continuing.";
            }

            return false;
        }
    }

    private Task LoadCampaignAsync(bool resetEditors)
    {
        string? campaignId = CampaignId;
        CancellationToken cancellationToken = _routeCancellation?.Token ?? CancellationToken.None;
        return string.IsNullOrWhiteSpace(campaignId)
            ? Task.CompletedTask
            : LoadCampaignAsync(
                campaignId,
                resetEditors,
                _routeVersion,
                _observedInviteId,
                cancellationToken);
    }

    private async Task LoadCampaignAsync(
        string campaignId,
        bool resetEditors,
        long routeVersion,
        string? inviteId,
        CancellationToken cancellationToken)
    {
        try
        {
            CampaignWorkspaceProjection campaign = await CampaignClient.GetCampaignAsync(
                campaignId,
                cancellationToken);
            if (!IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
            {
                return;
            }

            if (!string.Equals(campaign.CampaignId, campaignId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Campaign authority mismatch.");
            }

            _campaign = campaign;
            if (IsGameMaster)
            {
                if (_issuedInvite is not null
                    && !string.Equals(
                        _issuedInvite.CampaignId,
                        campaign.CampaignId,
                        StringComparison.Ordinal))
                {
                    ClearIssuedInvite();
                }

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
                ClearIssuedInvite();
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            if (IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
            {
                ClearIssuedInvite();
                _campaign = null;
                _errorMessage ??= "Campaign workspace is unavailable for this account.";
            }
        }
    }

    private async Task LoadCampaignIndexAsync(
        long routeVersion,
        string? campaignId,
        string? inviteId,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<CampaignListItemProjection> campaigns =
                await CampaignClient.ListCampaignsAsync(cancellationToken);
            IReadOnlyList<CampaignEligibleCharacterProjection> eligibleCharacters =
                await CampaignClient.GetEligibleCharactersAsync(cancellationToken);
            if (!IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
            {
                return;
            }

            _campaigns = campaigns;
            _eligibleCharacters = eligibleCharacters;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            if (IsCurrentRoute(routeVersion, campaignId, inviteId, cancellationToken))
            {
                _campaigns = [];
                _eligibleCharacters = [];
                _errorMessage ??= "Campaigns and existing characters are unavailable for this account.";
            }
        }
    }

    private async Task RunMutationAsync(
        Func<Task> mutation,
        Action? onTerminalFailure = null)
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
            onTerminalFailure?.Invoke();
            _errorMessage = "Revision conflict. The current campaign state has been reloaded.";
            await LoadCampaignAsync(resetEditors: true);
        }
        catch (CampaignCollaborationException exception)
            when (IsTerminalMutationFailure(exception.StatusCode))
        {
            onTerminalFailure?.Invoke();
            _errorMessage = "The campaign change was rejected. Review the current values before trying again.";
        }
        catch
        {
            _errorMessage = "The campaign change outcome could not be confirmed. Retry the unchanged request; Chummer will reuse the same operation key.";
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

    private void BeginRouteTransition(string? campaignId, string? inviteId)
    {
        CampaignId = campaignId;
        InviteId = inviteId;
        _hasObservedRoute = true;
        _observedCampaignId = campaignId;
        _observedInviteId = inviteId;
        _routeVersion++;
        _routeCancellation?.Cancel();
        _routeCancellation?.Dispose();
        _routeCancellation = new CancellationTokenSource();
        ResetRouteScopedState();
    }

    private void ResetRouteScopedState()
    {
        _campaign = null;
        _campaigns = [];
        _eligibleCharacters = [];
        _selectedEligibleDossierId = null;
        _grantGmEditAuthority = false;
        _joinCode = string.Empty;
        _newCampaignName = string.Empty;
        _newCampaignSummary = string.Empty;
        _newCampaignRunTitle = string.Empty;
        ClearIssuedInvite();
        _editingDossierId = null;
        _editingRunnerHandle = string.Empty;
        _editingDisplayName = string.Empty;
        _editingStatus = string.Empty;
        _characterEditReason = string.Empty;
        _editingCharacterRevision = 0;
        _editingSections = [];
        _authorityDossierId = null;
        _authorityGrant = false;
        _authorityBindingRevision = 0;
        _authorityReason = string.Empty;
        _runsiteTitle = string.Empty;
        _runsiteSummary = string.Empty;
        _runsiteGmNotes = string.Empty;
        _runsiteSections = [];
        _runsiteRevision = 0;
        _pendingInviteSecret = null;
        _joinIdempotencyKey = null;
        _characterEditIdempotencyKey = null;
        _authorityIdempotencyKey = null;
        _campaignCreateMutationKey = null;
        _campaignInviteMutationKey = null;
        _runsiteDraftMutationKey = null;
        _runsitePublishMutationKey = null;
        _inviteMessage = null;
        _statusMessage = null;
        _errorMessage = null;
        _isLoading = true;
    }

    private bool IsCurrentRoute(
        long routeVersion,
        string? campaignId,
        string? inviteId,
        CancellationToken cancellationToken)
        => !_disposed
            && !cancellationToken.IsCancellationRequested
            && routeVersion == _routeVersion
            && string.Equals(campaignId, _observedCampaignId, StringComparison.Ordinal)
            && string.Equals(inviteId, _observedInviteId, StringComparison.Ordinal);

    private static bool TryNormalizeReason(string value, out string reason)
    {
        reason = (value ?? string.Empty).Trim();
        return reason.Length >= MinimumReasonLength && reason.Length <= 500;
    }

    private static string? NullIfWhiteSpace(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string AcquireMutationKey(
        ref PendingMutationKey? pending,
        string prefix,
        object normalizedPayload)
    {
        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(
            normalizedPayload,
            IdempotencyPayloadJsonOptions);
        string payloadSha256 = Convert.ToHexString(SHA256.HashData(payloadBytes));
        if (pending is null
            || !string.Equals(pending.PayloadSha256, payloadSha256, StringComparison.Ordinal))
        {
            pending = new PendingMutationKey(
                payloadSha256,
                $"{prefix}-{Guid.NewGuid():N}");
        }

        return pending.IdempotencyKey;
    }

    private static bool IsTerminalMutationFailure(int statusCode)
        => statusCode is >= 400 and <= 499
            && statusCode is not 408 and not 425 and not 429;

    private sealed record PendingMutationKey(
        string PayloadSha256,
        string IdempotencyKey);

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
