using System.Text.RegularExpressions;
using Chummer.Application.LifeModules;
using Chummer.Contracts.LifeModules;

namespace Chummer.Presentation.OriginBooks;

/// <summary>
/// Read-only display of a chapter from an already Core-validated book. Legacy
/// source templates are not executable narrative: their random-table macros
/// cannot silently choose new biography. Keep the original chapter/digest intact
/// and show only that decision's confirmed facts until authored prose is ready.
/// </summary>
public static partial class OriginBookChapterText
{
    public static string Render(OriginStoryArcSeed book, OriginNarrativeChapterProjection chapter)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(chapter);
        if (!book.VisibleChapters.Contains(chapter)
            || !book.CanonicalLayer.AcceptedDecisionIds.Contains(chapter.ThroughAcceptedDecisionId, StringComparer.Ordinal))
            throw new InvalidOperationException("The chapter does not belong to this accepted book.");

        string language = OriginDossierNarrativeLocalePolicy.PrimaryLanguage(book.CurrentTurn.Locale);
        string runner = book.CurrentTurn.RunnerDisplayName;
        // The canonical finish chapter contains the old wizard prompt, not
        // authored prose. Present the recorded choice in the past tense so it
        // cannot ask a Career runner to finish Creation again. Selection alone
        // does not prove Career entry; never infer that from this historical fact.
        // Approved reading editions remain a separate, higher-priority layer.
        if (chapter.PlayerLayerDigest == LifeModuleOriginDossierService.EmptyPlayerLayerDigest
            && chapter.ProviderLayerDigest == LifeModuleOriginDossierService.EmptyProviderLayerDigest
            && book.CanonicalLayer.Facts.Any(fact => fact.AcceptedDecisionId == chapter.ThroughAcceptedDecisionId
                && fact.FactKind == "accepted-module-selection-finish"))
            return language switch
            {
                "de" => $"Deine Modulauswahl ist abgeschlossen. Die bestätigten Entscheidungen für {runner} bleiben in den vorherigen Kapiteln erhalten.",
                "es" => $"La selección de módulos de vida está completa. Las decisiones confirmadas de {runner} se conservan en los capítulos anteriores.",
                _ => $"The Life Modules selection is complete. {runner}'s confirmed choices are preserved in the preceding chapters."
            };
        if (!LegacyTemplateToken().IsMatch(chapter.VisibleMarkdown))
            return chapter.VisibleMarkdown;

        string title = chapter.Title;
        string lead = language switch
        {
            "de" => $"Dieser Teil von {runner}s Vorgeschichte steht fest: {title}.",
            "es" => $"Esta parte de la historia de {runner} ya está decidida: {title}.",
            _ => $"This part of {runner}'s background is settled: {title}."
        };
        string pending = language switch
        {
            "de" => "Die ausgearbeitete Erzählung dazu steht noch aus. Gespeicherte Entscheidungen bleiben unverändert.",
            "es" => "La narración completa aún está pendiente. Las decisiones guardadas no cambian.",
            _ => "The full narrative is still pending. Your saved decisions are unchanged."
        };
        // Do not leak later decisions into an earlier chapter or interpret user
        // answers as templates. A literal dollar expression in an answer stays literal.
        string[] details = book.CanonicalLayer.Facts
            .Where(fact => fact.AcceptedDecisionId == chapter.ThroughAcceptedDecisionId
                && fact.FactKind is "accepted-metatype" or "accepted-life-module-answer")
            .Select(fact => fact.LocalizedSummary)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return string.Join("\n\n", new[] { lead }.Concat(details).Append(pending));
    }

    [GeneratedRegex(@"\$[A-Za-z][A-Za-z0-9_]*", RegexOptions.CultureInvariant)]
    private static partial Regex LegacyTemplateToken();
}
