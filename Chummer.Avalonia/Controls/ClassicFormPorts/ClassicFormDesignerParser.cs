using System.Text.RegularExpressions;

namespace Chummer.Avalonia.Controls;

internal static partial class ClassicFormDesignerParser
{
    public static ClassicFormDesignerSnapshot Parse(string relativeDesignerPath)
    {
        string? sourcePath = ResolveDesignerPath(relativeDesignerPath);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new ClassicFormDesignerSnapshot(
                relativeDesignerPath,
                Exists: false,
                Controls: [],
                RootControls: [],
                Tabs: [],
                Groups: [],
                ToolStrips: [],
                ContextMenus: [],
                EventHandlers: []);
        }

        string text = File.ReadAllText(sourcePath);
        string[] controls = AssignmentRegex()
            .Matches(text)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        string[] roots = RootControlRegex()
            .Matches(text)
            .Select(match => match.Groups["child"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        string[] tabs = TabRegex()
            .Matches(text)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        string[] groups = GroupRegex()
            .Matches(text)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        string[] toolStrips = ToolStripRegex()
            .Matches(text)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        string[] contextMenus = ContextMenuRegex()
            .Matches(text)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        ClassicFormEventHandlerSnapshot[] handlers = EventHandlerRegex()
            .Matches(text)
            .Select(match => new ClassicFormEventHandlerSnapshot(
                match.Groups["control"].Value,
                match.Groups["event"].Value,
                match.Groups["handler"].Value))
            .Distinct()
            .Take(80)
            .ToArray();

        return new ClassicFormDesignerSnapshot(
            sourcePath,
            Exists: true,
            Controls: controls,
            RootControls: roots,
            Tabs: tabs,
            Groups: groups,
            ToolStrips: toolStrips,
            ContextMenus: contextMenus,
            EventHandlers: handlers);
    }

    private static string? ResolveDesignerPath(string relativeDesignerPath)
    {
        string normalized = relativeDesignerPath.Replace('\\', Path.DirectorySeparatorChar);
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, normalized),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", normalized)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", normalized)),
            Path.Combine(Directory.GetCurrentDirectory(), normalized),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    [GeneratedRegex(@"this\.(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*new\s+", RegexOptions.Multiline)]
    private static partial Regex AssignmentRegex();

    [GeneratedRegex(@"this\.Controls\.Add\(this\.(?<child>[A-Za-z_][A-Za-z0-9_]*)\)", RegexOptions.Multiline)]
    private static partial Regex RootControlRegex();

    [GeneratedRegex(@"this\.(?<name>tab[A-Za-z0-9_]+)\s*=\s*new\s+System\.Windows\.Forms\.TabPage", RegexOptions.Multiline)]
    private static partial Regex TabRegex();

    [GeneratedRegex(@"this\.(?<name>(grp|gpb|gbp)[A-Za-z0-9_]+)\s*=\s*new\s+System\.Windows\.Forms\.GroupBox", RegexOptions.Multiline)]
    private static partial Regex GroupRegex();

    [GeneratedRegex(@"this\.(?<name>[A-Za-z_][A-Za-z0-9_]*?(ToolStrip|MenuStrip|StatusStrip|tsMain|StatusStrip))\s*=\s*new\s+", RegexOptions.Multiline)]
    private static partial Regex ToolStripRegex();

    [GeneratedRegex(@"this\.(?<name>cms[A-Za-z0-9_]+)\s*=\s*new\s+System\.Windows\.Forms\.ContextMenuStrip", RegexOptions.Multiline)]
    private static partial Regex ContextMenuRegex();

    [GeneratedRegex(@"this\.(?<control>[A-Za-z_][A-Za-z0-9_]*)\.(?<event>[A-Za-z_][A-Za-z0-9_]*)\s*\+=\s*new\s+[A-Za-z0-9_\.<>]+\(\s*this\.(?<handler>[A-Za-z_][A-Za-z0-9_]*)\s*\)", RegexOptions.Multiline)]
    private static partial Regex EventHandlerRegex();
}

internal sealed record ClassicFormDesignerSnapshot(
    string SourcePath,
    bool Exists,
    IReadOnlyList<string> Controls,
    IReadOnlyList<string> RootControls,
    IReadOnlyList<string> Tabs,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> ToolStrips,
    IReadOnlyList<string> ContextMenus,
    IReadOnlyList<ClassicFormEventHandlerSnapshot> EventHandlers);

internal sealed record ClassicFormEventHandlerSnapshot(
    string Control,
    string EventName,
    string HandlerName);
