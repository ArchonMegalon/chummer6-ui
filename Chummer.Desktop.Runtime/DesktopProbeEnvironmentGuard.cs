namespace Chummer.Desktop.Runtime;

/// <summary>
/// Windows persistence-probe environment isolation. The two-process probe requires that a head
/// launched under it resolves its workspace store inside the probe-provided
/// <c>CHUMMER_DESKTOP_STATE_ROOT</c> and runs the local in-process client — regardless of what
/// hostile variables the invoking shell leaked in. Scopes the inherited environment before the
/// desktop host builds its DI container.
/// </summary>
public static class DesktopProbeEnvironmentGuard
{
    /// <summary>The opt-in probe command-line switch that arms the guard.</summary>
    public const string ProbeArgument = "--windows-persistence-probe";

    /// <summary>
    /// Variables that steer store resolution or client selection and therefore must not leak into
    /// a probe head from an invoking shell: an inherited store path would redirect saves outside
    /// the probe's isolated state root, and an inherited client mode would flip the head off the
    /// local in-process client onto an HTTP client (or vice versa).
    /// </summary>
    private static readonly string[] HostileVariableNames =
    {
        "CHUMMER_STATE_PATH",
        "CHUMMER_CLIENT_MODE",
        "CHUMMER_DESKTOP_CLIENT_MODE",
        "CHUMMER_API_BASE_URL",
        "CHUMMER_API_KEY",
    };

    /// <summary>
    /// Returns true when the given command line invokes the Windows persistence probe. The guard
    /// only scopes the environment on this opt-in surface; ordinary desktop launches keep their
    /// inherited environment so scripted/orchestrated sessions remain supported.
    /// </summary>
    public static bool IsProbeCommandLine(IReadOnlyList<string>? commandLine)
    {
        if (commandLine is null)
        {
            return false;
        }

        foreach (string argument in commandLine)
        {
            if (string.Equals(argument, ProbeArgument, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clears every hostile variable that is actually present in the current process environment
    /// and returns the names that were removed (in input order; absent variables are skipped so
    /// the caller can distinguish "scoped N inherited values" from "environment was clean").
    /// Comparison is ordinal case-insensitive to match Win32 environment semantics.
    /// </summary>
    public static IReadOnlyList<string> Scope(IReadOnlyCollection<string> variables)
    {
        List<string> scoped = new();
        if (variables is null || variables.Count == 0)
        {
            return scoped;
        }

        foreach (string name in variables)
        {
            if (string.IsNullOrEmpty(name) || !IsHostile(name))
            {
                continue;
            }

            string? value = Environment.GetEnvironmentVariable(name);
            if (value is null)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(name, null);
            scoped.Add(name);
        }

        LastScopedVariables = scoped.ToArray();
        return scoped;
    }

    /// <summary>The full hostile-variable list, exposed for tests and diagnostics.</summary>
    public static IReadOnlyList<string> HostileVariables => HostileVariableNames;

    /// <summary>
    /// The hostile variables the most recent <see cref="Scope"/> call actually removed from this
    /// process. The guard cleans the environment before the DI host builds (so resolution can no
    /// longer be steered), and this record preserves the fact that the invoking environment was
    /// hostile — the persistence probe reads it and refuses to emit evidence from such a run.
    /// </summary>
    public static IReadOnlyList<string> LastScopedVariables { get; private set; }
        = Array.Empty<string>();

    private static bool IsHostile(string name)
    {
        foreach (string hostileName in HostileVariableNames)
        {
            if (string.Equals(name, hostileName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
