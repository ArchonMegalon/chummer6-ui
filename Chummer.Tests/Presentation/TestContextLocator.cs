using System.IO;

namespace Chummer.Tests.Presentation;

internal static class TestContextLocator
{
    public static string ResolveChummerPresentationRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            string? parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
            {
                break;
            }

            if (File.Exists(Path.Combine(parent, "Chummer.slnx"))
                && Directory.Exists(Path.Combine(parent, "Chummer.Avalonia"))
                && Directory.Exists(Path.Combine(parent, "Chummer.Tests")))
            {
                return parent;
            }

            current = parent;
        }

        return "/docker/chummercomplete/chummer-presentation";
    }
}
