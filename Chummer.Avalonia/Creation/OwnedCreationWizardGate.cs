using System;

namespace Chummer.Avalonia.Creation;

/// <summary>
/// Gen6 (T795): the owned-window SR5 creation wizard ships behind an explicit
/// session opt-in until the full journey passes on the new host. Mirrors
/// <see cref="Sr6CreationWizardGatePolicy"/>'s env-gate shape.
/// </summary>
public static class OwnedCreationWizardGate
{
    public const string SessionEnvVariable = "CHUMMER_DESKTOP_OWNED_WIZARD_PREVIEW";

    public static bool IsSessionEnabled()
        => string.Equals(Environment.GetEnvironmentVariable(SessionEnvVariable), "1", StringComparison.Ordinal);
}
