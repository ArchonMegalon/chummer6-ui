#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Chummer.Tests;

internal static class DesktopReleaseMatrixTestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += ResolveFromLocalOutput;
    }

    private static Assembly? ResolveFromLocalOutput(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name)
            || !assemblyName.Name.StartsWith("Chummer.", StringComparison.Ordinal))
        {
            return null;
        }

        string candidatePath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
        if (!File.Exists(candidatePath))
        {
            return null;
        }

        return context.LoadFromAssemblyPath(candidatePath);
    }
}
