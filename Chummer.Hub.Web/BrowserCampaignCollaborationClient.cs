using System.Text.Json;
using Microsoft.JSInterop;

namespace Chummer.Hub.Web;

public sealed class BrowserCampaignCollaborationClient : ICampaignCollaborationClient
{
    private const string CampaignApiRoot = "/api/v1/campaigns";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IJSRuntime _jsRuntime;

    public BrowserCampaignCollaborationClient(IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        _jsRuntime = jsRuntime;
    }

    public async Task<IReadOnlyList<CampaignListItemProjection>> ListCampaignsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CampaignApiProjection> campaigns =
            await SendAsync<IReadOnlyList<CampaignApiProjection>>(
                CampaignApiRoot,
                "GET",
                cancellationToken).ConfigureAwait(false);
        return campaigns
            .Select(campaign => new CampaignListItemProjection(
                campaign.CampaignId,
                campaign.Name,
                campaign.Summary,
                campaign.Role,
                campaign.CanManage,
                campaign.Roster?.Count ?? 0,
                campaign.UpdatedAtUtc))
            .ToArray();
    }

    public async Task<CampaignWorkspaceProjection> CreateCampaignAsync(
        CampaignCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CampaignApiProjection created = await SendAsync<CampaignCreateRequest, CampaignApiProjection>(
            CampaignApiRoot,
            "POST",
            request,
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(created.CampaignId))
        {
            throw new CampaignCollaborationException(0);
        }

        return await GetCampaignAsync(created.CampaignId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CampaignInviteSecretProjection> CreateCampaignInviteAsync(
        string campaignId,
        CampaignInviteCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string normalizedCampaignId = NormalizeIdentifier(campaignId, nameof(campaignId));
        CampaignInviteSecretApiProjection issued =
            await SendAsync<CampaignInviteCreateRequest, CampaignInviteSecretApiProjection>(
                $"{CampaignPath(normalizedCampaignId)}/invites",
                "POST",
                request,
                cancellationToken).ConfigureAwait(false);
        return ValidateInviteHandoff(normalizedCampaignId, issued);
    }

    public async Task<IReadOnlyList<CampaignEligibleCharacterProjection>> GetEligibleCharactersAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EligibleCharacterApiProjection> characters =
            await SendAsync<IReadOnlyList<EligibleCharacterApiProjection>>(
                $"{CampaignApiRoot}/eligible-characters",
                "GET",
                cancellationToken).ConfigureAwait(false);
        return characters
            .Select(character => new CampaignEligibleCharacterProjection(
                character.DossierId,
                character.AuthorityKind,
                character.AuthoritativeCharacterId,
                character.RunnerHandle,
                character.DisplayName,
                character.Status,
                character.CurrentRevision,
                character.UpdatedAtUtc))
            .ToArray();
    }

    public async Task<CampaignWorkspaceProjection> GetCampaignAsync(
        string campaignId,
        CancellationToken cancellationToken = default)
    {
        string campaignPath = CampaignPath(campaignId);
        CampaignApiProjection campaign = await SendAsync<CampaignApiProjection>(
            campaignPath,
            "GET",
            cancellationToken).ConfigureAwait(false);
        string normalizedCampaignId = NormalizeIdentifier(campaignId, nameof(campaignId));
        if (!string.Equals(campaign.CampaignId, normalizedCampaignId, StringComparison.Ordinal))
        {
            throw new CampaignCollaborationException(0);
        }

        IReadOnlyList<CampaignEligibleCharacterProjection> eligibleCharacters =
            await GetEligibleCharactersAsync(cancellationToken).ConfigureAwait(false);
        HashSet<string> ownedDossierIds = eligibleCharacters
            .Select(character => character.DossierId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        CampaignRosterMemberProjection[] roster = await Task.WhenAll(
            (campaign.Roster ?? [])
                .Select(async member =>
                {
                    PlayerSafeSheetApiProjection sheet = await SendAsync<PlayerSafeSheetApiProjection>(
                        $"{campaignPath}/sheets/{EscapeIdentifier(member.DossierId, nameof(member.DossierId))}",
                        "GET",
                        cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(sheet.DossierId, member.DossierId, StringComparison.Ordinal))
                    {
                        throw new CampaignCollaborationException(0);
                    }

                    return new CampaignRosterMemberProjection(
                        member.DossierId,
                        member.DisplayName,
                        member.Role,
                        member.AuthorityKind,
                        member.AuthoritativeCharacterId,
                        member.GmEditAuthorityGranted,
                        member.GmAuthorityBindingRevision,
                        ownedDossierIds.Contains(member.DossierId),
                        MapSheet(sheet, ownedDossierIds.Contains(member.DossierId)));
                })).ConfigureAwait(false);

        string? runId = campaign.RunIds?.FirstOrDefault();
        PublishedRunsiteApiProjection? published = null;
        RunsiteDraftApiProjection? draft = null;
        if (!string.IsNullOrWhiteSpace(runId))
        {
            string runsitePath = $"{campaignPath}/runs/{EscapeIdentifier(runId, nameof(runId))}/runsite";
            published = await TrySendAsync<PublishedRunsiteApiProjection>(
                runsitePath,
                "GET",
                cancellationToken).ConfigureAwait(false);
            if (campaign.CanManage && CampaignViewerRoles.IsGameMaster(campaign.Role))
            {
                draft = await TrySendAsync<RunsiteDraftApiProjection>(
                    $"{runsitePath}/draft",
                    "GET",
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return new CampaignWorkspaceProjection(
            campaign.CampaignId,
            campaign.Name,
            campaign.Summary,
            campaign.Role,
            campaign.CanManage,
            runId,
            roster,
            new CampaignRunsiteProjection(
                runId,
                draft?.Revision ?? published?.Revision ?? 0,
                published is null ? null : MapPublishedRunsite(published),
                draft is null ? null : MapRunsiteDraft(draft)));
    }

    public async Task<CampaignJoinReceipt> JoinCampaignAsync(
        string inviteId,
        CampaignJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CampaignInviteRedemptionApiProjection receipt = await SendAsync<CampaignJoinRequest, CampaignInviteRedemptionApiProjection>(
            $"{CampaignApiRoot}/invites/{EscapeIdentifier(inviteId, nameof(inviteId))}/redeem",
            "POST",
            request,
            cancellationToken).ConfigureAwait(false);
        return MapJoinReceipt(receipt);
    }

    public async Task<CampaignJoinReceipt> JoinCampaignByCodeAsync(
        CampaignJoinCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CampaignInviteRedemptionApiProjection receipt =
            await SendAsync<CampaignJoinCodeRequest, CampaignInviteRedemptionApiProjection>(
                $"{CampaignApiRoot}/join-code/redeem",
                "POST",
                request,
                cancellationToken).ConfigureAwait(false);
        return MapJoinReceipt(receipt);
    }

    private static CampaignJoinReceipt MapJoinReceipt(
        CampaignInviteRedemptionApiProjection receipt)
        => new(
            Joined: true,
            receipt.CampaignId,
            receipt.DossierId,
            receipt.Role,
            receipt.AlreadyJoined,
            receipt.Binding.BindingRevision,
            receipt.Binding.CurrentRevision,
            string.Equals(
                receipt.Binding.GmAuthorityRole,
                "gm_character_editor",
                StringComparison.Ordinal));

    public async Task<CampaignMutationReceipt> UpdatePlayerSafeSheetAsync(
        string campaignId,
        string dossierId,
        CampaignCharacterEditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SharedSheetEditApiReceipt receipt = await SendAsync<CampaignCharacterEditRequest, SharedSheetEditApiReceipt>(
            $"{CampaignPath(campaignId)}/sheets/{EscapeIdentifier(dossierId, nameof(dossierId))}",
            "PUT",
            request,
            cancellationToken).ConfigureAwait(false);
        return new CampaignMutationReceipt(true, receipt.Revision);
    }

    public async Task<CampaignGmAuthorityReceipt> UpdateGmEditAuthorityAsync(
        string campaignId,
        string dossierId,
        CampaignGmAuthorityUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        GmAuthorityApiReceipt receipt = await SendAsync<CampaignGmAuthorityUpdateRequest, GmAuthorityApiReceipt>(
            $"{CampaignPath(campaignId)}/sheets/{EscapeIdentifier(dossierId, nameof(dossierId))}/gm-authority",
            "PUT",
            request,
            cancellationToken).ConfigureAwait(false);
        return new CampaignGmAuthorityReceipt(
            Applied: true,
            receipt.BindingRevision,
            receipt.CurrentCharacterRevision,
            receipt.GmEditAuthorityGranted,
            receipt.Changed);
    }

    public async Task<CampaignMutationReceipt> SaveRunsiteDraftAsync(
        string campaignId,
        RunsiteDraftSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payload = new RunsiteDraftApiRequest(
            request.ExpectedRevision,
            request.IdempotencyKey,
            request.Title,
            request.Summary,
            request.PlayerSections,
            request.GmNotes);
        RunsiteDraftApiProjection draft = await SendAsync<RunsiteDraftApiRequest, RunsiteDraftApiProjection>(
            $"{CampaignPath(campaignId)}/runs/{EscapeIdentifier(request.RunId, nameof(request.RunId))}/runsite/draft",
            "PUT",
            payload,
            cancellationToken).ConfigureAwait(false);
        return new CampaignMutationReceipt(true, draft.Revision);
    }

    public async Task<CampaignMutationReceipt> PublishRunsiteAsync(
        string campaignId,
        RunsitePublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payload = new PublishRunsiteApiRequest(
            request.ExpectedRevision,
            request.IdempotencyKey);
        PublishedRunsiteApiProjection published = await SendAsync<PublishRunsiteApiRequest, PublishedRunsiteApiProjection>(
            $"{CampaignPath(campaignId)}/runs/{EscapeIdentifier(request.RunId, nameof(request.RunId))}/runsite/publish",
            "POST",
            payload,
            cancellationToken).ConfigureAwait(false);
        return new CampaignMutationReceipt(true, published.Revision);
    }

    private static string CampaignPath(string campaignId)
        => $"{CampaignApiRoot}/{EscapeIdentifier(campaignId, nameof(campaignId))}";

    private static string NormalizeIdentifier(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 128)
        {
            throw new ArgumentException("A bounded identifier is required.", parameterName);
        }

        return normalized;
    }

    private static string EscapeIdentifier(string value, string parameterName)
    {
        return Uri.EscapeDataString(NormalizeIdentifier(value, parameterName));
    }

    private static CampaignInviteSecretProjection ValidateInviteHandoff(
        string expectedCampaignId,
        CampaignInviteSecretApiProjection issued)
    {
        string inviteId = NormalizeIdentifier(issued.InviteId, nameof(issued.InviteId));
        if (!string.Equals(issued.CampaignId, expectedCampaignId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(issued.LinkSecret)
            || issued.LinkSecret.Length > 256
            || string.IsNullOrWhiteSpace(issued.ShortCode)
            || issued.ShortCode.Length > 64
            || issued.ExpiresAtUtc == default
            || issued.MaxUses is < 1 or > 100)
        {
            throw new CampaignCollaborationException(0);
        }

        string canonicalJoinPath = $"/join/campaign/{Uri.EscapeDataString(inviteId)}#secret={Uri.EscapeDataString(issued.LinkSecret)}";
        if (!string.Equals(issued.JoinPath, canonicalJoinPath, StringComparison.Ordinal))
        {
            throw new CampaignCollaborationException(0);
        }

        return new CampaignInviteSecretProjection(
            inviteId,
            expectedCampaignId,
            canonicalJoinPath,
            issued.LinkSecret,
            issued.ShortCode,
            issued.ExpiresAtUtc,
            issued.MaxUses);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        string path,
        string method,
        CancellationToken cancellationToken)
    {
        BrowserEnvelope envelope = await SendEnvelopeAsync(
            path,
            method,
            body: null,
            cancellationToken).ConfigureAwait(false);
        return DeserializeSuccess<TResponse>(envelope);
    }

    private async Task<TResponse?> TrySendAsync<TResponse>(
        string path,
        string method,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        BrowserEnvelope envelope = await SendEnvelopeAsync(
            path,
            method,
            body: null,
            cancellationToken).ConfigureAwait(false);
        return envelope.Status == 404
            ? null
            : DeserializeSuccess<TResponse>(envelope);
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        string path,
        string method,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        string body = JsonSerializer.Serialize(payload, JsonOptions);
        BrowserEnvelope envelope = await SendEnvelopeAsync(
            path,
            method,
            body,
            cancellationToken).ConfigureAwait(false);
        return DeserializeSuccess<TResponse>(envelope);
    }

    private async Task<BrowserEnvelope> SendEnvelopeAsync(
        string path,
        string method,
        string? body,
        CancellationToken cancellationToken)
    {
        string envelopeText = body is null
            ? await _jsRuntime.InvokeAsync<string>(
                "chummerHubApi.send",
                cancellationToken,
                path,
                method).ConfigureAwait(false)
            : await _jsRuntime.InvokeAsync<string>(
                "chummerHubApi.send",
                cancellationToken,
                path,
                method,
                body).ConfigureAwait(false);
        return JsonSerializer.Deserialize<BrowserEnvelope>(envelopeText, JsonOptions)
            ?? throw new CampaignCollaborationException(0);
    }

    private static TResponse DeserializeSuccess<TResponse>(BrowserEnvelope envelope)
    {
        if (envelope.Status < 200 || envelope.Status >= 300)
        {
            throw new CampaignCollaborationException(envelope.Status);
        }

        return JsonSerializer.Deserialize<TResponse>(envelope.Text ?? string.Empty, JsonOptions)
            ?? throw new CampaignCollaborationException(envelope.Status);
    }

    private static PlayerSafeCharacterSheetProjection MapSheet(
        PlayerSafeSheetApiProjection sheet,
        bool isOwnedByViewer)
    {
        PublicationSafeSectionApiProjection[] sections = (sheet.Sections ?? []).ToArray();
        if (sections.Any(static section => section is null
                || !string.Equals(section.Audience, "campaign", StringComparison.Ordinal)
                || !string.Equals(section.PublicationState, "player_safe", StringComparison.Ordinal)
                || section.Discoverable
                || section.ArtifactId is not null
                || section.OwnershipSummary is not null
                || section.TrustBand is not null
                || section.PublicationSummary is not null
                || section.CreatorPublicationId is not null
                || section.NextSafeAction is not null
                || section.ProvenanceSummary is not null
                || section.AuditSummary is not null
                || section.CompatibilitySummary is not null
                || section.LineageSummary is not null))
        {
            throw new CampaignCollaborationException(0);
        }

        return new PlayerSafeCharacterSheetProjection(
            sheet.DossierId,
            sheet.RunnerHandle,
            sheet.DisplayName,
            sheet.Status,
            sheet.Role,
            sheet.CanManage,
            sheet.GmEditAuthorityGranted,
            sheet.GmAuthorityBindingRevision,
            isOwnedByViewer,
            sheet.Revision,
            sheet.RuleEnvironmentFingerprint,
            sections
                .Select(section => new CampaignPublicationSafeSectionProjection(
                    section.ProjectionId,
                    section.Kind,
                    section.Label,
                    section.Summary,
                    Audience: "campaign",
                    PublicationState: "player_safe"))
                .ToArray());
    }

    private static PublishedRunsiteProjection MapPublishedRunsite(PublishedRunsiteApiProjection runsite)
        => new(
            runsite.Title,
            runsite.Summary,
            (runsite.Sections ?? [])
                .Select(section => new RunsitePlayerSectionProjection(section.Heading, section.Body))
                .ToArray(),
            runsite.Revision,
            runsite.PublishedAtUtc);

    private static RunsiteDraftProjection MapRunsiteDraft(RunsiteDraftApiProjection runsite)
        => new(
            runsite.Title,
            runsite.Summary,
            (runsite.PlayerSections ?? [])
                .Select(section => new RunsitePlayerSectionProjection(section.Heading, section.Body))
                .ToArray(),
            runsite.GmNotes,
            runsite.Revision,
            runsite.UpdatedAtUtc);

    private sealed record BrowserEnvelope(int Status, string? Text);

    private sealed record CampaignApiProjection(
        string CampaignId,
        string Name,
        string Summary,
        string Role,
        bool CanManage,
        IReadOnlyList<string>? RunIds,
        IReadOnlyList<CampaignRosterApiProjection>? Roster,
        DateTimeOffset UpdatedAtUtc = default);

    private sealed record CampaignInviteSecretApiProjection(
        string InviteId,
        string CampaignId,
        string JoinPath,
        string LinkSecret,
        string ShortCode,
        DateTimeOffset ExpiresAtUtc,
        int MaxUses);

    private sealed record CampaignRosterApiProjection(
        string DossierId,
        string AuthorityKind,
        string AuthoritativeCharacterId,
        string DisplayName,
        string Role,
        bool GmEditAuthorityGranted,
        long GmAuthorityBindingRevision);

    private sealed record PlayerSafeSheetApiProjection(
        string DossierId,
        string RunnerHandle,
        string DisplayName,
        string Status,
        string Role,
        bool CanManage,
        bool GmEditAuthorityGranted,
        long GmAuthorityBindingRevision,
        long Revision,
        string RuleEnvironmentFingerprint,
        IReadOnlyList<PublicationSafeSectionApiProjection>? Sections);

    private sealed record PublicationSafeSectionApiProjection(
        string ProjectionId,
        string Kind,
        string Label,
        string Summary,
        string? ArtifactId,
        string Audience,
        string? OwnershipSummary,
        string? PublicationState,
        string? TrustBand,
        bool Discoverable,
        string? PublicationSummary,
        string? CreatorPublicationId,
        string? NextSafeAction,
        string? ProvenanceSummary,
        string? AuditSummary,
        string? CompatibilitySummary,
        string? LineageSummary);

    private sealed record CampaignInviteRedemptionApiProjection(
        string CampaignId,
        string DossierId,
        string Role,
        CampaignCharacterBindingApiProjection Binding,
        bool AlreadyJoined);

    private sealed record CampaignCharacterBindingApiProjection(
        long BindingRevision,
        long CurrentRevision,
        string GmAuthorityRole);

    private sealed record EligibleCharacterApiProjection(
        string DossierId,
        string AuthorityKind,
        string AuthoritativeCharacterId,
        string RunnerHandle,
        string DisplayName,
        string Status,
        long CurrentRevision,
        DateTimeOffset UpdatedAtUtc);

    private sealed record SharedSheetEditApiReceipt(long Revision);

    private sealed record GmAuthorityApiReceipt(
        long BindingRevision,
        long CurrentCharacterRevision,
        bool GmEditAuthorityGranted,
        bool Changed);

    private sealed record RunsitePlayerSectionApiProjection(string Heading, string Body);

    private sealed record RunsiteDraftApiRequest(
        long ExpectedRevision,
        string IdempotencyKey,
        string Title,
        string Summary,
        IReadOnlyList<RunsitePlayerSectionProjection> PlayerSections,
        string? GmNotes);

    private sealed record PublishRunsiteApiRequest(
        long ExpectedRevision,
        string IdempotencyKey);

    private sealed record RunsiteDraftApiProjection(
        long Revision,
        string Title,
        string Summary,
        IReadOnlyList<RunsitePlayerSectionApiProjection>? PlayerSections,
        string? GmNotes,
        DateTimeOffset UpdatedAtUtc);

    private sealed record PublishedRunsiteApiProjection(
        long Revision,
        string Title,
        string Summary,
        IReadOnlyList<RunsitePlayerSectionApiProjection>? Sections,
        DateTimeOffset PublishedAtUtc);
}

public sealed class CampaignCollaborationException : InvalidOperationException
{
    public CampaignCollaborationException(int statusCode)
        : base(statusCode > 0
            ? $"Campaign collaboration request failed with status {statusCode}."
            : "Campaign collaboration response was invalid.")
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
