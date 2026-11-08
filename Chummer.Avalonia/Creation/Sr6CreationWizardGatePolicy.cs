namespace Chummer.Avalonia.Creation;

/// <summary>
/// The SR6 creation wizard is enabled by default; set the session variable to "0"
/// to opt out and keep the legacy disclosure-only flow for qualification runs.
/// </summary>
public static class Sr6CreationWizardGatePolicy
{
    public const string SessionEnvVariable = "CHUMMER_DESKTOP_SR6_CREATION_PREVIEW";

    public static bool IsSessionEnabled()
        => !string.Equals(Environment.GetEnvironmentVariable(SessionEnvVariable), "0", StringComparison.Ordinal);
}
