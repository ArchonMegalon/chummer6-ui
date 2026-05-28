namespace Chummer.Avalonia.Controls;

public sealed class CharacterCareerClassicPort : ClassicFormPortSurfaceControl
{
    public CharacterCareerClassicPort()
        : base(
            "character_career",
            "Character Career Classic",
            ["Character", "Gear", "Armor", "Weapons", "Contacts", "Notes"],
            "Chummer/Forms/Character Forms/CharacterCareer.Designer.cs")
    {
    }
}
