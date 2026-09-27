using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;

namespace Chummer.Presentation.OriginBooks;

/// <summary>
/// Story-facing copy for an already admitted decision. This is not a second
/// choice catalogue: callers retain the original ID, affordability, review and
/// confirmation. Copy describes a possible path, never an accepted outcome.
/// </summary>
public static class OriginStoryDecisionText
{
    public static string Prompt(OriginDossierLifeModuleDecisionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        string language = Language(state);
        return state.StageOrder switch
        {
            LifeModuleJourneyStageOrders.Nationality => Pick(language,
                "Where does this life begin?", "Wo beginnt dieses Leben?", "¿Dónde comienza esta vida?"),
            LifeModuleJourneyStageOrders.FormativeYears => Pick(language,
                "What kind of childhood shapes this life?", "Welche Kindheit prägt dieses Leben?", "¿Qué infancia marca esta vida?"),
            LifeModuleJourneyStageOrders.TeenYears => Pick(language,
                "Which path beckons as childhood gives way to youth?", "Welcher Weg lockt, wenn die Kindheit zu Ende geht?", "¿Qué camino se abre al dejar atrás la infancia?"),
            LifeModuleJourneyStageOrders.FurtherEducation => Pick(language,
                "Keep studying, or find a different way into the world?", "Weiterlernen oder einen anderen Weg ins Leben suchen?", "¿Seguir estudiando o buscar otro camino en la vida?"),
            _ => Pick(language,
                "Where should the next chapter of this life lead?", "Wohin soll das nächste Kapitel dieses Lebens führen?", "¿Adónde debe llevar el próximo capítulo de esta vida?")
        };
    }

    public static string Choice(OriginDossierLifeModuleDecisionState state, string choiceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        // Resolve by the exact issued ID, not a translated label or list index.
        // A caption cannot introduce a new selectable branch.
        var choice = state.Choices.SingleOrDefault(c => c.ChoiceId == choiceId)
            ?? throw new ArgumentException("The story choice does not belong to this decision.", nameof(choiceId));
        string language = Language(state);
        if (choice.Effects.Any(effect => effect.Domain == "creation-stage"
                && effect.TargetId == CharacterCreationLifeModuleStageIds.SelectionFinished && effect.AfterValue == "true"))
            return Pick(language, "Let this past lead into a new life.",
                "Mit dieser Vergangenheit in ein neues Leben aufbrechen.", "Empezar una nueva vida con este pasado.");

        // These are authored captions, not rules or copies of sourcebook prose.
        // Match whole canonical anchors. Similar names or unknown/custom modules
        // receive a neutral label-preserving sentence, never an invented history.
        string? caption = KnownPath(state.StageOrder, choice.SourceAnchorIds, language);
        if (caption is not null)
        {
            // Keep version/subject distinctions visible: several legal choices
            // may share the same school but mean different courses of study.
            return caption + "\n" + choice.Label;
        }
        return state.StageOrder switch
        {
            LifeModuleJourneyStageOrders.Nationality => Pick(language,
                $"Let the story begin here: {choice.Label}.", $"Hier soll die Geschichte beginnen: {choice.Label}.",
                $"Que la historia comience aquí: {choice.Label}."),
            LifeModuleJourneyStageOrders.FormativeYears => Pick(language,
                $"Grow up in this world: {choice.Label}.", $"In dieser Welt aufwachsen: {choice.Label}.",
                $"Crecer en este mundo: {choice.Label}."),
            _ => Pick(language, $"Take this path: {choice.Label}.", $"Diesen Weg einschlagen: {choice.Label}.",
                $"Seguir este camino: {choice.Label}.")
        };
    }

    private static string? KnownPath(int stage, IReadOnlyList<string> anchors, string language)
    {
        // An ambiguous set of module anchors has no specific narrative caption.
        const string prefix = "lifemodules.xml#module:";
        string[] modules = anchors.Where(a => a.StartsWith(prefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (modules.Length != 1) return null;
        return (stage, modules[0][prefix.Length..]) switch
        {
            (LifeModuleJourneyStageOrders.FormativeYears, "4f078a7f-bfa5-4eba-97f9-a97f06eab6e8") => Pick(language,
                "Grow up surrounded by wealth and expectations.", "Zwischen Wohlstand und Erwartungen aufwachsen.",
                "Crecer entre privilegios y expectativas."),
            (LifeModuleJourneyStageOrders.FormativeYears, "fe3d36d3-6e0a-4d65-8516-3350f1b5b812") => Pick(language,
                "Learn to find a way through childhood on the streets.", "Schon als Kind auf der Straße einen Weg finden.",
                "Aprender desde la infancia a abrirse camino en la calle."),
            (LifeModuleJourneyStageOrders.FormativeYears, "1521128b-0b5e-41ce-a2dd-61512f2dff2c") => Pick(language,
                "Grow up in a world shaped by office life.", "In einer von Büroarbeit geprägten Welt aufwachsen.",
                "Crecer en un mundo marcado por la vida de oficina."),
            (LifeModuleJourneyStageOrders.TeenYears, "15bd4283-f287-4be7-b174-9e5ab97bda1a") => Pick(language,
                "Follow the discipline of a military school.", "Den Weg an eine Militärschule einschlagen.",
                "Seguir el camino de una escuela militar."),
            (LifeModuleJourneyStageOrders.TeenYears, "f0393b9e-2698-4955-bd31-112b619ac7b8") => Pick(language,
                "Seek a future through a corporation's education.", "Die Zukunft in der Ausbildung eines Konzerns suchen.",
                "Buscar un futuro en la educación de una corporación."),
            (LifeModuleJourneyStageOrders.TeenYears, "21c3cb79-e0d9-49b8-9ad3-9232bab4f12b") => Pick(language,
                "Find a way through youth on the streets.", "Die Jugend auf der Straße meistern.",
                "Abrirse camino en la calle durante la juventud."),
            (LifeModuleJourneyStageOrders.FurtherEducation, "9e601908-df9a-4fe1-b58e-74bfd6f5d7b5") => Pick(language,
                "Build a future at a community college.", "An einem Community College eine Zukunft aufbauen.",
                "Construir un futuro en un centro de estudios superiores local."),
            (LifeModuleJourneyStageOrders.FurtherEducation, "1156fac7-e8d2-4599-94c3-64b8464037d7") => Pick(language,
                "Pursue an education among the academic elite.", "Den Weg an eine Eliteuniversität verfolgen.",
                "Seguir una formación entre la élite académica."),
            (LifeModuleJourneyStageOrders.FurtherEducation, "142d7a1b-c676-4bf6-a71f-bc2d29617a14") => Pick(language,
                "Pursue a future at a state university.", "Die Zukunft an einer staatlichen Universität suchen.",
                "Buscar un futuro en una universidad pública."),
            (LifeModuleJourneyStageOrders.RealLife, "adeea2d5-ef7a-4852-81da-627197a31dd2") => Pick(language,
                "Try a life in the service of a corporation.", "Mein Glück im Dienst eines Konzerns versuchen.",
                "Probar suerte al servicio de una corporación."),
            _ => null
        };
    }

    private static string Language(OriginDossierLifeModuleDecisionState state)
        => OriginDossierNarrativeLocalePolicy.Resolve(state.Locale).ResourceLanguage;

    private static string Pick(string language, string en, string de, string es)
        => language switch { "de" => de, "es" => es, _ => en };
}
