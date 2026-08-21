using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Infrastructure.Xml;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    public async Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyAttributeEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyOriginDossierEditAsync(OriginDossierEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyOriginDossierEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyCollectionMutationAsync(WorkspaceCollectionMutationRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                request,
                _characterSourceDataResolver),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyConditionMonitorEditAsync(ConditionMonitorEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyConditionMonitorEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task<CareerReputationEditorState?> PrepareCareerReputationEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved career runner before editing reputation." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for reputation editing." });
                return null;
            }

            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before reputation editing could begin." });
                return null;
            }

            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Reputation editing requires a native XML dossier." });
                return null;
            }

            CareerReputationEditorState editor = CareerReputationEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision,
                _characterSourceDataResolver);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyCareerReputationEditAsync(CareerReputationEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while reputation was open. Reopen Reputation before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyCareerReputationEdit(
                xml,
                request,
                _characterSourceDataResolver),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyBurnStreetCredAsync(BurnStreetCredRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while reputation was open. Reopen Reputation before burning Street Cred." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyBurnStreetCred(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task<SituationalModifiersEditorState?> PrepareSituationalModifiersEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved runner before editing situational modifiers." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for situational modifier editing." });
                return null;
            }

            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before situational modifier editing could begin." });
                return null;
            }

            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Situational modifier editing requires a native XML dossier." });
                return null;
            }

            SituationalModifiersEditorState editor = SituationalModifiersEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplySituationalModifiersEditAsync(SituationalModifiersEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while situational modifiers were open. Reopen them before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplySituationalModifiersEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task<PrimaryArmEditorState?> PreparePrimaryArmEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved runner before editing primary arm." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for primary-arm editing." });
                return null;
            }

            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before primary-arm editing could begin." });
                return null;
            }

            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Primary-arm editing requires a native XML dossier." });
                return null;
            }

            PrimaryArmEditorState editor = PrimaryArmEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyPrimaryArmEditAsync(PrimaryArmEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Primary Arm was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyPrimaryArmEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task<GroupMembershipEditorState?> PrepareGroupMembershipEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved runner before editing group membership." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for group-membership editing." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before group-membership editing could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Group-membership editing requires a native XML dossier." });
                return null;
            }

            GroupMembershipEditorState editor = GroupMembershipEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision,
                _characterSourceDataResolver);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyGroupMembershipEditAsync(GroupMembershipEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Group Membership was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyGroupMembershipEdit(
                xml,
                request,
                _characterSourceDataResolver),
            ct).ConfigureAwait(false);
    }

    public async Task<GroupNameEditorState?> PrepareGroupNameEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved runner before editing the group name." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for group-name editing." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before group-name editing could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Group-name editing requires a native XML dossier." });
                return null;
            }

            GroupNameEditorState editor = GroupNameEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyGroupNameEditAsync(GroupNameEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Group Name was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyGroupNameEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task<TraditionNameEditorState?> PrepareTraditionNameEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved runner before editing the tradition name." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for tradition-name editing." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before tradition-name editing could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Tradition-name editing requires a native XML dossier." });
                return null;
            }

            TraditionNameEditorState editor = TraditionNameEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyTraditionNameEditAsync(TraditionNameEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Tradition Name was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyTraditionNameEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task<TraditionDrainEditorState?> PrepareTraditionDrainEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved runner before editing tradition drain attributes." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for tradition-drain editing." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before tradition-drain editing could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Tradition-drain editing requires a native XML dossier." });
                return null;
            }

            TraditionDrainEditorState editor = TraditionDrainEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision,
                _characterSourceDataResolver);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyTraditionDrainEditAsync(TraditionDrainEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Tradition Drain was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyTraditionDrainEdit(
                xml,
                request,
                _characterSourceDataResolver),
            ct).ConfigureAwait(false);
    }

    public async Task<CareerEdgeUseEditorState?> PrepareCareerEdgeUseEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved career runner before editing Edge use." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for Edge-use editing." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before Edge-use editing could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Edge-use editing requires a native XML dossier." });
                return null;
            }

            CareerEdgeUseEditorState editor = CareerEdgeUseEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyCareerEdgeUseEditAsync(CareerEdgeUseEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Edge use was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyCareerEdgeUseEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task<CareerManualKarmaEditorState?> PrepareCareerManualKarmaEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved career runner before recording manual Karma." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for manual-Karma editing." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before manual-Karma editing could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Manual-Karma editing requires a native XML dossier." });
                return null;
            }

            CareerManualKarmaEditorState editor = CareerManualKarmaEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision,
                _characterSourceDataResolver);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyCareerManualKarmaEditAsync(CareerManualKarmaEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while manual Karma was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(
                xml,
                request,
                _characterSourceDataResolver),
            ct).ConfigureAwait(false);
    }

    public async Task<CareerManualNuyenEditorState?> PrepareCareerManualNuyenEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved career runner before recording manual Nuyen." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for manual-Nuyen editing." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before manual-Nuyen editing could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Manual-Nuyen editing requires a native XML dossier." });
                return null;
            }

            CareerManualNuyenEditorState editor = CareerManualNuyenEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision,
                _characterSourceDataResolver);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyCareerManualNuyenEditAsync(CareerManualNuyenEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while manual Nuyen was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyCareerManualNuyenEdit(
                xml,
                request,
                _characterSourceDataResolver),
            ct).ConfigureAwait(false);
    }

    public async Task<CareerNuyenExpenseEditorState?> PrepareCareerNuyenExpenseEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved career runner before editing Nuyen expenses." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for Nuyen-expense editing." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before Nuyen-expense editing could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Nuyen-expense editing requires a native XML dossier." });
                return null;
            }

            CareerNuyenExpenseEditorState editor = CareerNuyenExpenseEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyCareerNuyenExpenseEditAsync(CareerNuyenExpenseEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while a Nuyen expense was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyCareerNuyenExpenseEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task<SustainedObjectsEditorState?> PrepareSustainedObjectsEditAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved runner before editing sustained effects." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for sustained-effect editing." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before sustained-effect editing could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Sustained-effect editing requires a native XML dossier." });
                return null;
            }

            SustainedObjectsEditorState editor = SustainedObjectsEditorProjector.Project(
                read.Value.Document.Content,
                currentWorkspace.Value,
                expectedContentRevision);
            Publish(State with { Error = null });
            return editor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplySustainedObjectEditAsync(SustainedObjectEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while sustained effects were open. Reopen them before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplySustainedObjectEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyPsycheActiveEditAsync(PsycheActiveEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Psyche state was open. Reopen sustained effects before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyPsycheActiveEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyGearLocationAddAsync(GearLocationAddRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Add Gear Location was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyGearLocationAdd(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyWeaponLocationAddAsync(WeaponLocationAddRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Add Weapon Location was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyWeaponLocationAdd(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyVehicleLocationAddAsync(VehicleLocationAddRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Add Vehicle Location was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyVehicleHomeNodeEditAsync(VehicleHomeNodeEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Vehicle Home Node was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyVehicleHomeNodeEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyArmorHomeNodeEditAsync(ArmorHomeNodeEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Armor Home Node was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyArmorHomeNodeEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyWeaponHomeNodeEditAsync(WeaponHomeNodeEditRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Weapon Home Node was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyWeaponHomeNodeEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyWeaponActiveCommlinkEditAsync(
        WeaponActiveCommlinkEditRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Weapon Active Commlink was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyWeaponActiveCommlinkEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyArmorActiveCommlinkEditAsync(
        ArmorActiveCommlinkEditRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Armor Active Commlink was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyArmorActiveCommlinkEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyArmorDamageAdjustmentAsync(
        ArmorDamageAdjustmentRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Armor Condition was open. Reopen it before adjusting damage." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyArmorDamageAdjustment(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyArmorEquipmentEditAsync(
        ArmorEquipmentEditRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Armor Equipment was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyArmorEquipmentEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyWeaponAccessoryIncludedEditAsync(
        WeaponAccessoryIncludedEditRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Included in Weapon was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyWeaponAccessoryIncludedEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyCritterPowerCountEditAsync(
        CritterPowerCountEditRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Critter Power Count was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyCritterPowerCountEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplySpiritFetteredEditAsync(
        SpiritFetteredEditRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Fettered/Pet was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplySpiritFetteredEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyGearQuantityEditAsync(
        GearQuantityEditRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Gear Quantity was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyGearQuantityEdit(
                xml,
                request,
                _characterSourceDataResolver),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyLifestyleIncrementEditAsync(
        LifestyleIncrementEditRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Lifestyle intervals were open. Reopen them before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyLifestyleIncrementEdit(xml, request),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyQualityLevelEditAsync(
        QualityLevelEditRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Quality Level was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyQualityLevelEdit(
                xml,
                request,
                _characterSourceDataResolver),
            ct).ConfigureAwait(false);
    }

    public async Task<CyberwareCommerceEditorState?> PrepareCyberwareCommerceEditAsync(
        Guid cyberwareId,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        long expectedContentRevision = State.ContentRevision;
        if (cyberwareId == Guid.Empty || currentWorkspace is null || expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Open a saved career runner and select stable Cyberware before commerce." });
            return null;
        }

        try
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await _client
                .GetWorkspaceAsync(currentWorkspace.Value, ct)
                .ConfigureAwait(false);
            if (!read.Success || read.Value is null)
            {
                Publish(State with { Error = read.Error ?? "Dossier could not be read for Cyberware commerce." });
                return null;
            }
            if (!string.Equals(read.Value.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal)
                || read.Value.ContentRevision != expectedContentRevision)
            {
                Publish(State with { Error = "The dossier changed before Cyberware commerce could begin." });
                return null;
            }
            if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
            {
                Publish(State with { Error = "Cyberware commerce requires a native XML dossier." });
                return null;
            }

            CharacterCyberwareSummary[] matches = new CharacterSectionService(_characterSourceDataResolver)
                .ParseCyberwares(read.Value.Document.Content)
                .Cyberwares
                .Where(candidate => Guid.TryParseExact(candidate.Guid, "D", out Guid parsed)
                    && parsed == cyberwareId)
                .ToArray();
            if (matches.Length != 1 || matches[0].CommerceSemantics is null)
            {
                Publish(State with { Error = "The selected stable Cyberware commerce state is unavailable." });
                return null;
            }

            Publish(State with { Error = null });
            return new CyberwareCommerceEditorState(
                currentWorkspace.Value,
                expectedContentRevision,
                cyberwareId,
                matches[0].Name,
                matches[0].CommerceSemantics!);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(State with { Error = exception.Message });
            return null;
        }
    }

    public async Task ApplyCyberwareCommerceEditAsync(
        CyberwareCommerceRequest request,
        CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Cyberware Commerce was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyCyberwareCommerceEdit(
                xml,
                request,
                _characterSourceDataResolver),
            ct).ConfigureAwait(false);
    }

    public async Task ApplyLocationRenameAsync(LocationRenameRequest request, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        ArgumentNullException.ThrowIfNull(request);
        if (State.WorkspaceId != request.WorkspaceId
            || State.ContentRevision != request.ExpectedContentRevision)
        {
            Publish(State with { Error = "This runner changed while Rename Location was open. Reopen it before saving." });
            return;
        }

        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyLocationRename(xml, request),
            ct).ConfigureAwait(false);
    }

    private async Task ApplyQuickAddAsync(WorkspaceQuickAddRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ApplyWorkspaceXmlMutationAsync(
            xml => WorkspaceXmlMutationCatalog.ApplyQuickAdd(xml, request),
            ct);
    }

    private async Task ApplyWorkspaceXmlMutationAsync(Func<string, string> mutateXml, CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with { Error = "No dossier loaded." });
            return;
        }

        long expectedContentRevision = State.ContentRevision;
        if (expectedContentRevision <= 0)
        {
            Publish(State with { Error = "Dossier revision is unavailable. Reload before editing." });
            return;
        }

        string? returnTabId = State.ActiveTabId;
        string? returnActionId = State.ActiveActionId;
        string? returnSectionId = State.ActiveSectionId;
        WorkspaceDocument? committedDocument = null;
        IWorkspaceRecoveryCaptureIntent? postCommitCaptureIntent = null;
        WorkspaceOperationExecution<CommandResult<WorkspaceRevisionReceipt>> execution;
        try
        {
            if (HasAuthoritativeRecoveryLoader)
            {
                long anticipatedContentRevision = checked(expectedContentRevision + 1);
                _workspaceRecoveryPayloadStore.TryBeginCaptureIntent(
                    currentWorkspace.Value,
                    anticipatedContentRevision,
                    out postCommitCaptureIntent);
            }

            execution = await _workspaceOperationCoordinator.RunCurrentAsync(
                currentWorkspace.Value,
                async token =>
                {
                    CommandResult<WorkspaceDocumentSnapshot> read = await _client
                        .GetWorkspaceAsync(currentWorkspace.Value, token)
                        .ConfigureAwait(false);
                    if (!read.Success || read.Value is null)
                    {
                        return new CommandResult<WorkspaceRevisionReceipt>(
                            false,
                            null,
                            read.Error ?? "Dossier could not be read for editing.",
                            read.Outcome);
                    }

                    if (!string.Equals(
                            read.Value.Id.Value,
                            currentWorkspace.Value.Value,
                            StringComparison.Ordinal))
                    {
                        return new CommandResult<WorkspaceRevisionReceipt>(
                            false,
                            null,
                            "Dossier read returned a different workspace than the requested edit target.",
                            WorkspaceOperationOutcome.Corrupt);
                    }

                    if (read.Value.ContentRevision != expectedContentRevision)
                    {
                        return new CommandResult<WorkspaceRevisionReceipt>(
                            false,
                            null,
                            "The dossier changed before the edit could be applied.",
                            WorkspaceOperationOutcome.Conflict);
                    }

                    if (read.Value.Document.Format != WorkspaceDocumentFormat.NativeXml)
                    {
                        return new CommandResult<WorkspaceRevisionReceipt>(
                            false,
                            null,
                            "This edit requires a native XML dossier.",
                            WorkspaceOperationOutcome.Corrupt);
                    }

                    string mutatedXml = mutateXml(read.Value.Document.Content);
                    WorkspaceDocument replacement = read.Value.Document with
                    {
                        State = read.Value.Document.State with { Payload = mutatedXml }
                    };
                    committedDocument = replacement;
                    CommandResult<WorkspaceRevisionReceipt> replacementResult = await _client.ReplaceWorkspaceDocumentAsync(
                        currentWorkspace.Value,
                        expectedContentRevision,
                        replacement,
                        token).ConfigureAwait(false);
                    return replacementResult;
                },
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            postCommitCaptureIntent?.Dispose();
            Publish(State with { Error = ex.Message });
            return;
        }

        if (!execution.CanPublish)
        {
            if (execution.HasValue
                && execution.Value is { Success: true, Value: not null } staleResult
                && committedDocument is not null
                && HasAuthoritativeRecoveryLoader)
            {
                using var stalePostCommitBudget = new CancellationTokenSource(PostCommitRecoveryBudget);
                bool staleRecoveryCaptured = await TryCaptureRecoveryPayloadAsync(
                    currentWorkspace.Value,
                    staleResult.Value.ContentRevision,
                    stalePostCommitBudget.Token,
                    postCommitCaptureIntent,
                    committedDocument).ConfigureAwait(false);
                if (!staleRecoveryCaptured)
                {
                    GateStalePostCommitRecovery(
                        currentWorkspace.Value,
                        staleResult.Value.ContentRevision,
                        "stale postcommit XML recovery",
                        "The edit committed after its view was superseded, but exact recovery validation failed. Review this runner before closing it.");
                }
                postCommitCaptureIntent = null;
            }

            postCommitCaptureIntent?.Dispose();
            return;
        }

        CommandResult<WorkspaceRevisionReceipt> replaced = execution.Value;
        if (!replaced.Success || replaced.Value is null)
        {
            postCommitCaptureIntent?.Dispose();
            WorkspaceSessionState failedSession = replaced.Outcome == WorkspaceOperationOutcome.Conflict
                ? _workspaceSessionPresenter.SetConflictState(
                    currentWorkspace.Value,
                    new WorkspaceConflictState(
                        "XML edit",
                        expectedContentRevision,
                        ActualContentRevision: null,
                        replaced.Error ?? "The dossier changed before the edit could be applied."))
                : _workspaceSessionPresenter.State;
            Publish(State with
            {
                IsBusy = false,
                Error = replaced.Error,
                Notice = replaced.Outcome == WorkspaceOperationOutcome.Conflict
                    ? "Edit stopped because a newer dossier revision won. No overwrite or retry was attempted."
                    : State.Notice,
                Session = failedSession,
                OpenWorkspaces = failedSession.OpenWorkspaces
            });
            return;
        }

        if (committedDocument is null)
        {
            postCommitCaptureIntent?.Dispose();
            Publish(State with
            {
                IsBusy = false,
                Error = null,
                Notice = "The edit committed, but its exact postcommit document is unavailable. Keep this runner open for review."
            });
            return;
        }

        using var postCommitBudget = new CancellationTokenSource(PostCommitRecoveryBudget);
        bool recoveryCaptured = !HasAuthoritativeRecoveryLoader;
        if (!recoveryCaptured)
        {
            recoveryCaptured = await TryCaptureRecoveryPayloadAsync(
                currentWorkspace.Value,
                replaced.Value.ContentRevision,
                postCommitBudget.Token,
                postCommitCaptureIntent,
                committedDocument).ConfigureAwait(false);
            postCommitCaptureIntent = null;
        }

        WorkspaceSessionState session = _workspaceSessionPresenter.SetRevisions(
            currentWorkspace.Value,
            replaced.Value.ContentRevision,
            replaced.Value.SavedRevision);
        CharacterOverviewState revisionState = State with
        {
            Session = session,
            OpenWorkspaces = session.OpenWorkspaces,
            Error = null
        };
        bool viewCaptureFailed = false;
        try
        {
            _workspaceOverviewLifecycleCoordinator.CaptureCurrentWorkspaceView(revisionState);
        }
        catch
        {
            viewCaptureFailed = true;
        }
        WorkspaceOverviewLifecycleResult? reloaded = null;
        try
        {
            reloaded = await _workspaceOverviewLifecycleCoordinator.LoadAsync(
                revisionState,
                currentWorkspace.Value,
                postCommitBudget.Token);
        }
        catch
        {
            // The durable mutation and exact recovery capture are retained;
            // bounded overview projection failure is review-gated below.
        }

        if (!recoveryCaptured || reloaded is null || !reloaded.CanPublish)
        {
            WorkspaceSessionState reviewSession = _workspaceSessionPresenter.SetConflictState(
                currentWorkspace.Value,
                new WorkspaceConflictState(
                    "postcommit XML refresh",
                    replaced.Value.ContentRevision,
                    replaced.Value.ContentRevision,
                    recoveryCaptured
                        ? "The edit committed and its exact recovery copy is secured, but the refreshed view needs review."
                        : "The edit committed, but exact recovery validation needs review before this runner can close."));
            PublishPostCommitState(revisionState with
            {
                IsBusy = false,
                Error = null,
                Notice = recoveryCaptured
                    ? "Edit committed. Exact postcommit recovery is secured; keep this runner open while the refreshed view is reviewed."
                    : "Edit committed, but exact postcommit recovery is review-gated. Keep this runner open.",
                Session = reviewSession,
                OpenWorkspaces = reviewSession.OpenWorkspaces
            });
            return;
        }

        PublishPostCommitState(reloaded.State);
        if (viewCaptureFailed)
        {
            PublishPostCommitWarning(
                "The edit committed, but the local workspace view could not be retained; it will refresh on the next interaction.");
        }

        if (!string.IsNullOrWhiteSpace(returnSectionId)
            && !string.Equals(returnSectionId, "summary", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(returnSectionId, "validate", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await LoadSectionAsync(
                    returnSectionId,
                    returnTabId,
                    returnActionId,
                    postCommitBudget.Token);
            }
            catch
            {
                WorkspaceSessionState reviewSession = _workspaceSessionPresenter.SetConflictState(
                    currentWorkspace.Value,
                    new WorkspaceConflictState(
                        "postcommit section refresh",
                        replaced.Value.ContentRevision,
                        replaced.Value.ContentRevision,
                        "The edit and exact recovery copy committed, but the requested section needs review."));
                PublishPostCommitState(State with
                {
                    IsBusy = false,
                    Error = null,
                    Notice = "Edit committed and exact recovery is secured, but the requested section refresh is review-gated.",
                    Session = reviewSession,
                    OpenWorkspaces = reviewSession.OpenWorkspaces
                });
            }
        }
    }
}
