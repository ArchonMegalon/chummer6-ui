using System.Text.RegularExpressions;

namespace Chummer.Presentation;

public static class UndetectableHumanizerCopyAdapter
{
    private const string GroundedDossierToken = "__CHUMMER_KEEP_GROUNDED_DOSSIER__";
    private const string GroundedDossierPortraitToken = "__CHUMMER_KEEP_GROUNDED_DOSSIER_PORTRAIT__";

    private static readonly (string From, string To)[] PhraseReplacements =
    [
        ("Public Proof Shelf", "Public Files"),
        ("Proof Shelf", "Files"),
        ("Proof Trail", "Details"),
        ("proof trail", "details"),
        ("My Artifact Shelf", "My Files"),
        ("Creator Artifact Shelf", "Creator Files"),
        ("Campaign Artifact Shelf", "Campaign Files"),
        ("artifact shelf", "files"),
        ("Rule Environment Studio", "Rules Setup"),
        ("Rule-environment studio", "Rules setup"),
        ("rule-environment", "rules setup"),
        ("same claimed desktop lane", "this desktop"),
        ("same claimed desktop path", "this desktop"),
        ("claimed desktop lane", "this desktop"),
        ("claimed desktop path", "this desktop"),
        ("claimed desktop", "this desktop"),
        ("the this desktop", "this desktop"),
        ("Support diagnostics explain receipt", "Support explanation"),
        ("Support diagnostics receipt", "Support details"),
        ("Support diagnostics", "Support details"),
        ("Diagnostics environment diff before support", "System before support"),
        ("Diagnostics environment change before support", "System before support"),
        ("Diagnostics environment diff after support", "System after support"),
        ("Diagnostics environment change after support", "System after support"),
        ("Diagnostics environment diff", "System details"),
        ("Diagnostics environment change", "System details"),
        ("Before-after diffs", "Changes"),
        ("follow-through", "next step"),
        ("follow through", "next step"),
        ("Follow-up", "Next step"),
        ("follow-up", "next step"),
        ("follow up", "next step"),
        ("signed-in support lane", "account support"),
        ("signed-in support", "account support"),
        ("support closure", "support status"),
        ("flagship client", "desktop app"),
        ("flagship desktop", "desktop app"),
        ("desktop flagship", "desktop app"),
        ("flagship", "desktop"),
        ("synthetic", "local"),
        ("reporter-ready release path", "available release"),
        ("reporter-ready release", "available release"),
        ("reporter-ready fix", "available fix"),
        ("reporter-ready", "available"),
        ("Grounded explain receipt", "Current explanation"),
        ("receipt-backed", "reviewed"),
        ("explain companion", "details"),
        ("Explain receipts", "Explanations"),
        ("Explain receipt", "Explanation"),
        ("Unmixr AI", "Unmixr"),
        ("AI narration", "alternate narration"),
        ("AI coach route", "assistant service"),
        ("approved origin canon", "approved origin story"),
        ("origin canon", "origin story"),
        ("source packet", "source"),
        ("source-backed", "current"),
        ("first-party", "local"),
        ("media-factory", "render"),
        ("deterministic providers", "rules services"),
        ("provider bindings", "service links"),
        ("provider binding", "service link"),
        ("service truth", "service status"),
        ("runtime truth", "runtime status"),
        ("provider truth", "service status"),
        ("providers", "services"),
        ("provider", "service"),
        ("support can cite", "Support can use"),
        ("authority truth", "status"),
        ("proof status", "release status"),
        ("explain proof", "explanation"),
        ("explain receipt", "explanation"),
        ("environment truth", "environment details"),
        ("parity evidence", "compatibility notes"),
        ("evidence", "details"),
        ("proofs", "details"),
        ("proof", "details"),
        ("truth", "status"),
        ("diffs", "changes"),
        ("diff", "change"),
        ("receipts", "records"),
        ("receipt", "record"),
        ("artifacts", "items"),
        ("artifact", "item"),
        ("operator voice", "default voice"),
        ("operator reading", "dossier reading"),
        ("operator shaped", "runner shaped"),
        ("operator", "user"),
        ("grounded", "current"),
        ("governed", "reviewed"),
        ("validation checks", "review"),
        ("verification checks", "confirmation"),
        ("audit verdict", "review decision"),
        ("validation", "review"),
        ("verification", "confirmation"),
        ("audit", "review"),
        ("verdict", "decision"),
        ("posture", "status"),
        ("checks", "reviews"),
        ("canonical", "selected"),
        ("canon", "story"),
        ("handoff", "next step"),
        ("server-plane", "server update"),
        ("server plane", "server update"),
        ("registry", "app record"),
        ("rails", "paths"),
        ("rail", "path"),
        ("lanes", "paths"),
        ("lane", "path"),
        ("bundle root", "dossier folder"),
        ("preview-backed", "previewed"),
        ("generated", "created"),
        ("claim the install", "claim this copy"),
    ];

    public static string Humanize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string cleaned = value.Trim()
            .Replace("Grounded dossier portrait", GroundedDossierPortraitToken, StringComparison.OrdinalIgnoreCase)
            .Replace("Grounded dossier", GroundedDossierToken, StringComparison.OrdinalIgnoreCase);

        foreach ((string from, string to) in PhraseReplacements)
        {
            cleaned = ReplaceWholePhrase(cleaned, from, to);
        }

        cleaned = cleaned
            .Replace(GroundedDossierPortraitToken, "Grounded dossier portrait", StringComparison.Ordinal)
            .Replace(GroundedDossierToken, "Grounded dossier", StringComparison.Ordinal);

        cleaned = Regex.Replace(cleaned, @"\bALICE\b", "Alice", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\bAI\b", "assistant", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @" ?([,.;:])", "$1", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\s+\n", "\n", RegexOptions.CultureInvariant);
        return cleaned.Trim();
    }

    private static string ReplaceWholePhrase(string value, string from, string to)
    {
        string pattern = $@"(?<!\w){Regex.Escape(from)}(?!\w)";
        return Regex.Replace(value, pattern, to, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static string[] HumanizeLines(IEnumerable<string> values)
        => values
            .Select(Humanize)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
}
