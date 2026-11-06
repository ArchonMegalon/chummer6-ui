namespace Chummer.Avalonia.Creation;

/// <summary>SR6 remains a qualification-only increment until the complete native journey passes.</summary>
public static class Sr6CreationWizardGatePolicy
{
    public const string SessionEnvVariable = "CHUMMER_DESKTOP_SR6_CREATION_PREVIEW";

    public static bool IsSessionEnabled()
        => string.Equals(Environment.GetEnvironmentVariable(SessionEnvVariable), "1", StringComparison.Ordinal);
}
