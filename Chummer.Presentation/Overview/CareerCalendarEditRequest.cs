using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerCalendarEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterCareerCalendarWeekState> Weeks,
    bool CanChangeStartingDate,
    string ChangeStartingDateBlocker);

public sealed record CareerCalendarAddRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerCalendarWeekIdentity NewIdentity,
    int RequestedFirstYear,
    int RequestedFirstWeek);

public sealed record CareerCalendarEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerCalendarWeekState ExpectedWeek,
    string ExpectedSourceRevision,
    string Notes,
    string NotesColor);

public sealed record CareerCalendarDeleteRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerCalendarWeekState ExpectedWeek,
    string ExpectedSourceRevision,
    bool Confirmed);

internal static class CareerCalendarEditorProjector
{
    internal const string ChangeStartingDateBlocker =
        "Change Starting Date is unavailable because the pinned Chummer5 CalendarWeek.ModifyWeekAsync implementation ignores its requested offset.";

    public static CareerCalendarEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for calendar editing.");
        }
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing the calendar.");
        }

        return new CareerCalendarEditorState(
            workspaceId,
            contentRevision,
            ProjectState(xml),
            CanChangeStartingDate: false,
            ChangeStartingDateBlocker);
    }

    internal static IReadOnlyList<CharacterCareerCalendarWeekState> ProjectState(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = RequireCharacterRoot(document);
        if (!ReadRequiredBool(root, "created"))
        {
            throw new InvalidOperationException(
                "Calendar editing is available only for career runners.");
        }

        XElement[] calendars = root.Elements("calendar").Take(2).ToArray();
        if (calendars.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate <calendar> containers.");
        }

        List<CharacterCareerCalendarWeekState> weeks = [];
        HashSet<Guid> identities = [];
        HashSet<(int Year, int Week)> coordinates = [];
        foreach (XElement weekElement in calendars.SingleOrDefault()?.Elements("week") ?? [])
        {
            Guid id = ReadRequiredGuid(weekElement, "guid");
            int year = ReadRequiredInt(weekElement, "year");
            int week = ReadRequiredInt(weekElement, "week");
            if (!identities.Add(id))
            {
                throw new InvalidOperationException(
                    "The saved runner has duplicate calendar-week GUIDs.");
            }
            if (!coordinates.Add((year, week)))
            {
                throw new InvalidOperationException(
                    "The saved runner has duplicate calendar year/week coordinates.");
            }

            string notes = ReadOptionalText(weekElement, "notes", string.Empty);
            string notesColor = ReadOptionalText(
                weekElement,
                "notesColor",
                CharacterCareerCalendarRules.DefaultNotesColor);
            if (!CharacterCareerCalendarRules.TryCreateState(
                    new CharacterCareerCalendarWeekIdentity(id),
                    created: true,
                    year,
                    week,
                    notes,
                    notesColor,
                    weekElement.ToString(SaveOptions.DisableFormatting),
                    out CharacterCareerCalendarWeekState state))
            {
                throw new InvalidOperationException(
                    $"Calendar week {id:D} is outside Chummer5's editable bounds.");
            }
            weeks.Add(state);
        }

        return weeks
            .OrderByDescending(static candidate => candidate.Year)
            .ThenByDescending(static candidate => candidate.Week)
            .ToArray();
    }

    internal static XElement RequireCharacterRoot(XDocument document)
        => document.Root is { } root && root.Name == XName.Get("character")
            ? root
            : throw new InvalidOperationException(
                "Workspace XML must use <character> as the root node.");

    internal static bool ReadRequiredBool(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1
            || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException(
                $"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    internal static Guid ReadRequiredGuid(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1
            || !Guid.TryParse(values[0].Value.Trim(), out Guid value)
            || value == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"A calendar week has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    internal static int ReadRequiredInt(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1
            || !int.TryParse(
                values[0].Value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value))
        {
            throw new InvalidOperationException(
                $"A calendar week has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    internal static string ReadOptionalText(
        XElement parent,
        string name,
        string fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => fallback,
            1 => values[0].Value,
            _ => throw new InvalidOperationException(
                $"A calendar week has duplicate <{name}> values.")
        };
    }
}
