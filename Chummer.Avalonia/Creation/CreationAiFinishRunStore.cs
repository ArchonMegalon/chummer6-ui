using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;

namespace Chummer.Avalonia.Creation;

/// <summary>
/// A durable, fail-closed record for the gated staged finisher. A retained run is
/// inspected read-only, including after process restart. Resumption is qualified:
/// it only ever continues the SAME frozen plan under the SAME operation identity in
/// the SAME exclusive fence, by appending to the journal; the exclusive CreateNew
/// fence is never replaced, deleted, or re-created. The pinned Core does not offer
/// a unified draft-completion receipt lookup, so an unobserved write stays
/// uncertain until the re-dispatch itself observes it.
/// </summary>
public sealed class CreationAiFinishRunStore(string root)
{
    public sealed record Entry(string Kind, string OperationId, string Domain,
        CreationAiFinishService.AiFinishDomainResult? Result = null, string? PlanJson = null);

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
                result = new(step.Domain, false, CharacterCreationFoundationOutcomes.Invalid,
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
    /// Qualified durable staged resumption: continues the SAME frozen plan under
    /// the SAME operation identity by appending to the SAME exclusive fence file.
    /// Exact receipt reconciliation first: the claimed plan must byte-match the
    /// caller's plan, the claimed operation id must match, and every dispatching
    /// entry must be paired with an observed result (settled) or is the one
    /// uncertain domain (returned as the resume point). The exclusive CreateNew
    /// fence stays untouched; nothing is deleted, replaced, or replayed.
    /// </summary>
    public ResumptionReceipt ReconcileAndResume(
        OwnerContextStamp owner, string workspaceId, string operationId, string frozenPlanJson,
        IReadOnlyList<(string Domain, Func<CreationAiFinishService.AiFinishDomainResult> Apply)> steps)
    {
        string path = RunPath(owner, workspaceId);
        Directory.CreateDirectory(root);
        IReadOnlyList<Entry> entries = Read(owner, workspaceId)
            ?? throw new InvalidOperationException(
                "No saved AI completion operation exists for this character; there is nothing to resume.");
        if (entries[0].OperationId != operationId)
            throw new InvalidOperationException(
                "The saved operation identity does not match the resume request; a different plan cannot be resumed.");
        string? claimedPlan = entries.FirstOrDefault(e => e.Kind == "claimed")?.PlanJson;
        if (claimedPlan is null || claimedPlan != frozenPlanJson)
            throw new InvalidOperationException(
                "The frozen plan does not match the saved record; resumption would send different changes.");
        if (entries[^1].Kind == "completed")
            throw new InvalidOperationException(
                "The saved operation already completed; there is nothing to resume.");

        // Exact receipt reconciliation: pair dispatching with observed per domain.
        // At most one unpaired dispatching (the uncertain domain) may exist: the
        // store stops at the first failed step, so later steps were never sent.
        var settled = new HashSet<string>(StringComparer.Ordinal);
        string? uncertain = null;
        for (int i = 1; i < entries.Count; i++)
        {
            if (entries[i].Kind == "dispatching")
            {
                if (i + 1 < entries.Count && entries[i + 1].Kind == "observed"
                    && entries[i + 1].Domain == entries[i].Domain)
                {
                    settled.Add(entries[i].Domain);
                    i++;
                    continue;
                }
                if (uncertain is not null || i != entries.Count - 1)
                    throw new InvalidOperationException(
                        "The saved record has an inconsistent dispatch history; manual inspection is required before any continuation.");
                uncertain = entries[i].Domain;
            }
        }
        if (uncertain is null)
            throw new InvalidOperationException(
                "The saved record has no unobserved stage to resume; manual inspection is required.");
        if (!steps.Any(step => step.Domain == uncertain))
            throw new InvalidOperationException(
                "The resume plan does not contain the uncertain domain; the record stays pending.");

        // Append-only continuation: same stream discipline as Execute.
        using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.None,
            Options = FileOptions.WriteThrough
        });
        void Save(Entry entry)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        Save(new("resuming", operationId, uncertain));
        var results = new List<CreationAiFinishService.AiFinishDomainResult>();
        bool resuming = true;
        foreach (var step in steps)
        {
            if (resuming)
            {
                if (settled.Contains(step.Domain) || step.Domain != uncertain)
                    continue;
                resuming = false;
            }
            Save(new("dispatching", operationId, step.Domain));
            CreationAiFinishService.AiFinishDomainResult result;
            try { result = step.Apply(); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                result = new(step.Domain, false, "resumption-outcome-uncertain",
                    ["The saved outcome is uncertain again. Reload the character to inspect it; no retry or later step was sent."]);
            }
            results.Add(result);
            Save(new("observed", operationId, step.Domain, result));
            if (!result.Success)
                return new ResumptionReceipt(operationId, uncertain, results);
        }
        if (resuming)
            throw new InvalidOperationException(
                "The resume plan does not contain the uncertain domain; the record stays pending.");
        Save(new("completed", operationId, ""));
        return new ResumptionReceipt(operationId, uncertain, results);
    }

    public sealed record ResumptionReceipt(
        string OperationId, string ResumedDomain, IReadOnlyList<CreationAiFinishService.AiFinishDomainResult> Results);
}
