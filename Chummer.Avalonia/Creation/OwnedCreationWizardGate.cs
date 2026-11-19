using System;

namespace Chummer.Avalonia.Creation;

/// <summary>
/// Gen6 (T795 -> wave 4 W6): the owned-window creation wizard is the DELIVERED
/// default host. The session environment variable becomes an OPT-OUT: setting
/// CHUMMER_DESKTOP_OWNED_WIZARD_PREVIEW=0 restores the inline center surface
/// (diagnostics and A/B comparison only). Any value other than "0" (or unset)
/// keeps the owned route enabled.
/// </summary>
public static class OwnedCreationWizardGate
{
    public const string SessionEnvVariable = "CHUMMER_DESKTOP_OWNED_WIZARD_PREVIEW";

    public static bool IsSessionEnabled()
        => !string.Equals(Environment.GetEnvironmentVariable(SessionEnvVariable), "0", StringComparison.Ordinal);
}
