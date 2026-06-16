namespace Chummer.Presentation.Overview;

public static class DesktopHorizonWorkbenchCatalog
{
    public static IReadOnlyList<DesktopHorizonWorkbenchEntry> ListEntries()
        => ProductSpineCatalog.ListDesktopHorizons();

    public static IReadOnlyList<DesktopHorizonRouteOption> ListKarmaForgeTargets()
        => ProductSpineCatalog.ListKarmaForgeTargets();
}
