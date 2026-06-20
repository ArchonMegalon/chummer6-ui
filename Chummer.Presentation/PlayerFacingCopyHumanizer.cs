using System.Text.RegularExpressions;

namespace Chummer.Presentation;

public static class PlayerFacingCopyHumanizer
{
    private static readonly (string From, string To)[] PhraseReplacements =
    [
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
        ("proof status", "release status"),
        ("explain proof", "explanation"),
        ("explain receipt", "explanation"),
        ("environment truth", "environment details"),
        ("parity evidence", "compatibility notes"),
        ("evidence", "details"),
        ("proofs", "details"),
        ("proof", "details"),
        ("truth", "status"),
        ("receipts", "records"),
        ("receipt", "record"),
        ("artifacts", "items"),
        ("artifact", "item"),
        ("artifact shelf", "item shelf"),
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
    ];

    public static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string cleaned = value.Trim();
        foreach ((string from, string to) in PhraseReplacements)
        {
            cleaned = ReplaceWholePhrase(cleaned, from, to);
        }

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

    public static string[] CleanLines(IEnumerable<string> values)
        => values
            .Select(Clean)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
}
