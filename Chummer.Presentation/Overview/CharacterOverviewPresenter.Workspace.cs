using Chummer.Contracts.Presentation;
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

            CaptureRecoveryPayload(result);
            Publish(result.State);
            await RefreshNavigationContextForCurrentWorkspaceAsync(ct);
            await EnsureDefaultWorkspaceSurfaceAsync(ct);
            await SyncShellWorkspaceContextAsync(ct);
        }
        catch (Exception ex)
        {
            Publish(State with
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

    public async Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.LoadAsync(State, id, ct);
            if (!result.CanPublish)
            {
                return;
            }

            CaptureRecoveryPayload(result);
            Publish(result.State);
            await RefreshNavigationContextForCurrentWorkspaceAsync(ct);
            await EnsureDefaultWorkspaceSurfaceAsync(ct);
            await SyncShellWorkspaceContextAsync(ct);
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    public async Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.SwitchAsync(State, id, ct);
        if (!result.CanPublish)
        {
            return;
        }

        CaptureRecoveryPayload(result);
        Publish(result.State);
        await RefreshNavigationContextForCurrentWorkspaceAsync(ct);
        await SyncShellWorkspaceContextAsync(ct);
    }

    public async Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.CloseAsync(State, id, ct);
        if (!result.CanPublish)
        {
            return;
        }

        if (result.PostCommit)
        {
            try { CaptureRecoveryPayload(result); } catch { }
            PublishPostCommitState(result.State);
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
            CaptureRecoveryPayload(result);
            Publish(result.State);
            await SyncShellWorkspaceContextAsync(ct);
        }
    }

    public async Task DeleteWorkspaceAsync(CharacterWorkspaceId id, bool confirmed, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.DeleteAsync(
            State,
            id,
            confirmed,
            ct);
        if (!result.CanPublish)
        {
            return;
        }

        if (result.PostCommit)
        {
            try { CaptureRecoveryPayload(result); } catch { }
            PublishPostCommitState(result.State);
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
            CaptureRecoveryPayload(result);
            Publish(result.State);
            await SyncShellWorkspaceContextAsync(ct);
        }
    }

    private void PublishPostCommitState(CharacterOverviewState state)
    {
        try
        {
            Publish(state with { Error = null });
        }
        catch
        {
            // Publish assigns State before invoking shell/subscriber callbacks.
            // Reassert committed success without letting observer failure escape.
            State = state with { Error = null };
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
            State = warningState;
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
        CharacterWorkspaceId workspaceId,
        long committedRevision,
        string operation,
        string message)
    {
        try
        {
            _workspaceSessionPresenter.SetConflictState(
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
            _workspaceRecoveryPayloadStore.SetProtected(
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
        WorkspaceOverviewLifecycleResult result = await _workspaceOverviewLifecycleCoordinator.CloseAllAsync(State, ct, notice);
        if (!result.CanPublish)
        {
            return;
        }

        if (result.PostCommit)
        {
            try { CaptureRecoveryPayload(result); } catch { }
            PublishPostCommitState(result.State);
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
            CaptureRecoveryPayload(result);
            Publish(result.State);
            await SyncShellWorkspaceContextAsync(ct);
        }
    }

    private Task SyncShellWorkspaceContextAsync(CancellationToken ct)
    {
        if (_shellPresenter is null)
        {
            return Task.CompletedTask;
        }

        CharacterWorkspaceId? activeWorkspaceId = ResolveCurrentWorkspaceId();
        return _shellPresenter.SyncWorkspaceContextAsync(activeWorkspaceId, ct);
    }

    private CharacterOverviewState CreateWorkspaceResetState(string commandId, string notice)
    {
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

        bool ownsIntent = advertisedIntent is null;
        IWorkspaceRecoveryCaptureIntent? captureIntent = advertisedIntent;
        if (captureIntent is null
            && !_workspaceRecoveryPayloadStore.TryBeginCaptureIntent(
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
        WorkspaceDocument? expectedDocument = null)
    {
        IWorkspaceRecoveryCaptureIntent? captureIntent = advertisedIntent;
        if (captureIntent is null
            && !_workspaceRecoveryPayloadStore.TryBeginCaptureIntent(
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

                WorkspaceRecoveryAuthoritySnapshot loaded = await authoritativeLoader
                    .LoadRecoverySnapshotAsync(workspaceId, ct)
                    .ConfigureAwait(false);
                if (loaded.ContentRevision != expectedContentRevision)
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
