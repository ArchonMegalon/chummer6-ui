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
        ("source-backed", "checked"),
        ("first-party", "local"),
        ("media-factory", "render"),
        ("provider bindings", "service links"),
        ("provider binding", "service link"),
        ("provider", "service"),
        ("proof", "check"),
        ("receipt", "record"),
        ("operator voice", "default voice"),
        ("operator reading", "dossier reading"),
        ("operator shaped", "runner shaped"),
        ("operator", "user"),
        ("grounded", "current"),
        ("governed", "reviewed"),
        ("canonical", "selected"),
        ("canon", "story"),
        ("handoff", "next step"),
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
            cleaned = cleaned.Replace(from, to, StringComparison.OrdinalIgnoreCase);
        }

        cleaned = Regex.Replace(cleaned, @"\bALICE\b", "Alice", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\bAI\b", "assistant", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @" ?([,.;:])", "$1", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\s+\n", "\n", RegexOptions.CultureInvariant);
        return cleaned.Trim();
    }

    public static string[] CleanLines(IEnumerable<string> values)
        => values
            .Select(Clean)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
}
