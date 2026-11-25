using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace Chummer.Avalonia;

internal sealed record AmendedRunnerExportCheck(
    int Karma, bool Magician, bool Adept, string? Tradition, int SpellCount, int SustainedDurationCount,
    bool PortableProfileMatches, bool PortableBalancesMatch, bool PortablePayloadMatches)
{
    internal static AmendedRunnerExportCheck Read(string savedPath, string exportPath)
    {
        // Read the actual bytes so a declaration that contradicts the file encoding fails.
        using var savedFile = File.OpenRead(savedPath);
        XElement character = XDocument.Load(savedFile, LoadOptions.PreserveWhitespace).Root
            ?? throw new InvalidDataException("Missing character in the Save As file.");
        if (character.Name != "character")
            throw new InvalidDataException("Unexpected Save As document type.");
        int karma = int.Parse(character.Element("karma")!.Value, CultureInfo.InvariantCulture);
        bool magician = bool.Parse(character.Element("magician")!.Value);
        bool adept = bool.Parse(character.Element("adept")!.Value);
        var spells = character.Element("spells")!.Elements("spell").ToArray();

        using var exportFile = File.OpenRead(exportPath);
        using var export = JsonDocument.Parse(exportFile);
        JsonElement root = export.RootElement;
        JsonElement profile = root.GetProperty("Profile");
        JsonElement native = root.GetProperty("NativeWorkspace");
        XElement embedded = XElement.Parse(native.GetProperty("Payload").GetString()
            ?? throw new InvalidDataException("Missing native character payload."), LoadOptions.PreserveWhitespace);

        return new(karma, magician, adept, character.Element("tradition")?.Value, spells.Length,
            spells.Count(spell => spell.Element("duration")?.Value == "S"),
            profile.GetProperty("Magician").GetBoolean() == magician && profile.GetProperty("Adept").GetBoolean() == adept,
            root.GetProperty("Summary").GetProperty("Karma").GetInt32() == karma
                && root.GetProperty("Progress").GetProperty("Karma").GetInt32() == karma,
            native.GetProperty("RulesetId").GetString() == "sr5"
                && native.GetProperty("PayloadKind").GetString() == "sr5/chum5-xml"
                && native.GetProperty("SchemaVersion").GetInt32() == 1 && XNode.DeepEquals(character, embedded));
    }
}
