namespace Chummer.Avalonia.Controls;

public sealed class CharacterCreateClassicPort : ClassicFormPortSurfaceControl
{
    public CharacterCreateClassicPort()
        : base(
            "character_create",
            "Character Create Classic",
            ["Priorities", "Attributes", "Skills", "Gear", "Spells", "Final"],
            "Chummer/Forms/Character Forms/CharacterCreate.Designer.cs")
    {
    }
}
