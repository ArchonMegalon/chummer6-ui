global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.IO;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Runtime.Loader;
global using AngleSharp.Dom;
global using Chummer;
global using Chummer.Contracts.Content;
global using Chummer.Contracts.Product;
global using Chummer.Contracts.Presentation;
global using Chummer.Rulesets.Hosting;
global using Chummer.Rulesets.Hosting.Presentation;
global using BuildLabActionDescriptor = Chummer.Contracts.Presentation.BuildLabActionDescriptor;
global using BuildLabBadge = Chummer.Contracts.Presentation.BuildLabBadge;
global using BuildLabBadgeKinds = Chummer.Contracts.Presentation.BuildLabBadgeKinds;
global using BuildLabConceptIntakeProjection = Chummer.Contracts.Presentation.BuildLabConceptIntakeProjection;
global using BuildLabExportField = Chummer.Contracts.Presentation.BuildLabExportField;
global using BuildLabExportPayload = Chummer.Contracts.Presentation.BuildLabExportPayload;
global using BuildLabExportTarget = Chummer.Contracts.Presentation.BuildLabExportTarget;
global using BuildLabExportTargetKinds = Chummer.Contracts.Presentation.BuildLabExportTargetKinds;
global using BuildLabFieldKinds = Chummer.Contracts.Presentation.BuildLabFieldKinds;
global using BuildLabFieldOption = Chummer.Contracts.Presentation.BuildLabFieldOption;
global using BuildLabIntakeField = Chummer.Contracts.Presentation.BuildLabIntakeField;
global using BuildLabProgressionStep = Chummer.Contracts.Presentation.BuildLabProgressionStep;
global using BuildLabProgressionTimeline = Chummer.Contracts.Presentation.BuildLabProgressionTimeline;
global using BuildLabSurfaceIds = Chummer.Contracts.Presentation.BuildLabSurfaceIds;
global using BuildLabTeamCoverageProjection = Chummer.Contracts.Presentation.BuildLabTeamCoverageProjection;
global using BuildLabVariantMetric = Chummer.Contracts.Presentation.BuildLabVariantMetric;
global using BuildLabVariantProjection = Chummer.Contracts.Presentation.BuildLabVariantProjection;
global using BuildLabVariantWarning = Chummer.Contracts.Presentation.BuildLabVariantWarning;
global using BuildLabWarningKinds = Chummer.Contracts.Presentation.BuildLabWarningKinds;
global using IEngineEvaluator = Chummer.Contracts.Rulesets.IRulesetCapabilityHost;

internal static class TestAssemblyResolutionBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += ResolveFromLocalOutput;
    }

    private static Assembly? ResolveFromLocalOutput(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name) ||
            !assemblyName.Name.StartsWith("Chummer.", StringComparison.Ordinal))
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
