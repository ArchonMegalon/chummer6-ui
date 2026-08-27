using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public enum Sr5TableWizardLane
{
    BeforeRun,
    Playtime
}

public enum Sr5TableWizardActionKind
{
    SpendEdge,
    RegainEdge,
    FireWeapon
}

public static class Sr5TableWizardSchemas
{
    public const string SnapshotV1 = "chummer.sr5_table_wizard.snapshot.v1";
    public const string CheckpointV1 = "chummer.sr5_table_wizard.checkpoint.v1";
}

public static class Sr5TableWizardActionIds
{
    public const string BeforeRunSpendEdge = "before-run.edge.spend";
    public const string BeforeRunRegainEdge = "before-run.edge.regain";
    public const string PlaytimeSpendEdge = "playtime.edge.spend";
    public const string PlaytimeRegainEdge = "playtime.edge.regain";
    public const string PlaytimeFireWeapon = "playtime.weapon.fire";
}

public static class Sr5TableWizardCheckpointInvalidationReasons
{
    public const string InvalidCheckpoint = "table-wizard-checkpoint-invalid";
    public const string LaneChanged = "table-wizard-checkpoint-lane-changed";
    public const string WorkspaceChanged = "table-wizard-checkpoint-workspace-changed";
    public const string WorkspaceRevisionChanged = "table-wizard-checkpoint-revision-changed";
    public const string SnapshotChanged = "table-wizard-checkpoint-snapshot-changed";
    public const string ActionUnavailable = "table-wizard-checkpoint-action-unavailable";
}

/// <summary>
/// Stable identity for one exact table action. Weapon actions bind the direct Weapon, active clip,
/// linked ammunition, saved Weapon revision, and firing mode. Edge actions bind the exact saved
/// Edge projection. The digest also binds the reviewed postcondition.
/// </summary>
public sealed record Sr5TableWizardActionIdentity(
    string ActionId,
    Sr5TableWizardLane Lane,
    Sr5TableWizardActionKind Kind,
    Guid WeaponId,
    int AmmoSlot,
    Guid AmmoGearId,
    string TargetRevision,
    CharacterWeaponFireMode? FireMode,
    string ActionDigest);

public sealed record Sr5TableWizardActionState(
    Sr5TableWizardActionIdentity Identity,
    string DisplayName,
    int EdgeUsedBefore,
    int EdgeUsedAfter,
    CharacterWeaponFirePlan? WeaponPlan);

public sealed record Sr5TableWizardSnapshot(
    string Schema,
    Sr5TableWizardLane Lane,
    CharacterWorkspaceId WorkspaceId,
    long WorkspaceRevision,
    CharacterCareerEdgeUseState Edge,
    IReadOnlyList<CareerWeaponFireEditorState> Weapons,
    IReadOnlyList<Sr5TableWizardActionState> Actions,
    string SnapshotDigest);

public sealed record Sr5TableWizardCheckpoint(
    string Schema,
    Sr5TableWizardLane Lane,
    string WorkspaceId,
    long WorkspaceRevision,
    string SnapshotDigest,
    Sr5TableWizardActionIdentity SelectedAction);

public sealed record Sr5TableWizardResume(bool Restored, string? InvalidationReason);

public sealed record Sr5TableWizardState(
    Sr5TableWizardSnapshot Snapshot,
    Sr5TableWizardActionState? SelectedAction,
    Sr5TableWizardResume Resume)
{
    public bool CanConfirm => SelectedAction is not null;
}

/// <summary>
/// Projects only the two table-safe leaves that already have exact Core and persistence
/// authority: one-point Edge use, and direct Career Weapon ammunition consumption.
/// </summary>
public static class Sr5TableWizardProjector
{
    private const int MaximumWeapons = 512;
    private const int MaximumActions = 4096;

    public static Sr5TableWizardSnapshot Project(
        Sr5TableWizardLane lane,
        CareerEdgeUseEditorState edge,
        CareerWeaponFireCatalogEditorState? weaponCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(edge);
        if (!Enum.IsDefined(lane)
            || string.IsNullOrWhiteSpace(edge.WorkspaceId.Value)
            || edge.ContentRevision <= 0
            || !IsCoherentEdge(edge.Edge))
        {
            throw new InvalidOperationException(
                "The SR5 table wizard requires an exact saved Career Edge projection.");
        }

        CareerWeaponFireEditorState[] weapons = lane switch
        {
            Sr5TableWizardLane.BeforeRun when weaponCatalog is null => [],
            Sr5TableWizardLane.BeforeRun when weaponCatalog.Weapons.Count == 0 => [],
            Sr5TableWizardLane.BeforeRun => throw new InvalidOperationException(
                "Before Run cannot receive Playtime Weapon authority."),
            Sr5TableWizardLane.Playtime when weaponCatalog is not null
                                               && weaponCatalog.WorkspaceId == edge.WorkspaceId
                                               && weaponCatalog.ContentRevision == edge.ContentRevision
                => ValidateWeapons(weaponCatalog.Weapons, edge.WorkspaceId, edge.ContentRevision),
            _ => throw new InvalidOperationException(
                "Playtime requires an exact current Career Weapon catalog, even when it is empty.")
        };

        var actions = new List<Sr5TableWizardActionState>();
        AddEdgeActions(actions, lane, edge.Edge);
        if (lane == Sr5TableWizardLane.Playtime)
            AddWeaponActions(actions, lane, weapons);
        if (actions.Count > MaximumActions)
            throw new InvalidOperationException("The SR5 table action catalog exceeds its bound.");

        string digest = ComputeSnapshotDigest(
            lane,
            edge.WorkspaceId,
            edge.ContentRevision,
            edge.Edge,
            weapons,
            actions);
        return new Sr5TableWizardSnapshot(
            Sr5TableWizardSchemas.SnapshotV1,
            lane,
            edge.WorkspaceId,
            edge.ContentRevision,
            edge.Edge,
            weapons,
            actions,
            digest);
    }

    internal static void ValidateSnapshot(Sr5TableWizardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(snapshot.Schema, Sr5TableWizardSchemas.SnapshotV1, StringComparison.Ordinal)
            || snapshot.Weapons is null
            || snapshot.Actions is null
            || !IsDigest(snapshot.SnapshotDigest))
        {
            throw new InvalidOperationException("The SR5 table wizard snapshot is invalid.");
        }

        var edge = new CareerEdgeUseEditorState(
            snapshot.WorkspaceId,
            snapshot.WorkspaceRevision,
            snapshot.Edge);
        CareerWeaponFireCatalogEditorState? catalog = snapshot.Lane == Sr5TableWizardLane.Playtime
            ? new CareerWeaponFireCatalogEditorState(
                snapshot.WorkspaceId,
                snapshot.WorkspaceRevision,
                snapshot.Weapons)
            : null;
        Sr5TableWizardSnapshot expected = Project(snapshot.Lane, edge, catalog);
        if (!string.Equals(snapshot.SnapshotDigest, expected.SnapshotDigest, StringComparison.Ordinal)
            || snapshot.Actions.Count != expected.Actions.Count
            || !snapshot.Actions.SequenceEqual(expected.Actions))
        {
            throw new InvalidOperationException(
                "The SR5 table wizard snapshot digest or typed action catalog is inconsistent.");
        }
    }

    internal static bool IsDigest(string? value)
        => value is { Length: 71 }
           && value.StartsWith("sha256:", StringComparison.Ordinal)
           && value.AsSpan(7).ToString().All(static character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static bool IsTargetRevision(string? value)
        => IsDigest(value)
           || value is { Length: CharacterWeaponFireRules.RevisionHexLength }
              && value.All(static character =>
                  character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static CareerWeaponFireEditorState[] ValidateWeapons(
        IReadOnlyList<CareerWeaponFireEditorState> candidates,
        CharacterWorkspaceId workspaceId,
        long workspaceRevision)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count > MaximumWeapons)
            throw new InvalidOperationException("The Career Weapon catalog exceeds its bound.");

        var identities = new HashSet<Guid>();
        var weapons = new CareerWeaponFireEditorState[candidates.Count];
        for (int index = 0; index < candidates.Count; index++)
        {
            CareerWeaponFireEditorState? candidate = candidates[index];
            CharacterWeaponFireState? weapon = candidate?.Weapon;
            if (candidate is null
                || candidate.WorkspaceId != workspaceId
                || candidate.ContentRevision != workspaceRevision
                || weapon is null
                || !CharacterWeaponFireRules.IsValidIdentity(weapon.Identity)
                || !identities.Add(weapon.Identity.WeaponId)
                || weapon.Revision is not { Length: CharacterWeaponFireRules.RevisionHexLength }
                || weapon.Modes is null
                || weapon.Modes.Count == 0
                || weapon.Modes.Select(static mode => mode.Mode).Distinct().Count() != weapon.Modes.Count)
            {
                throw new InvalidOperationException(
                    "The Career Weapon catalog contains a stale, ambiguous, or incoherent identity.");
            }
            weapons[index] = candidate;
        }
        return weapons;
    }

    private static bool IsCoherentEdge(CharacterCareerEdgeUseState edge)
        => edge is not null
           && CharacterCareerEdgeUseRules.TryProject(
               created: true,
               edge.EdgeUsed,
               edge.TotalEdge,
               out CharacterCareerEdgeUseState? projected)
           && projected == edge;

    private static void AddEdgeActions(
        ICollection<Sr5TableWizardActionState> actions,
        Sr5TableWizardLane lane,
        CharacterCareerEdgeUseState edge)
    {
        if (CharacterCareerEdgeUseRules.CanApply(edge, CharacterCareerEdgeUseAction.Spend))
        {
            actions.Add(CreateEdgeAction(
                lane,
                Sr5TableWizardActionKind.SpendEdge,
                lane == Sr5TableWizardLane.BeforeRun
                    ? Sr5TableWizardActionIds.BeforeRunSpendEdge
                    : Sr5TableWizardActionIds.PlaytimeSpendEdge,
                edge,
                CharacterCareerEdgeUseAction.Spend));
        }
        if (CharacterCareerEdgeUseRules.CanApply(edge, CharacterCareerEdgeUseAction.Regain))
        {
            actions.Add(CreateEdgeAction(
                lane,
                Sr5TableWizardActionKind.RegainEdge,
                lane == Sr5TableWizardLane.BeforeRun
                    ? Sr5TableWizardActionIds.BeforeRunRegainEdge
                    : Sr5TableWizardActionIds.PlaytimeRegainEdge,
                edge,
                CharacterCareerEdgeUseAction.Regain));
        }
    }

    private static Sr5TableWizardActionState CreateEdgeAction(
        Sr5TableWizardLane lane,
        Sr5TableWizardActionKind kind,
        string actionId,
        CharacterCareerEdgeUseState edge,
        CharacterCareerEdgeUseAction action)
    {
        int after = CharacterCareerEdgeUseRules.Apply(edge, action);
        string targetRevision = Hash(
            "chummer.sr5_table_wizard.edge_target.v1",
            edge.EdgeUsed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            edge.TotalEdge.ToString(System.Globalization.CultureInfo.InvariantCulture),
            edge.CanSpend ? "1" : "0",
            edge.CanRegain ? "1" : "0");
        string digest = Hash(
            "chummer.sr5_table_wizard.edge_action.v1",
            lane.ToString(),
            kind.ToString(),
            actionId,
            targetRevision,
            after.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var identity = new Sr5TableWizardActionIdentity(
            actionId,
            lane,
            kind,
            Guid.Empty,
            0,
            Guid.Empty,
            targetRevision,
            null,
            digest);
        return new Sr5TableWizardActionState(
            identity,
            kind == Sr5TableWizardActionKind.SpendEdge ? "Spend 1 Edge" : "Regain 1 Edge",
            edge.EdgeUsed,
            after,
            null);
    }

    private static void AddWeaponActions(
        ICollection<Sr5TableWizardActionState> actions,
        Sr5TableWizardLane lane,
        IReadOnlyList<CareerWeaponFireEditorState> weapons)
    {
        foreach (CareerWeaponFireEditorState editor in weapons)
        {
            CharacterWeaponFireState weapon = editor.Weapon;
            foreach (CharacterWeaponFireModeState mode in weapon.Modes)
            {
                if (!CharacterWeaponFireRules.TryCreatePlan(
                        weapon,
                        weapon.Revision,
                        mode.Mode,
                        out CharacterWeaponFirePlan plan))
                {
                    continue;
                }
                string digest = Hash(
                    "chummer.sr5_table_wizard.weapon_action.v1",
                    lane.ToString(),
                    Sr5TableWizardActionIds.PlaytimeFireWeapon,
                    weapon.Identity.WeaponId.ToString("D"),
                    weapon.Identity.AmmoSlot.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    weapon.Identity.AmmoGearId.ToString("D"),
                    weapon.Revision,
                    weapon.DisplayName,
                    mode.Mode.ToString(),
                    plan.RoundsConsumed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    plan.NewAmmoRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    plan.NewAmmoGearQuantity?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        ?? string.Empty,
                    plan.DeleteAmmoGear ? "1" : "0",
                    plan.RequiresPartialConfirmation ? "1" : "0");
                var identity = new Sr5TableWizardActionIdentity(
                    Sr5TableWizardActionIds.PlaytimeFireWeapon,
                    lane,
                    Sr5TableWizardActionKind.FireWeapon,
                    weapon.Identity.WeaponId,
                    weapon.Identity.AmmoSlot,
                    weapon.Identity.AmmoGearId,
                    weapon.Revision,
                    mode.Mode,
                    digest);
                actions.Add(new Sr5TableWizardActionState(
                    identity,
                    weapon.DisplayName,
                    0,
                    0,
                    plan));
            }
        }
    }

    private static string ComputeSnapshotDigest(
        Sr5TableWizardLane lane,
        CharacterWorkspaceId workspaceId,
        long workspaceRevision,
        CharacterCareerEdgeUseState edge,
        IReadOnlyList<CareerWeaponFireEditorState> weapons,
        IReadOnlyList<Sr5TableWizardActionState> actions)
    {
        var values = new List<string>
        {
            Sr5TableWizardSchemas.SnapshotV1,
            lane.ToString(),
            workspaceId.Value,
            workspaceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            edge.EdgeUsed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            edge.TotalEdge.ToString(System.Globalization.CultureInfo.InvariantCulture),
            edge.CanSpend ? "1" : "0",
            edge.CanRegain ? "1" : "0"
        };
        foreach (CareerWeaponFireEditorState editor in weapons)
        {
            values.Add(editor.Weapon.Identity.WeaponId.ToString("D"));
            values.Add(editor.Weapon.Identity.AmmoSlot.ToString(System.Globalization.CultureInfo.InvariantCulture));
            values.Add(editor.Weapon.Identity.AmmoGearId.ToString("D"));
            values.Add(editor.Weapon.DisplayName);
            values.Add(editor.Weapon.Revision);
        }
        values.AddRange(actions.Select(static action => action.Identity.ActionDigest));
        return Hash(values.ToArray());
    }

    private static string Hash(params string[] values)
    {
        var canonical = new StringBuilder();
        foreach (string value in values)
        {
            canonical.Append(value.Length)
                .Append(':')
                .Append(value)
                .Append(';');
        }
        return "sha256:" + Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}

/// <summary>
/// Renderer-neutral select/review session. It never mutates XML; confirmation produces only the
/// existing typed Presentation requests, which remain subject to their own CAS validation.
/// </summary>
public sealed class Sr5TableWizardSession
{
    private Sr5TableWizardSnapshot? _snapshot;
    private Sr5TableWizardState? _state;

    public Sr5TableWizardState State
        => _state ?? throw new InvalidOperationException(
            "Bind an SR5 table snapshot before reading state.");

    public Sr5TableWizardState Bind(
        Sr5TableWizardSnapshot snapshot,
        Sr5TableWizardCheckpoint? checkpoint = null)
    {
        Sr5TableWizardProjector.ValidateSnapshot(snapshot);
        Sr5TableWizardResume resume = ResolveResume(snapshot, checkpoint, out Sr5TableWizardActionState? selected);
        _snapshot = snapshot;
        return _state = new Sr5TableWizardState(snapshot, selected, resume);
    }

    public bool TrySelect(Sr5TableWizardActionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Sr5TableWizardSnapshot snapshot = _snapshot
            ?? throw new InvalidOperationException("Bind an SR5 table snapshot before selecting.");
        Sr5TableWizardActionState? selected = FindAction(snapshot, identity);
        if (selected is null)
            return false;
        _state = new Sr5TableWizardState(
            snapshot,
            selected,
            new Sr5TableWizardResume(Restored: false, InvalidationReason: null));
        return true;
    }

    public Sr5TableWizardCheckpoint CreateCheckpoint()
    {
        Sr5TableWizardState state = State;
        Sr5TableWizardActionState selected = state.SelectedAction
            ?? throw new InvalidOperationException("Choose an exact table action before checkpointing.");
        return new Sr5TableWizardCheckpoint(
            Sr5TableWizardSchemas.CheckpointV1,
            state.Snapshot.Lane,
            state.Snapshot.WorkspaceId.Value,
            state.Snapshot.WorkspaceRevision,
            state.Snapshot.SnapshotDigest,
            selected.Identity);
    }

    public CareerEdgeUseEditRequest CreateEdgeRequest(bool confirmed)
    {
        Sr5TableWizardState state = State;
        Sr5TableWizardActionState selected = RequireConfirmedSelection(state, confirmed);
        CharacterCareerEdgeUseAction action = selected.Identity.Kind switch
        {
            Sr5TableWizardActionKind.SpendEdge => CharacterCareerEdgeUseAction.Spend,
            Sr5TableWizardActionKind.RegainEdge => CharacterCareerEdgeUseAction.Regain,
            _ => throw new InvalidOperationException("The selected table action is not an Edge action.")
        };
        if (!CharacterCareerEdgeUseRules.CanApply(state.Snapshot.Edge, action)
            || CharacterCareerEdgeUseRules.Apply(state.Snapshot.Edge, action) != selected.EdgeUsedAfter)
        {
            throw new InvalidOperationException("The reviewed Edge action is no longer coherent.");
        }
        return new CareerEdgeUseEditRequest(
            state.Snapshot.WorkspaceId,
            state.Snapshot.WorkspaceRevision,
            state.Snapshot.Edge,
            action);
    }

    public CareerWeaponFireRequest CreateWeaponRequest(bool confirmed)
    {
        Sr5TableWizardState state = State;
        Sr5TableWizardActionState selected = RequireConfirmedSelection(state, confirmed);
        if (selected.Identity.Kind != Sr5TableWizardActionKind.FireWeapon
            || selected.Identity.FireMode is not { } mode
            || selected.WeaponPlan is null)
        {
            throw new InvalidOperationException("The selected table action is not a Weapon action.");
        }

        CareerWeaponFireEditorState editor = state.Snapshot.Weapons.Single(candidate =>
            candidate.Weapon.Identity.WeaponId == selected.Identity.WeaponId
            && candidate.Weapon.Identity.AmmoSlot == selected.Identity.AmmoSlot
            && candidate.Weapon.Identity.AmmoGearId == selected.Identity.AmmoGearId
            && string.Equals(
                candidate.Weapon.Revision,
                selected.Identity.TargetRevision,
                StringComparison.Ordinal));
        if (!CharacterWeaponFireRules.TryCreatePlan(
                editor.Weapon,
                editor.Weapon.Revision,
                mode,
                out CharacterWeaponFirePlan plan)
            || plan != selected.WeaponPlan)
        {
            throw new InvalidOperationException("The reviewed Weapon action is no longer coherent.");
        }

        return new CareerWeaponFireRequest(
            state.Snapshot.WorkspaceId,
            state.Snapshot.WorkspaceRevision,
            editor.Weapon.Identity,
            editor.Weapon.Revision,
            mode,
            ConfirmedPartial: plan.RequiresPartialConfirmation);
    }

    public static byte[] SerializeCheckpoint(Sr5TableWizardCheckpoint checkpoint)
    {
        ValidateCheckpointShape(checkpoint);
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint);
    }

    public static bool TryDeserializeCheckpoint(
        ReadOnlySpan<byte> payload,
        out Sr5TableWizardCheckpoint? checkpoint)
    {
        checkpoint = null;
        try
        {
            Sr5TableWizardCheckpoint? parsed =
                JsonSerializer.Deserialize<Sr5TableWizardCheckpoint>(payload);
            if (parsed is null)
                return false;
            ValidateCheckpointShape(parsed);
            checkpoint = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static Sr5TableWizardActionState RequireConfirmedSelection(
        Sr5TableWizardState state,
        bool confirmed)
    {
        if (!confirmed || state.SelectedAction is null)
            throw new InvalidOperationException("Review and explicitly confirm the exact table action first.");
        return state.SelectedAction;
    }

    private static Sr5TableWizardResume ResolveResume(
        Sr5TableWizardSnapshot snapshot,
        Sr5TableWizardCheckpoint? checkpoint,
        out Sr5TableWizardActionState? selected)
    {
        selected = null;
        if (checkpoint is null)
            return new Sr5TableWizardResume(Restored: false, InvalidationReason: null);
        if (!IsValidCheckpointShape(checkpoint))
            return new Sr5TableWizardResume(false, Sr5TableWizardCheckpointInvalidationReasons.InvalidCheckpoint);
        if (checkpoint.Lane != snapshot.Lane)
            return new Sr5TableWizardResume(false, Sr5TableWizardCheckpointInvalidationReasons.LaneChanged);
        if (!string.Equals(checkpoint.WorkspaceId, snapshot.WorkspaceId.Value, StringComparison.Ordinal))
            return new Sr5TableWizardResume(false, Sr5TableWizardCheckpointInvalidationReasons.WorkspaceChanged);
        if (checkpoint.WorkspaceRevision != snapshot.WorkspaceRevision)
            return new Sr5TableWizardResume(false, Sr5TableWizardCheckpointInvalidationReasons.WorkspaceRevisionChanged);
        if (!string.Equals(checkpoint.SnapshotDigest, snapshot.SnapshotDigest, StringComparison.Ordinal))
            return new Sr5TableWizardResume(false, Sr5TableWizardCheckpointInvalidationReasons.SnapshotChanged);

        selected = FindAction(snapshot, checkpoint.SelectedAction);
        return selected is null
            ? new Sr5TableWizardResume(false, Sr5TableWizardCheckpointInvalidationReasons.ActionUnavailable)
            : new Sr5TableWizardResume(Restored: true, InvalidationReason: null);
    }

    private static Sr5TableWizardActionState? FindAction(
        Sr5TableWizardSnapshot snapshot,
        Sr5TableWizardActionIdentity identity)
        => snapshot.Actions.SingleOrDefault(action => action.Identity == identity);

    private static void ValidateCheckpointShape(Sr5TableWizardCheckpoint checkpoint)
    {
        if (!IsValidCheckpointShape(checkpoint))
            throw new InvalidOperationException("The SR5 table wizard checkpoint is invalid.");
    }

    private static bool IsValidCheckpointShape(Sr5TableWizardCheckpoint checkpoint)
        => checkpoint is not null
           && string.Equals(checkpoint.Schema, Sr5TableWizardSchemas.CheckpointV1, StringComparison.Ordinal)
           && Enum.IsDefined(checkpoint.Lane)
           && !string.IsNullOrWhiteSpace(checkpoint.WorkspaceId)
           && checkpoint.WorkspaceRevision > 0
           && Sr5TableWizardProjector.IsDigest(checkpoint.SnapshotDigest)
           && checkpoint.SelectedAction is not null
           && checkpoint.SelectedAction.Lane == checkpoint.Lane
           && Enum.IsDefined(checkpoint.SelectedAction.Kind)
           && !string.IsNullOrWhiteSpace(checkpoint.SelectedAction.ActionId)
           && Sr5TableWizardProjector.IsTargetRevision(checkpoint.SelectedAction.TargetRevision)
           && Sr5TableWizardProjector.IsDigest(checkpoint.SelectedAction.ActionDigest)
           && (checkpoint.SelectedAction.Kind == Sr5TableWizardActionKind.FireWeapon
               ? checkpoint.SelectedAction.WeaponId != Guid.Empty
                 && checkpoint.SelectedAction.AmmoSlot > 0
                 && checkpoint.SelectedAction.FireMode is not null
               : checkpoint.SelectedAction.WeaponId == Guid.Empty
                 && checkpoint.SelectedAction.AmmoSlot == 0
                 && checkpoint.SelectedAction.AmmoGearId == Guid.Empty
                 && checkpoint.SelectedAction.FireMode is null);
}
