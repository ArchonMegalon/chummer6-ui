using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;

namespace Chummer.Avalonia.Creation;

/// <summary>
/// A durable, fail-closed record for the gated staged finisher. A retained run
/// is inspected read-only, including after process restart. There is NO automatic
/// continuation: the pinned Core has no draft-completion receipt/replay contract,
/// so a re-dispatch could not be distinguished from a duplicate mutation. When a
/// dispatch outcome was not observed, the typed classification is
/// <see cref="RecoveryState.OutcomeUnknown"/> and every surface stays read-only.
/// The journal is a state machine: every prefix that <see cref="Execute"/> can
/// emit has a defined restart interpretation in <see cref="ClassifyRecovery"/>.
/// The writer emits only claimed/dispatching/observed/completed, so an unpaired
/// dispatching entry is always the final entry and classifies as the uncertain
/// restart point - including after a repeated interruption.
/// </summary>
public sealed class CreationAiFinishRunStore(string root)
{
    public const string UncertainOutcome = "ai-finish-outcome-uncertain";

    public sealed record Entry(string Kind, string OperationId, string Domain,
        CreationAiFinishService.AiFinishDomainResult? Result = null, string? PlanJson = null);

    /// <summary>Typed recovery classification of a saved run. No English matching.</summary>
    public enum RecoveryState
    {
        /// <summary>No saved run: normal flow.</summary>
        None = 0,
        /// <summary>Every dispatched stage was observed successful; the run is terminal.</summary>
        Completed = 1,
        /// <summary>A dispatched stage was observed failed; later stages were never sent. Terminal.</summary>
        Failed = 2,
        /// <summary>A dispatched stage's outcome was never observed; what happened is unknown.</summary>
        OutcomeUnknown = 3,
        /// <summary>The record is damaged or structurally inconsistent; nothing is authorized.</summary>
        Unrecoverable = 4,
        /// <summary>The run stopped between stages without its terminal marker. No continuation is authorized.</summary>
        Incomplete = 5,
    }

    public static CreationAiFinishRunStore ForCurrentProfile()
    {
        string? configured = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? xdg = !OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("XDG_DATA_HOME") : null;
        string profile = !string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(configured)
            : !string.IsNullOrWhiteSpace(xdg) && Path.IsPathFullyQualified(xdg) ? xdg
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(profile))
            throw new InvalidOperationException("A persistent profile directory is required for AI completion.");
        return new(Path.Combine(profile, "Chummer6", "ai-finish"));
    }

    public static bool IsLinkedOwner(OwnerContextStamp owner) => owner.IsValid
        && !owner.Owner.UsesLocalSingleUserValue
        && owner.Owner.NormalizedValue.StartsWith("install-account:", StringComparison.Ordinal)
        && owner.Owner.NormalizedValue.Length > "install-account:".Length;

    private string RunPath(OwnerContextStamp owner, string workspaceId)
    {
        if (!IsLinkedOwner(owner)) throw new InvalidOperationException("Link your account before using AI completion.");
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new[] { owner.Owner.NormalizedValue, workspaceId }))));
        return Path.Combine(root, key + ".jsonl");
    }

    public IReadOnlyList<Entry>? Read(OwnerContextStamp owner, string workspaceId)
    {
        string path = RunPath(owner, workspaceId);
        // File absence alone means no dispatch. An empty, damaged or unreadable
        // existing fence must never be interpreted as permission to start again.
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 8 * 1024 * 1024) throw new InvalidOperationException("AI completion recovery record needs review.");
        using var reader = new StreamReader(stream);
        var entries = new List<Entry>();
        while (reader.ReadLine() is { } line)
        {
            try { entries.Add(JsonSerializer.Deserialize<Entry>(line) ?? throw new JsonException()); }
            catch (JsonException) { throw new InvalidOperationException("AI completion was interrupted. Its saved record cannot authorize another write."); }
        }
        if (entries.Count == 0 || entries[0].Kind != "claimed")
            throw new InvalidOperationException("AI completion was interrupted before its record was complete. No retry was sent.");
        return entries;
    }

    public IReadOnlyList<CreationAiFinishService.AiFinishDomainResult> Execute(
        OwnerContextStamp owner, string workspaceId, string operationId, string frozenPlanJson,
        IReadOnlyList<(string Domain, Func<CreationAiFinishService.AiFinishDomainResult> Apply)> steps)
    {
        string path = RunPath(owner, workspaceId);
        Directory.CreateDirectory(root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        // Exclusive creation is the cross-process no-replay fence. Never replace
        // or delete it, even when the process dies before the first confirmation.
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            Options = FileOptions.WriteThrough
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using var stream = new FileStream(path, options);
        void Save(Entry entry)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        Save(new("claimed", operationId, "", PlanJson: frozenPlanJson));
        var results = new List<CreationAiFinishService.AiFinishDomainResult>();
        foreach (var step in steps)
        {
            Save(new("dispatching", operationId, step.Domain));
            CreationAiFinishService.AiFinishDomainResult result;
            try { result = step.Apply(); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                result = new(step.Domain, false, UncertainOutcome,
                    ["The saved outcome is uncertain. Reload the character to inspect it; no retry or later step was sent."]);
            }
            results.Add(result);
            Save(new("observed", operationId, step.Domain, result));
            if (!result.Success) return results;
        }
        Save(new("completed", operationId, ""));
        return results;
    }

    /// <summary>
    /// Read-only typed recovery classification for the UI and tests. Never
    /// dispatches; never opens the journal for writing. Throws for a structurally
    /// damaged record (the same fail-closed contract as Read); returns
    /// <see cref="RecoveryState.None"/> when no run was ever started.
    /// Every durable prefix the writer can emit has a defined interpretation:
    /// a trailing unpaired dispatching entry is OutcomeUnknown (its domain is
    /// the uncertain one), a trailing completed entry is Completed, an observed
    /// rejection is Failed; exception outcomes remain OutcomeUnknown. A prefix
    /// without its terminal marker is Incomplete and cannot authorize continuation.
    /// </summary>
    public (RecoveryState State, string? UncertainDomain, IReadOnlyList<Entry> Entries) ClassifyRecovery(
        OwnerContextStamp owner, string workspaceId)
    {
        string path = RunPath(owner, workspaceId);
        if (!File.Exists(path)) return (RecoveryState.None, null, []);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 8 * 1024 * 1024) throw new InvalidOperationException("AI completion recovery record needs review.");
        using var reader = new StreamReader(stream);
        var entries = new List<Entry>();
        while (reader.ReadLine() is { } line)
        {
            try { entries.Add(JsonSerializer.Deserialize<Entry>(line) ?? throw new JsonException()); }
            catch (JsonException) { throw new InvalidOperationException("AI completion was interrupted. Its saved record cannot authorize another write."); }
        }
        if (entries.Count == 0 || entries[0].Kind != "claimed")
            throw new InvalidOperationException("AI completion was interrupted before its record was complete. No retry was sent.");

        // Only a durable completed marker proves the whole run completed. A
        // successful prefix can still have unsent stages in its frozen plan.
        string operation = entries[0].OperationId;
        if (string.IsNullOrWhiteSpace(operation) || entries[0].Domain != ""
            || entries.Any(e => e.OperationId != operation))
            throw new InvalidOperationException("The saved record has inconsistent operation identities.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 1; i < entries.Count;)
        {
            Entry entry = entries[i];
            if (entry.Kind == "completed" && i == entries.Count - 1 && entry.Domain == "" && entry.Result is null)
                return (RecoveryState.Completed, null, entries);
            if (entry.Kind != "dispatching" || string.IsNullOrWhiteSpace(entry.Domain) || !seen.Add(entry.Domain))
                throw new InvalidOperationException("The saved record has an inconsistent dispatch history.");
            if (i == entries.Count - 1)
                return (RecoveryState.OutcomeUnknown, entry.Domain, entries);
            Entry observation = entries[i + 1];
            if (observation.Kind != "observed" || observation.Domain != entry.Domain
                || observation.Result is not { } result || result.Domain != entry.Domain)
                throw new InvalidOperationException("The saved record has an inconsistent observation.");
            i += 2;
            if (!result.Success)
            {
                if (i != entries.Count)
                    throw new InvalidOperationException("The saved record continues after a stopped stage.");
                // Older writers used Invalid both for rejection and for exceptions
                // after a possible commit. Those records cannot prove a failure.
                bool uncertain = result.Outcome is UncertainOutcome
                    or CharacterCreationFoundationOutcomes.Invalid
                    or CharacterCreationFoundationOutcomes.Success;
                return (uncertain ? RecoveryState.OutcomeUnknown : RecoveryState.Failed,
                    uncertain ? entry.Domain : null, entries);
            }
        }
        return (RecoveryState.Incomplete, null, entries);
    }
}
