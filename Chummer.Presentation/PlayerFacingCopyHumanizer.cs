namespace Chummer.Presentation;

public static class PlayerFacingCopyHumanizer
{
    public static string Clean(string? value)
        => UndetectableHumanizerCopyAdapter.Humanize(value);

    public static string[] CleanLines(IEnumerable<string> values)
        => UndetectableHumanizerCopyAdapter.HumanizeLines(values);
}
