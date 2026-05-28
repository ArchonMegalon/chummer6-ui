namespace Chummer.Avalonia.Controls;

public sealed class MasterIndexClassicPort : ClassicFormPortSurfaceControl
{
    public MasterIndexClassicPort()
        : base(
            "master_index",
            "Master Index Classic",
            ["Browse", "Search", "Source"],
            "Chummer/Forms/Utility Forms/MasterIndex.Designer.cs")
    {
    }
}
