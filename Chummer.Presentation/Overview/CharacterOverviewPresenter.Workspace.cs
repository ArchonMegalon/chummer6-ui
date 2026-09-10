using Chummer.Contracts.Presentation;
using Chummer.Application.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Shell;
using System.Text.RegularExpressions;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private static readonly Regex GameEditionRegex = new(
        @"<gameedition>\s*([^<]+?)\s*</gameedition>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        long displayGeneration = BeginDisplayTransition();
        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceImportDocument resolvedDocument = await ResolveImportDocumentAsync(document, ct);
            WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.ImportAsync(State, resolvedDocument, ct);
            if (!result.CanPublish)
            {
                return;
            }

            if (!IsDisplayGenerationCurrent(displayGeneration)) return;
            CaptureRecoveryPayload(result);
            if (!TryPublishDisplayTransition(displayGeneration, result.State)) return;
            await RefreshNavigationContextForCurrentWorkspaceAsync(ct);
            await EnsureDefaultWorkspaceSurfaceAsync(ct);
            await SyncShellWorkspaceContextAsync(ct);
        }
        catch (Exception ex)
        {
            TryPublishDisplayTransition(displayGeneration, State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    private async Task<WorkspaceImportDocument> ResolveImportDocumentAsync(WorkspaceImportDocument document, CancellationToken ct)
    {
        IReadOnlyList<OpenWorkspaceState> openWorkspaces = State.OpenWorkspaces ?? [];
        IReadOnlyList<AppCommandDefinition> commands = State.Commands ?? [];
        IReadOnlyList<NavigationTabDefinition> navigationTabs = State.NavigationTabs ?? [];
        string? explicitRulesetId = RulesetDefaults.NormalizeOptional(document.RulesetId);
        if (explicitRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, explicitRulesetId, document.Format);

        string? detectedRulesetId = TryDetectImportRulesetId(document);
        if (detectedRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, detectedRulesetId, document.Format);

        CharacterWorkspaceId? activeWorkspaceId = State.WorkspaceId;
        if (activeWorkspaceId is not null)
        {
            OpenWorkspaceState? activeWorkspace = openWorkspaces.FirstOrDefault(
                workspace => string.Equals(workspace.Id.Value, activeWorkspaceId.Value.Value, StringComparison.Ordinal));
            string? activeWorkspaceRulesetId = RulesetDefaults.NormalizeOptional(activeWorkspace?.RulesetId);
            if (activeWorkspaceRulesetId is not null)
                return new WorkspaceImportDocument(document.Content, activeWorkspaceRulesetId, document.Format);
        }

        string? commandRulesetId = RulesetDefaults.NormalizeOptional(commands.FirstOrDefault()?.RulesetId);
        if (commandRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, commandRulesetId, document.Format);

        string? tabRulesetId = RulesetDefaults.NormalizeOptional(navigationTabs.FirstOrDefault()?.RulesetId);
        if (tabRulesetId is not null)
            return new WorkspaceImportDocument(document.Content, tabRulesetId, document.Format);

        ShellBootstrapData bootstrap = TryCreateBootstrapFromShellState(out ShellBootstrapData shellBootstrap)
            ? shellBootstrap
            : await _bootstrapDataProvider.GetAsync(ct);
        string? bootstrapRulesetId = RulesetDefaults.NormalizeOptional(bootstrap.PreferredRulesetId)
            ?? RulesetDefaults.NormalizeOptional(bootstrap.ActiveRulesetId)
            ?? RulesetDefaults.NormalizeOptional(bootstrap.RulesetId);
        if (bootstrapRulesetId is null)
            throw new InvalidOperationException("Workspace ruleset is required.");

        return new WorkspaceImportDocument(document.Content, bootstrapRulesetId, document.Format);
    }

    private static string? TryDetectImportRulesetId(WorkspaceImportDocument document)
    {
        if (document.Format != WorkspaceDocumentFormat.NativeXml)
            return null;

        Match match = GameEditionRegex.Match(document.Content);
        if (!match.Success)
            return null;

        string edition = match.Groups[1].Value.Trim();
        if (edition.Equals("SR5", StringComparison.OrdinalIgnoreCase)
            || edition.Equals("Shadowrun 5", StringComparison.OrdinalIgnoreCase))
        {
            return RulesetDefaults.Sr5;
        }

        if (edition.Equals("SR6", StringComparison.OrdinalIgnoreCase)
            || edition.Equals("Shadowrun 6", StringComparison.OrdinalIgnoreCase))
        {
            return RulesetDefaults.Sr6;
        }

        return RulesetDefaults.NormalizeOptional(edition);
    }

    private CharacterWorkspaceId? ResolveCurrentWorkspaceId()
    {
        return _workspaceOverviewLifecycleCoordinator.CurrentWorkspaceId ?? State.WorkspaceId;
    }

    private async Task EnsureNavigationContextAsync(CancellationToken ct)
    {
        CharacterWorkspaceId? expectedWorkspace = ResolveCurrentWorkspaceId();
        IReadOnlyList<AppCommandDefinition> commands = State.Commands ?? [];
        IReadOnlyList<NavigationTabDefinition> navigationTabs = State.NavigationTabs ?? [];
        if (commands.Count > 0 && navigationTabs.Count > 0)
        {
            return;
        }

        string? rulesetId = ResolveCurrentWorkspaceId() is { } currentWorkspace
            ? ResolveWorkspaceRulesetId(currentWorkspace)
            : null;
        ShellBootstrapData bootstrap = TryCreateBootstrapFromShellState(out ShellBootstrapData shellBootstrap)
            ? shellBootstrap
            : await _bootstrapDataProvider.GetAsync(rulesetId, ct);
        if (!IsWorkspaceContextCurrent(expectedWorkspace))
        {
            return;
        }

        bootstrap = NormalizeBootstrapData(bootstrap, rulesetId);
        Publish(State with
        {
            Error = null,
            Commands = bootstrap.Commands ?? commands,
            NavigationTabs = bootstrap.NavigationTabs ?? navigationTabs
        });
    }

    private async Task RefreshNavigationContextForCurrentWorkspaceAsync(CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspaceId = ResolveCurrentWorkspaceId();
        if (currentWorkspaceId is null)
        {
            return;
        }

        string? rulesetId = ResolveWorkspaceRulesetId(currentWorkspaceId.Value);
        if (string.IsNullOrWhiteSpace(rulesetId))
        {
            return;
        }

        string? commandRulesetId = State.Commands
            .Select(command => RulesetDefaults.NormalizeOptional(command.RulesetId))
            .FirstOrDefault(candidate => candidate is not null);
        string? tabRulesetId = State.NavigationTabs
            .Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId))
            .FirstOrDefault(candidate => candidate is not null);
        bool needsRefresh = State.Commands.Count == 0
            || State.NavigationTabs.Count == 0
            || !string.Equals(commandRulesetId, rulesetId, StringComparison.Ordinal)
            || !string.Equals(tabRulesetId, rulesetId, StringComparison.Ordinal);
        if (!needsRefresh)
        {
            return;
        }

        ShellBootstrapData bootstrap = await _bootstrapDataProvider.GetAsync(rulesetId, ct);
        if (!IsWorkspaceContextCurrent(currentWorkspaceId))
        {
            return;
        }

        bootstrap = NormalizeBootstrapData(bootstrap, rulesetId);
        Publish(State with
        {
            Error = null,
            Commands = bootstrap.Commands,
            NavigationTabs = bootstrap.NavigationTabs
        });
    }

    private async Task EnsureDefaultWorkspaceSurfaceAsync(CancellationToken ct)
    {
        CharacterWorkspaceId? expectedWorkspace = ResolveCurrentWorkspaceId();
        if (expectedWorkspace is null || !string.IsNullOrWhiteSpace(State.ActiveSectionId))
        {
            return;
        }

        await EnsureNavigationContextAsync(ct);
        if (!IsWorkspaceContextCurrent(expectedWorkspace))
        {
            return;
        }

        IReadOnlyList<NavigationTabDefinition> navigationTabs = State.NavigationTabs ?? [];
        string? defaultTabId = !string.IsNullOrWhiteSpace(State.ActiveTabId)
            ? State.ActiveTabId
            : ResolveDefaultWorkspaceTabId(navigationTabs, State.LastCommandId);
        if (string.IsNullOrWhiteSpace(defaultTabId))
        {
            return;
        }

        await SelectTabAsync(defaultTabId, ct);
    }

    private static string? ResolveDefaultWorkspaceTabId(
        IReadOnlyList<NavigationTabDefinition> navigationTabs,
        string? lastCommandId)
    {
        if (IsNewWorkspaceCommand(lastCommandId))
        {
            string[] visibleNewWorkspaceTabPreference =
            [
                "tab-info",
                "tab-attributes",
                "tab-skills",
                "tab-gear",
                "tab-qualities"
            ];
            foreach (string preferredTabId in visibleNewWorkspaceTabPreference)
            {
                string? matchingTabId = navigationTabs
                    .FirstOrDefault(tab => tab.EnabledByDefault && string.Equals(tab.Id, preferredTabId, StringComparison.Ordinal))
                    ?.Id;
                if (!string.IsNullOrWhiteSpace(matchingTabId))
                {
                    return matchingTabId;
                }
            }

            return navigationTabs
                .FirstOrDefault(tab => tab.EnabledByDefault
                    && !string.Equals(tab.SectionId, "build-lab", StringComparison.Ordinal))?.Id
                ?? navigationTabs.FirstOrDefault(tab => tab.EnabledByDefault)?.Id;
        }

        return navigationTabs
            .FirstOrDefault(tab => tab.EnabledByDefault && string.Equals(tab.Id, "tab-info", StringComparison.Ordinal))
            ?.Id
            ?? navigationTabs.FirstOrDefault(tab => tab.EnabledByDefault)?.Id;
    }

    private static bool IsNewWorkspaceCommand(string? commandId)
        => string.Equals(commandId, "new_character", StringComparison.Ordinal)
            || string.Equals(commandId, "new_critter", StringComparison.Ordinal);

    public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct)
        => LoadWorkspaceForOwnerAsync(id, null, ct);

    Task IOwnerBoundWorkspaceRefreshPresenter.LoadAsync(
        OwnerContextStamp expectedOwner, CharacterWorkspaceId id, CancellationToken ct)
        => expectedOwner.IsValid
            ? LoadWorkspaceForOwnerAsync(id, expectedOwner, ct)
            : throw new InvalidOperationException("Original owner authority is required for workspace refresh.");

    private async Task LoadWorkspaceForOwnerAsync(CharacterWorkspaceId id, OwnerContextStamp? expectedOwner, CancellationToken ct)
    {
        if (expectedOwner is { } original
            && (State.DisplayOwnerContext != original
                || _client is not IOwnerBoundWorkspaceProjectionClient boundClient
                || boundClient.CaptureOwnerContext() != original))
            return;
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        long displayGeneration = BeginDisplayTransition();
        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceOverviewLifecycleResult result = expectedOwner is { } owner
                ? _workspaceOverviewLifecycleCoordinator is IOwnerBoundWorkspaceOverviewLifecycleCoordinator bound
                    ? await bound.LoadAsync(State, owner, id, ct)
                    : throw new InvalidOperationException("Owner-bound workspace refresh is unavailable.")
                : await _workspaceOverviewLifecycleCoordinator.LoadAsync(State, id, ct);
            if (!result.CanPublish)
            {
                return;
            }

            if (expectedOwner is { } retained
                && (result.State.DisplayOwnerContext != retained
                    || _client is not IOwnerBoundWorkspaceProjectionClient current
                    || current.CaptureOwnerContext() != retained)) return;
            if (!IsDisplayGenerationCurrent(displayGeneration)) return;
            CaptureRecoveryPayload(result);
            if (!TryPublishDisplayTransition(displayGeneration, result.State)) return;
            await RefreshNavigationContextForCurrentWorkspaceAsync(ct);
            await EnsureDefaultWorkspaceSurfaceAsync(ct);
            await SyncShellWorkspaceContextAsync(ct);
        }
        catch (Exception ex)
        {
            TryPublishDisplayTransition(displayGeneration, State with
            {
                IsBusy = false,
                Error = ex.Message,
                DisplayOwnerContext = null
            });
        }
    }

    public async Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        long displayGeneration = BeginDisplayTransition();
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.SwitchAsync(State, id, ct);
        if (!result.CanPublish)
        {
            return;
        }

        if (!IsDisplayGenerationCurrent(displayGeneration)) return;
        CaptureRecoveryPayload(result);
        if (!TryPublishDisplayTransition(displayGeneration, result.State)) return;
        await RefreshNavigationContextForCurrentWorkspaceAsync(ct);
        await SyncShellWorkspaceContextAsync(ct);
    }

    public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
        => CloseWorkspaceCoreAsync(State, id, ct);

    public Task CloseWorkspaceAsync(OwnerContextStamp originalOwner, CharacterWorkspaceId id,
        long expectedContentRevision, CancellationToken ct)
    {
        CharacterOverviewState originalState = State;
        if (originalState.DisplayOwnerContext != originalOwner
            || !IsOriginalPersistenceOwnerCurrent(originalOwner)
            || originalState.Session.FindWorkspace(id) is not { } closing
            || closing.ContentRevision != expectedContentRevision)
            return Task.CompletedTask;
        return CloseWorkspaceCoreAsync(originalState, id, ct);
    }

    private async Task CloseWorkspaceCoreAsync(CharacterOverviewState originalState, CharacterWorkspaceId id, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        long displayGeneration = BeginDisplayTransition();
        if (!IsOriginalPersistenceOwnerCurrent(originalState.DisplayOwnerContext))
        {
            AbandonOriginalPersistenceView(displayGeneration, originalState, committed: false);
            return;
        }
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.CloseAsync(originalState, id, ct);
        if (!result.CanPublish)
        {
            return;
        }

        if (result.PostCommit)
        {
            try { CaptureRecoveryPayload(result); } catch { }
            if (!PublishPostCommitState(result.State, displayGeneration)) return;
            using var postCommitBudget = new CancellationTokenSource(PostCommitShellSyncBudget);
            try
            {
                await SyncShellWorkspaceContextAsync(postCommitBudget.Token);
            }
            catch
            {
                PublishPostCommitWarning("Shell synchronization will retry later.");
            }
        }
        else
        {
            if (!IsDisplayGenerationCurrent(displayGeneration)) return;
            CaptureRecoveryPayload(result);
            if (!TryPublishDisplayTransition(displayGeneration, result.State)) return;
            await SyncShellWorkspaceContextAsync(ct);
        }
    }

    public async Task DeleteWorkspaceAsync(CharacterWorkspaceId id, bool confirmed, CancellationToken ct)
        => _ = await DeleteWorkspaceCoreAsync(State, id, confirmed, ct);

    public Task<CommandResult<WorkspaceRevisionReceipt>> DeleteWorkspaceAsync(
        OwnerContextStamp originalOwner, CharacterWorkspaceId id, long expectedContentRevision,
        bool confirmed, CancellationToken ct)
    {
        CharacterOverviewState original = State;
        if (original.DisplayOwnerContext != originalOwner
            || !IsOriginalPersistenceOwnerCurrent(originalOwner)
            || original.Session.FindWorkspace(id) is not { } deleting
            || deleting.ContentRevision != expectedContentRevision)
            return Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(false, null,
                "The original deletion context changed. Reopen the runner before deleting it.",
                WorkspaceOperationOutcome.Conflict));
        return DeleteWorkspaceCoreAsync(original, id, confirmed, ct);
    }

    private async Task<CommandResult<WorkspaceRevisionReceipt>> DeleteWorkspaceCoreAsync(
        CharacterOverviewState original, CharacterWorkspaceId id, bool confirmed, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        long displayGeneration = BeginDisplayTransition();
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.DeleteAsync(
            original,
            id,
            confirmed,
            ct);
        CommandResult<WorkspaceRevisionReceipt> canonical = result.DeletionResult
            ?? new CommandResult<WorkspaceRevisionReceipt>(false, null,
                result.State.Error ?? "No confirmed deletion receipt was returned.", WorkspaceOperationOutcome.Conflict);
        if (!result.CanPublish)
        {
            return canonical;
        }

        if (result.PostCommit)
        {
            try { CaptureRecoveryPayload(result); } catch { }
            if (!PublishPostCommitState(result.State, displayGeneration)) return canonical;
            using var postCommitBudget = new CancellationTokenSource(PostCommitShellSyncBudget);
            try
            {
                await SyncShellWorkspaceContextAsync(postCommitBudget.Token);
            }
            catch
            {
                PublishPostCommitWarning("The deletion committed; shell synchronization will retry later.");
            }
        }
        else
        {
            if (!IsDisplayGenerationCurrent(displayGeneration)) return canonical;
            CaptureRecoveryPayload(result);
            if (!TryPublishDisplayTransition(displayGeneration, result.State, original)) return canonical;
            await SyncShellWorkspaceContextAsync(ct);
        }
        return canonical;
    }

    private bool PublishPostCommitState(CharacterOverviewState state, long? displayGeneration = null)
    {
        CharacterOverviewState committedState = state with { Error = null };
        try
        {
            if (displayGeneration is { } generation)
                return TryPublishDisplayTransition(generation, committedState);
            Publish(committedState);
            return true;
        }
        catch
        {
            // Assignment precedes notifications. Do not reassert an old state:
            // a notification may have started a newer view before throwing.
            return ReferenceEquals(State, committedState);
        }
    }

    private void PublishPostCommitWarning(string warning)
    {
        string separator = string.IsNullOrWhiteSpace(State.Notice) ? string.Empty : " ";
        CharacterOverviewState warningState = State with
        {
            Error = null,
            Notice = $"{State.Notice}{separator}{warning}"
        };
        try
        {
            Publish(warningState);
        }
        catch
        {
            // A failed observer cannot replace a newer display with this warning.
        }
    }

    private void TryCapturePostCommitWorkspaceView(string warning)
    {
        try
        {
            _workspaceOverviewLifecycleCoordinator.CaptureCurrentWorkspaceView(State);
        }
        catch
        {
            // A view-store projection is an observer of the durable commit.
            // It can request a later refresh, but it cannot turn success into
            // an operation failure.
            PublishPostCommitWarning(warning);
        }
    }

    private void GateStalePostCommitRecovery(
        OwnerContextStamp? originalOwner,
        CharacterWorkspaceId workspaceId,
        long committedRevision,
        string operation,
        string message)
    {
        try
        {
            SetOriginalSessionConflict(
                originalOwner,
                workspaceId,
                new WorkspaceConflictState(
                    operation,
                    committedRevision,
                    committedRevision,
                    message));
        }
        catch
        {
            // The stale operation must never replace the winning UI. Recovery
            // capture failure remains recorded by the vault when validation
            // reached its commit boundary.
        }

        try
        {
            RecoveryPayloads(originalOwner).SetProtected(
                workspaceId,
                committedRevision,
                protectedFromEviction: true);
        }
        catch
        {
            // Best-effort protection cannot justify publishing stale state.
        }
    }

    private async Task CloseAllWorkspacesAsync(CancellationToken ct, string notice)
    {
        long displayGeneration = BeginDisplayTransition();
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.CloseAllAsync(State, ct, notice);
        if (!result.CanPublish)
        {
            return;
        }

        if (result.PostCommit)
        {
            try { CaptureRecoveryPayload(result); } catch { }
            if (!PublishPostCommitState(result.State, displayGeneration)) return;
            using var postCommitBudget = new CancellationTokenSource(PostCommitShellSyncBudget);
            try
            {
                await SyncShellWorkspaceContextAsync(postCommitBudget.Token);
            }
            catch
            {
                PublishPostCommitWarning("Shell synchronization will retry later.");
            }
        }
        else
        {
            if (!IsDisplayGenerationCurrent(displayGeneration)) return;
            CaptureRecoveryPayload(result);
            if (!TryPublishDisplayTransition(displayGeneration, result.State)) return;
            await SyncShellWorkspaceContextAsync(ct);
        }
    }

    private Task SyncShellWorkspaceContextAsync(CancellationToken ct)
    {
        if (_shellPresenter is null)
        {
            return Task.CompletedTask;
        }

        return ShellWorkspaceContextSynchronization.SynchronizeAsync(_shellPresenter, _client, State, ct);
    }

    private CharacterOverviewState CreateWorkspaceResetState(string commandId, string notice)
    {
        BeginDisplayTransition();
        return _workspaceOverviewLifecycleCoordinator.CreateResetState(State, commandId, notice).State;
    }

    private bool IsWorkspaceContextCurrent(CharacterWorkspaceId? expectedWorkspace)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (expectedWorkspace is null || currentWorkspace is null)
        {
            return expectedWorkspace is null && currentWorkspace is null;
        }

        return string.Equals(expectedWorkspace.Value.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
            && _workspaceOperationCoordinator.IsCurrent(expectedWorkspace.Value);
    }

    private void CaptureRecoveryPayload(
        WorkspaceOverviewLifecycleResult result,
        IWorkspaceRecoveryCaptureIntent? advertisedIntent = null)
    {
        if (!result.CanPublish
            || result.RecoveryDocument is null
            || result.RecoveryValidation is null
            || result.CurrentWorkspaceId is not { } workspaceId
            || result.State.ContentRevision <= 0)
        {
            return;
        }

        OwnerContextStamp? originalOwner = result.State.DisplayOwnerContext;
        bool ownsIntent = advertisedIntent is null;
        IWorkspaceRecoveryCaptureIntent? captureIntent = advertisedIntent;
        if (captureIntent is null
            && !RecoveryPayloads(originalOwner).TryBeginCaptureIntent(
                workspaceId,
                result.State.ContentRevision,
                out captureIntent))
        {
            return;
        }

        try
        {
            TryCommitRecoveryCapture(
                captureIntent!,
                workspaceId,
                result.State.ContentRevision,
                result.RecoveryDocument,
                result.RecoveryValidation,
                protectFromEviction: result.State.IsDirty || result.State.ConflictState is not null);
        }
        finally
        {
            if (ownsIntent)
                captureIntent?.Dispose();
        }
    }

    private IWorkspaceRecoveryPayloadStore RecoveryPayloads(OwnerContextStamp? originalOwner)
    {
        if (originalOwner is { IsValid: true } original)
            return _workspaceRecoveryPayloadStore.ForOwner(original);
        if (originalOwner is not null || _client is IOwnerBoundWorkspaceMutationClient)
            throw new InvalidOperationException("Original recovery owner authority is required.");
        return _workspaceRecoveryPayloadStore;
    }

    private bool HasAuthoritativeRecoveryLoader
        => _workspaceOverviewLoader is IAuthoritativeWorkspaceOverviewLoader
        {
            IsCompositionBound: true
        };

    private async Task<bool> TryCaptureRecoveryPayloadAsync(
        CharacterWorkspaceId workspaceId,
        long expectedContentRevision,
        CancellationToken ct,
        IWorkspaceRecoveryCaptureIntent? advertisedIntent = null,
        WorkspaceDocument? expectedDocument = null,
        OwnerContextStamp? originalOwner = null)
    {
        IWorkspaceRecoveryCaptureIntent? captureIntent = advertisedIntent;
        if (captureIntent is null
            && !RecoveryPayloads(originalOwner).TryBeginCaptureIntent(
                workspaceId,
                expectedContentRevision,
                out captureIntent))
        {
            return false;
        }

        using (captureIntent)
        {
            try
            {
                if (_workspaceOverviewLoader is not IAuthoritativeWorkspaceOverviewLoader
                    {
                        IsCompositionBound: true
                    } authoritativeLoader)
                {
                    return false;
                }

                WorkspaceRecoveryAuthoritySnapshot loaded = await (originalOwner is { } original
                    ? authoritativeLoader.LoadRecoverySnapshotAsync(original, workspaceId, ct)
                    : authoritativeLoader.LoadRecoverySnapshotAsync(workspaceId, ct)).ConfigureAwait(false);
                if (loaded.OriginalOwner != originalOwner || loaded.ContentRevision != expectedContentRevision)
                {
                    return false;
                }

                if (expectedDocument is not null
                    && !RecoveryDocumentsMatch(loaded.Document, expectedDocument))
                {
                    return false;
                }

                return TryCommitRecoveryCapture(
                    captureIntent!,
                    workspaceId,
                    expectedContentRevision,
                    loaded.Document,
                    loaded.Validation,
                    protectFromEviction: true);
            }
            catch (OperationCanceledException)
            {
                // The presenter-owned postcommit budget expired. Recovery stays
                // unavailable rather than reconstructing a lossy payload.
                return false;
            }
            catch
            {
                // Mutation success is durable even if exact recovery capture is
                // unavailable. Never substitute a lossy reconstructed payload.
                return false;
            }
        }
    }

    private static bool RecoveryDocumentsMatch(WorkspaceDocument left, WorkspaceDocument right)
        => left.Format == right.Format
            && string.Equals(left.RulesetId, right.RulesetId, StringComparison.Ordinal)
            && left.SchemaVersion == right.SchemaVersion
            && string.Equals(left.PayloadKind, right.PayloadKind, StringComparison.Ordinal)
            && string.Equals(left.Content, right.Content, StringComparison.Ordinal);

    private bool TryCommitRecoveryCapture(
        IWorkspaceRecoveryCaptureIntent captureIntent,
        CharacterWorkspaceId workspaceId,
        long sourceRevision,
        WorkspaceDocument document,
        WorkspaceOverviewLoader.CanonicalValidationCapability validationCapability,
        bool protectFromEviction)
    {
        try
        {
            if (_workspaceRecoveryPayloadStore is not IWorkspaceRecoveryCaptureStore captureStore)
                return false;

            WorkspaceRecoveryCaptureResult captured = captureStore.Capture(
                captureIntent,
                document,
                validationCapability,
                protectFromEviction);
            return captured.Success && captured.SourceRevision == sourceRevision;
        }
        catch
        {
            return false;
        }
    }
}
