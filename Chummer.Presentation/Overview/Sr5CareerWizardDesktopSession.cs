using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chummer.Presentation.Overview;

public static class Sr5CareerWizardSchemas
{
    public const string SnapshotV1 = "chummer.sr5_career_wizard.snapshot.v1";
    public const string CheckpointV1 = "chummer.sr5_career_wizard.checkpoint.v1";
}

public static class Sr5CareerWizardFamilyIds
{
    public const string Economy = "career.economy";
    public const string Advancement = "career.advancement";
    public const string Table = "career.table";
    public const string Calendar = "career.calendar";
}

/// <summary>
/// Stable routes to existing typed Career authorities. This catalog is intentionally narrower
/// than the eventual Career feature set: an absent typed authority stays blocked, while
/// situational conversions and generic character edits are not wizard actions at all.
/// </summary>
public static class Sr5CareerWizardActionIds
{
    public const string AdjustKarma = "career.karma.adjust";
    public const string AdjustNuyen = "career.nuyen.adjust";
    public const string EditKarmaExpense = "career.karma-expense.edit";
    public const string EditNuyenExpense = "career.nuyen-expense.edit";
    public const string AdvanceAttribute = "career.attribute.advance";
    public const string AdvanceActiveSkill = "career.active-skill.advance";
    public const string AdvanceKnowledgeSkill = "career.knowledge-skill.advance";
    public const string AdvanceSkillGroup = "career.skill-group.advance";
    public const string LearnSpecialization = "career.skill-specialization.learn";
    public const string ChangeQuality = "career.quality.change";
    public const string BeforeRun = "career.before-run.prepare";
    public const string Playtime = "career.playtime.open";
    public const string ManageCalendarEntry = "career.calendar-entry.manage";
}

public static class Sr5CareerWizardBlockers
{
    public const string AuthorityUnavailable = "career-wizard-authority-unavailable";
}

public static class Sr5CareerWizardCheckpointInvalidationReasons
{
    public const string InvalidCheckpoint = "career-wizard-checkpoint-invalid";
    public const string WorkspaceChanged = "career-wizard-checkpoint-workspace-changed";
    public const string WorkspaceRevisionChanged = "career-wizard-checkpoint-revision-changed";
    public const string SnapshotChanged = "career-wizard-checkpoint-snapshot-changed";
    public const string ActionUnavailable = "career-wizard-checkpoint-action-unavailable";
}

public sealed record Sr5CareerWizardBinding(
    string WorkspaceId,
    long WorkspaceRevision,
    long SavedRevision,
    string RulesetId,
    string RuntimeFingerprint,
    string SourceDigest,
    string ContentDigest);

/// <summary>
/// Availability projected by one existing typed action authority. Presentation does not infer
/// legality or cost from labels and cannot turn this availability into a confirmation.
/// </summary>
public sealed record Sr5CareerWizardAuthorityAvailability(
    Sr5CareerWizardBinding Binding,
    string ActionId,
    bool IsAvailable,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> SourceAnchorIds,
    string AuthorityDigest);

public sealed record Sr5CareerWizardActionState(
    string ActionId,
    string FamilyId,
    string LabelKey,
    bool CanOpen,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> SourceAnchorIds,
    string AuthorityDigest);

public sealed record Sr5CareerWizardFamilyState(
    string FamilyId,
    string LabelKey,
    IReadOnlyList<Sr5CareerWizardActionState> Actions,
    bool HasAvailableAction);

public sealed record Sr5CareerWizardSnapshot(
    string Schema,
    Sr5CareerWizardBinding Binding,
    IReadOnlyList<Sr5CareerWizardFamilyState> Families,
    string? ActiveActionId,
    bool CanOpenAnyAction,
    string SnapshotDigest);

/// <summary>
/// Navigation-only restart state. It contains no draft answer, quote, plan, confirmation,
/// idempotency key, mutation payload, or receipt.
/// </summary>
public sealed record Sr5CareerWizardCheckpoint(
    string Schema,
    string WorkspaceId,
    long WorkspaceRevision,
    string SnapshotDigest,
    string SelectedActionId);

public sealed record Sr5CareerWizardResume(
    bool Restored,
    string? InvalidationReason);

public sealed record Sr5CareerWizardDesktopState(
    Sr5CareerWizardSnapshot Snapshot,
    string? SelectedActionId,
    Sr5CareerWizardResume Resume,
    bool CanConfirm);

/// <summary>
/// Builds the deterministic SR5 Career chooser from separately projected typed authorities.
/// Missing authorities become explicit blocked actions. Unknown or incoherent authorities are
/// rejected rather than being treated as generic character-edit routes.
/// </summary>
public static class Sr5CareerWizardProjector
{
    private const string RulesetId = "sr5";
    private const int MaximumAuthorities = 64;

    private static readonly FamilyDefinition[] Catalog =
    [
        new(
            Sr5CareerWizardFamilyIds.Economy,
            "career.wizard.family.economy",
            [
                new(Sr5CareerWizardActionIds.AdjustKarma, "career.wizard.action.adjust_karma"),
                new(Sr5CareerWizardActionIds.AdjustNuyen, "career.wizard.action.adjust_nuyen"),
                new(Sr5CareerWizardActionIds.EditKarmaExpense, "career.wizard.action.edit_karma_expense"),
                new(Sr5CareerWizardActionIds.EditNuyenExpense, "career.wizard.action.edit_nuyen_expense")
            ]),
        new(
            Sr5CareerWizardFamilyIds.Advancement,
            "career.wizard.family.advancement",
            [
                new(Sr5CareerWizardActionIds.AdvanceAttribute, "career.wizard.action.advance_attribute"),
                new(Sr5CareerWizardActionIds.AdvanceActiveSkill, "career.wizard.action.advance_active_skill"),
                new(Sr5CareerWizardActionIds.AdvanceKnowledgeSkill, "career.wizard.action.advance_knowledge_skill"),
                new(Sr5CareerWizardActionIds.AdvanceSkillGroup, "career.wizard.action.advance_skill_group"),
                new(Sr5CareerWizardActionIds.LearnSpecialization, "career.wizard.action.learn_specialization"),
                new(Sr5CareerWizardActionIds.ChangeQuality, "career.wizard.action.change_quality")
            ]),
        new(
            Sr5CareerWizardFamilyIds.Table,
            "career.wizard.family.table",
            [
                new(Sr5CareerWizardActionIds.BeforeRun, "career.wizard.action.before_run"),
                new(Sr5CareerWizardActionIds.Playtime, "career.wizard.action.playtime")
            ]),
        new(
            Sr5CareerWizardFamilyIds.Calendar,
            "career.wizard.family.calendar",
            [
                new(Sr5CareerWizardActionIds.ManageCalendarEntry, "career.wizard.action.manage_calendar_entry")
            ])
    ];

    private static readonly IReadOnlyDictionary<string, ActionDefinition> ActionsById = Catalog
        .SelectMany(static family => family.Actions.Select(action => new ActionDefinition(
            action.ActionId,
            family.FamilyId,
            action.LabelKey)))
        .ToDictionary(static action => action.ActionId, StringComparer.Ordinal);

    public static IReadOnlyList<string> KnownActionIds { get; } = Catalog
        .SelectMany(static family => family.Actions.Select(static action => action.ActionId))
        .ToArray();

    public static Sr5CareerWizardSnapshot Project(
        Sr5CareerWizardBinding binding,
        IReadOnlyList<Sr5CareerWizardAuthorityAvailability> authorities)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(authorities);
        ValidateBinding(binding);
        if (authorities.Count > MaximumAuthorities)
            throw new InvalidOperationException("The Career wizard authority set exceeds its bounded catalog.");

        var canonical = new Dictionary<string, Sr5CareerWizardAuthorityAvailability>(StringComparer.Ordinal);
        foreach (Sr5CareerWizardAuthorityAvailability? authority in authorities)
        {
            if (authority is null
                || !ActionsById.ContainsKey(authority.ActionId)
                || !canonical.TryAdd(authority.ActionId, Canonicalize(binding, authority)))
            {
                throw new InvalidOperationException(
                    "Career wizard authorities must be non-null, known, and uniquely identified.");
            }
        }

        Sr5CareerWizardFamilyState[] families = Catalog
            .Select(family => ProjectFamily(family, canonical))
            .ToArray();
        Sr5CareerWizardActionState? active = families
            .SelectMany(static family => family.Actions)
            .FirstOrDefault(static action => action.CanOpen);
        var snapshot = new Sr5CareerWizardSnapshot(
            Sr5CareerWizardSchemas.SnapshotV1,
            binding,
            families,
            active?.ActionId,
            active is not null,
            string.Empty);
        return snapshot with { SnapshotDigest = ComputeSnapshotDigest(snapshot) };
    }

    internal static void ValidateSnapshot(Sr5CareerWizardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateBinding(snapshot.Binding);
        if (!string.Equals(snapshot.Schema, Sr5CareerWizardSchemas.SnapshotV1, StringComparison.Ordinal)
            || snapshot.Families.Count != Catalog.Length
            || !IsSha256(snapshot.SnapshotDigest))
        {
            throw new InvalidOperationException("The SR5 Career wizard snapshot is invalid.");
        }

        for (int familyIndex = 0; familyIndex < Catalog.Length; familyIndex++)
        {
            FamilyDefinition expectedFamily = Catalog[familyIndex];
            Sr5CareerWizardFamilyState actualFamily = snapshot.Families[familyIndex];
            if (actualFamily is null
                || !string.Equals(actualFamily.FamilyId, expectedFamily.FamilyId, StringComparison.Ordinal)
                || !string.Equals(actualFamily.LabelKey, expectedFamily.LabelKey, StringComparison.Ordinal)
                || actualFamily.Actions.Count != expectedFamily.Actions.Count)
            {
                throw new InvalidOperationException("The SR5 Career wizard family catalog changed unexpectedly.");
            }

            for (int actionIndex = 0; actionIndex < expectedFamily.Actions.Count; actionIndex++)
            {
                ActionLabelDefinition expectedAction = expectedFamily.Actions[actionIndex];
                Sr5CareerWizardActionState actualAction = actualFamily.Actions[actionIndex];
                ValidateActionState(actualAction, expectedFamily.FamilyId, expectedAction);
            }

            if (actualFamily.HasAvailableAction != actualFamily.Actions.Any(static action => action.CanOpen))
                throw new InvalidOperationException("The SR5 Career wizard family availability is inconsistent.");
        }

        Sr5CareerWizardActionState? active = snapshot.Families
            .SelectMany(static family => family.Actions)
            .FirstOrDefault(static action => action.CanOpen);
        if (snapshot.CanOpenAnyAction != (active is not null)
            || !string.Equals(snapshot.ActiveActionId, active?.ActionId, StringComparison.Ordinal)
            || !string.Equals(snapshot.SnapshotDigest, ComputeSnapshotDigest(snapshot), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The SR5 Career wizard snapshot binding or digest is inconsistent.");
        }
    }

    private static Sr5CareerWizardFamilyState ProjectFamily(
        FamilyDefinition family,
        IReadOnlyDictionary<string, Sr5CareerWizardAuthorityAvailability> authorities)
    {
        Sr5CareerWizardActionState[] actions = family.Actions
            .Select(action => ProjectAction(family.FamilyId, action, authorities))
            .ToArray();
        return new Sr5CareerWizardFamilyState(
            family.FamilyId,
            family.LabelKey,
            actions,
            actions.Any(static action => action.CanOpen));
    }

    private static Sr5CareerWizardActionState ProjectAction(
        string familyId,
        ActionLabelDefinition action,
        IReadOnlyDictionary<string, Sr5CareerWizardAuthorityAvailability> authorities)
    {
        if (!authorities.TryGetValue(action.ActionId, out Sr5CareerWizardAuthorityAvailability? authority))
        {
            return new Sr5CareerWizardActionState(
                action.ActionId,
                familyId,
                action.LabelKey,
                CanOpen: false,
                Blockers: [Sr5CareerWizardBlockers.AuthorityUnavailable],
                SourceAnchorIds: [],
                AuthorityDigest: string.Empty);
        }

        return new Sr5CareerWizardActionState(
            action.ActionId,
            familyId,
            action.LabelKey,
            authority.IsAvailable,
            authority.Blockers,
            authority.SourceAnchorIds,
            authority.AuthorityDigest);
    }

    private static Sr5CareerWizardAuthorityAvailability Canonicalize(
        Sr5CareerWizardBinding binding,
        Sr5CareerWizardAuthorityAvailability authority)
    {
        if (authority.Binding != binding
            || !IsSha256(authority.AuthorityDigest)
            || authority.Blockers is null
            || authority.SourceAnchorIds is null
            || authority.Blockers.Any(string.IsNullOrWhiteSpace)
            || authority.SourceAnchorIds.Any(string.IsNullOrWhiteSpace)
            || authority.Blockers.Contains(
                Sr5CareerWizardBlockers.AuthorityUnavailable,
                StringComparer.Ordinal)
            || authority.Blockers.Distinct(StringComparer.Ordinal).Count() != authority.Blockers.Count
            || authority.SourceAnchorIds.Distinct(StringComparer.Ordinal).Count() != authority.SourceAnchorIds.Count
            || authority.IsAvailable == authority.Blockers.Any())
        {
            throw new InvalidOperationException(
                "A Career wizard authority must have an exact digest and coherent availability, blockers, and source anchors.");
        }

        return authority with
        {
            Blockers = authority.Blockers.Order(StringComparer.Ordinal).ToArray(),
            SourceAnchorIds = authority.SourceAnchorIds.Order(StringComparer.Ordinal).ToArray()
        };
    }

    private static void ValidateActionState(
        Sr5CareerWizardActionState action,
        string familyId,
        ActionLabelDefinition expected)
    {
        if (action is null
            || !string.Equals(action.ActionId, expected.ActionId, StringComparison.Ordinal)
            || !string.Equals(action.FamilyId, familyId, StringComparison.Ordinal)
            || !string.Equals(action.LabelKey, expected.LabelKey, StringComparison.Ordinal)
            || action.Blockers is null
            || action.SourceAnchorIds is null
            || action.Blockers.Any(string.IsNullOrWhiteSpace)
            || action.SourceAnchorIds.Any(string.IsNullOrWhiteSpace)
            || action.Blockers.Distinct(StringComparer.Ordinal).Count() != action.Blockers.Count
            || action.SourceAnchorIds.Distinct(StringComparer.Ordinal).Count() != action.SourceAnchorIds.Count
            || !action.Blockers.SequenceEqual(
                action.Blockers.Order(StringComparer.Ordinal),
                StringComparer.Ordinal)
            || !action.SourceAnchorIds.SequenceEqual(
                action.SourceAnchorIds.Order(StringComparer.Ordinal),
                StringComparer.Ordinal)
            || action.CanOpen == action.Blockers.Any())
        {
            throw new InvalidOperationException("The SR5 Career wizard action state is inconsistent.");
        }

        bool missing = action.Blockers.Contains(
            Sr5CareerWizardBlockers.AuthorityUnavailable,
            StringComparer.Ordinal);
        if (missing ? action.AuthorityDigest.Length != 0 : !IsSha256(action.AuthorityDigest))
            throw new InvalidOperationException("The SR5 Career wizard action authority digest is invalid.");
    }

    private static void ValidateBinding(Sr5CareerWizardBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.WorkspaceId)
            || binding.WorkspaceRevision <= 0
            || binding.SavedRevision < 0
            || !string.Equals(binding.RulesetId, RulesetId, StringComparison.Ordinal)
            || !IsSha256(binding.RuntimeFingerprint)
            || !IsSha256(binding.SourceDigest)
            || !IsSha256(binding.ContentDigest))
        {
            throw new InvalidOperationException(
                "The Career wizard requires an exact SR5 workspace, revision, runtime, source, and content binding.");
        }
    }

    private static string ComputeSnapshotDigest(Sr5CareerWizardSnapshot snapshot)
    {
        var canonical = new StringBuilder();
        Append(canonical, snapshot.Schema);
        Append(canonical, snapshot.Binding.WorkspaceId);
        Append(canonical, snapshot.Binding.WorkspaceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, snapshot.Binding.SavedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, snapshot.Binding.RulesetId);
        Append(canonical, snapshot.Binding.RuntimeFingerprint);
        Append(canonical, snapshot.Binding.SourceDigest);
        Append(canonical, snapshot.Binding.ContentDigest);
        foreach (Sr5CareerWizardFamilyState family in snapshot.Families)
        {
            Append(canonical, family.FamilyId);
            Append(canonical, family.LabelKey);
            foreach (Sr5CareerWizardActionState action in family.Actions)
            {
                Append(canonical, action.ActionId);
                Append(canonical, action.FamilyId);
                Append(canonical, action.LabelKey);
                Append(canonical, action.CanOpen ? "1" : "0");
                Append(canonical, action.AuthorityDigest);
                foreach (string blocker in action.Blockers)
                    Append(canonical, blocker);
                foreach (string sourceAnchorId in action.SourceAnchorIds)
                    Append(canonical, sourceAnchorId);
            }
        }
        Append(canonical, snapshot.ActiveActionId ?? string.Empty);
        Append(canonical, snapshot.CanOpenAnyAction ? "1" : "0");
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return "sha256:" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length)
            .Append(':')
            .Append(value)
            .Append(';');
    }

    internal static bool IsSha256(string? value)
        => value is { Length: 71 }
           && value.StartsWith("sha256:", StringComparison.Ordinal)
           && value.AsSpan(7).ToString().All(static character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record FamilyDefinition(
        string FamilyId,
        string LabelKey,
        IReadOnlyList<ActionLabelDefinition> Actions);

    private sealed record ActionLabelDefinition(string ActionId, string LabelKey);

    private sealed record ActionDefinition(string ActionId, string FamilyId, string LabelKey);
}

/// <summary>
/// Renderer-neutral Career chooser session. It only changes which typed action route is selected.
/// The selected action's own interaction presenter remains responsible for review, confirmation,
/// persistence, recovery, and correction.
/// </summary>
public sealed class Sr5CareerWizardDesktopSession
{
    private Sr5CareerWizardSnapshot? _snapshot;
    private Sr5CareerWizardDesktopState? _state;

    public Sr5CareerWizardDesktopState State
        => _state ?? throw new InvalidOperationException(
            "Bind a Career wizard snapshot before reading state.");

    public Sr5CareerWizardDesktopState Bind(
        Sr5CareerWizardSnapshot snapshot,
        Sr5CareerWizardCheckpoint? checkpoint = null)
    {
        Sr5CareerWizardProjector.ValidateSnapshot(snapshot);
        Sr5CareerWizardResume resume = ResolveResume(snapshot, checkpoint, out string? selectedActionId);
        _snapshot = snapshot;
        return _state = new Sr5CareerWizardDesktopState(
            snapshot,
            selectedActionId,
            resume,
            CanConfirm: false);
    }

    public bool TrySelectAction(string actionId)
    {
        Sr5CareerWizardSnapshot snapshot = _snapshot
            ?? throw new InvalidOperationException("Bind a Career wizard snapshot before navigating.");
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        Sr5CareerWizardActionState? action = FindAction(snapshot, actionId);
        if (action is null || !action.CanOpen)
            return false;

        _state = new Sr5CareerWizardDesktopState(
            snapshot,
            action.ActionId,
            new Sr5CareerWizardResume(Restored: false, InvalidationReason: null),
            CanConfirm: false);
        return true;
    }

    public Sr5CareerWizardCheckpoint CreateCheckpoint()
    {
        Sr5CareerWizardDesktopState state = State;
        if (state.SelectedActionId is null)
            throw new InvalidOperationException("No available Career action can be checkpointed.");
        return new Sr5CareerWizardCheckpoint(
            Sr5CareerWizardSchemas.CheckpointV1,
            state.Snapshot.Binding.WorkspaceId,
            state.Snapshot.Binding.WorkspaceRevision,
            state.Snapshot.SnapshotDigest,
            state.SelectedActionId);
    }

    public static byte[] SerializeCheckpoint(Sr5CareerWizardCheckpoint checkpoint)
    {
        ValidateCheckpointShape(checkpoint);
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint);
    }

    public static bool TryDeserializeCheckpoint(
        ReadOnlySpan<byte> payload,
        out Sr5CareerWizardCheckpoint? checkpoint)
    {
        checkpoint = null;
        try
        {
            Sr5CareerWizardCheckpoint? parsed =
                JsonSerializer.Deserialize<Sr5CareerWizardCheckpoint>(payload);
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

    private static Sr5CareerWizardResume ResolveResume(
        Sr5CareerWizardSnapshot snapshot,
        Sr5CareerWizardCheckpoint? checkpoint,
        out string? selectedActionId)
    {
        selectedActionId = snapshot.ActiveActionId;
        if (checkpoint is null)
            return new Sr5CareerWizardResume(Restored: false, InvalidationReason: null);
        if (!IsValidCheckpointShape(checkpoint))
            return new Sr5CareerWizardResume(false, Sr5CareerWizardCheckpointInvalidationReasons.InvalidCheckpoint);
        if (!string.Equals(checkpoint.WorkspaceId, snapshot.Binding.WorkspaceId, StringComparison.Ordinal))
            return new Sr5CareerWizardResume(false, Sr5CareerWizardCheckpointInvalidationReasons.WorkspaceChanged);
        if (checkpoint.WorkspaceRevision != snapshot.Binding.WorkspaceRevision)
            return new Sr5CareerWizardResume(false, Sr5CareerWizardCheckpointInvalidationReasons.WorkspaceRevisionChanged);
        if (!string.Equals(checkpoint.SnapshotDigest, snapshot.SnapshotDigest, StringComparison.Ordinal))
            return new Sr5CareerWizardResume(false, Sr5CareerWizardCheckpointInvalidationReasons.SnapshotChanged);

        Sr5CareerWizardActionState? action = FindAction(snapshot, checkpoint.SelectedActionId);
        if (action is null || !action.CanOpen)
            return new Sr5CareerWizardResume(false, Sr5CareerWizardCheckpointInvalidationReasons.ActionUnavailable);

        selectedActionId = action.ActionId;
        return new Sr5CareerWizardResume(Restored: true, InvalidationReason: null);
    }

    private static Sr5CareerWizardActionState? FindAction(
        Sr5CareerWizardSnapshot snapshot,
        string actionId)
        => snapshot.Families
            .SelectMany(static family => family.Actions)
            .SingleOrDefault(action => string.Equals(action.ActionId, actionId, StringComparison.Ordinal));

    private static void ValidateCheckpointShape(Sr5CareerWizardCheckpoint checkpoint)
    {
        if (!IsValidCheckpointShape(checkpoint))
            throw new InvalidOperationException("The SR5 Career wizard checkpoint is invalid.");
    }

    private static bool IsValidCheckpointShape(Sr5CareerWizardCheckpoint checkpoint)
        => checkpoint is not null
           && string.Equals(checkpoint.Schema, Sr5CareerWizardSchemas.CheckpointV1, StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(checkpoint.WorkspaceId)
           && checkpoint.WorkspaceRevision > 0
           && Sr5CareerWizardProjector.IsSha256(checkpoint.SnapshotDigest)
           && Sr5CareerWizardProjector.KnownActionIds.Contains(
               checkpoint.SelectedActionId,
               StringComparer.Ordinal);
}
