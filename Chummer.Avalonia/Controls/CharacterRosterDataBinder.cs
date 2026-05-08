using System.Collections.ObjectModel;
using System.Linq;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Data binder for CharacterRosterControl.
/// Converts workspace state to Chummer5a-compatible treCharacterList structure.
/// </summary>
public static class CharacterRosterDataBinder
{
    /// <summary>
    /// Create roster nodes from workspace state matching Chummer5a treCharacterList structure.
    /// </summary>
    public static ObservableCollection<CharacterRosterNode> CreateRosterNodes(
        System.Collections.Generic.IReadOnlyList<OpenWorkspaceState>? workspaces)
    {
        if (workspaces == null || workspaces.Count == 0)
            return new ObservableCollection<CharacterRosterNode>();

        var nodes = new ObservableCollection<CharacterRosterNode>();

        foreach (var workspace in workspaces)
        {
            nodes.Add(new CharacterRosterNode
            {
                Id = workspace.Id.Value,
                Name = workspace.Name,
                Meta = workspace.Alias,
                Initials = GetInitials(workspace.Name)
            });
        }

        return nodes;
    }

    /// <summary>
    /// Create roster nodes from character profile data.
    /// </summary>
    public static CharacterRosterNode CreateCharacterNode(
        string characterId,
        string characterName,
        string? alias,
        string? metatype,
        string? careerKarma)
    {
        var metaParts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(metatype))
            metaParts.Add(metatype);
        if (!string.IsNullOrWhiteSpace(careerKarma))
            metaParts.Add($"Karma: {careerKarma}");

        string? meta = metaParts.Count > 0 ? string.Join(" | ", metaParts) : alias;

        return new CharacterRosterNode
        {
            Id = characterId,
            Name = characterName,
            Meta = meta,
            Initials = GetInitials(characterName)
        };
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return name.Substring(0, Math.Min(1, name.Length)).ToUpper();

        var initials = string.Concat(parts.Take(2).Select(p => p[0]));
        return initials.ToUpper().Substring(0, Math.Min(2, initials.Length));
    }
}
