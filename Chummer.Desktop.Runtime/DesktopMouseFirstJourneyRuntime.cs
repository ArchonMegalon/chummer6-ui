using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Chummer.Desktop.Runtime;

public static class DesktopMouseFirstJourneyRuntime
{
    public const string MouseFirstJourneySwitch = "--mouse-first-user-journey";
    public const string ReceiptEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT";
    public const string FailurePacketEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET";
    public const string ScreenshotDirectoryEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR";
    public const string TracePathEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE";
    public const string ArtifactDigestEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_ARTIFACT_DIGEST";
    public const string HostClassEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_HOST_CLASS";
    public const string ReleaseVersionEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RELEASE_VERSION";
    public const string RidEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RID";
    public const string ReleaseChannelEnvironmentVariable = "CHUMMER_DESKTOP_RELEASE_CHANNEL";
    public const string ScenarioIdEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCENARIO_ID";
    public const string CharacterNameEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_CHARACTER_NAME";
    public const string CharacterAliasEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_CHARACTER_ALIAS";
    public const string RulesetIdEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RULESET_ID";
    public const string BuildMethodEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_BUILD_METHOD";
    public const string MetatypeCategoryEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_METATYPE_CATEGORY";
    public const string PriorityHeritageEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_PRIORITY_HERITAGE";
    public const string MetatypeEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_METATYPE";
    public const string PriorityTalentEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_PRIORITY_TALENT";
    public const string PriorityTalentChoiceEnvironmentVariable = "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_PRIORITY_TALENT_CHOICE";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static bool ShouldRun(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(arg => string.Equals(arg, MouseFirstJourneySwitch, StringComparison.OrdinalIgnoreCase));
    }

    public static DesktopMouseFirstJourneyContext BuildContext(string headId, DateTimeOffset startedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        string processPath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        (string? artifactDigest, string artifactDigestSource) = ResolveArtifactDigest(processPath);
        string resolvedVersion = ResolveVersion(assembly);
        string? screenshotDirectory = Environment.GetEnvironmentVariable(ScreenshotDirectoryEnvironmentVariable);
        string? tracePath = Environment.GetEnvironmentVariable(TracePathEnvironmentVariable);
        return new DesktopMouseFirstJourneyContext(
            HeadId: ReadAssemblyMetadata(assembly, "ChummerDesktopHeadId") ?? headId,
            Version: resolvedVersion,
            ReleaseVersion: ResolveReleaseVersion(assembly, resolvedVersion),
            ChannelId: ResolveChannelId(assembly),
            Platform: DetectPlatform(),
            Arch: DetectArchitecture(),
            Rid: ResolveRid(),
            HostClass: Environment.GetEnvironmentVariable(HostClassEnvironmentVariable) ?? Environment.MachineName,
            ProcessPath: processPath,
            ArtifactDigest: artifactDigest,
            ArtifactDigestSource: artifactDigestSource,
            Framework: RuntimeInformation.FrameworkDescription,
            OperatingSystem: RuntimeInformation.OSDescription,
            StartedAtUtc: startedAtUtc,
            ReceiptPath: Environment.GetEnvironmentVariable(ReceiptEnvironmentVariable),
            FailurePacketPath: Environment.GetEnvironmentVariable(FailurePacketEnvironmentVariable),
            ScreenshotDirectory: string.IsNullOrWhiteSpace(screenshotDirectory) ? null : screenshotDirectory,
            TracePath: string.IsNullOrWhiteSpace(tracePath) ? null : tracePath);
    }

    public static DesktopMouseFirstJourneyPlan ReadPlan()
    {
        string rulesetId = NormalizeOptional(
            Environment.GetEnvironmentVariable(RulesetIdEnvironmentVariable),
            "sr5");
        string buildMethod = NormalizeBuildMethod(Environment.GetEnvironmentVariable(BuildMethodEnvironmentVariable));
        string characterName = NormalizeOptional(
            Environment.GetEnvironmentVariable(CharacterNameEnvironmentVariable),
            "Mouse Journey Runner");
        string characterAlias = NormalizeOptional(
            Environment.GetEnvironmentVariable(CharacterAliasEnvironmentVariable),
            "MouseRoute");
        string? metatypeCategory = NormalizeNullable(Environment.GetEnvironmentVariable(MetatypeCategoryEnvironmentVariable));
        string? priorityHeritage = NormalizePriorityLetter(Environment.GetEnvironmentVariable(PriorityHeritageEnvironmentVariable));
        string? metatype = NormalizeNullable(Environment.GetEnvironmentVariable(MetatypeEnvironmentVariable));
        string? priorityTalent = NormalizePriorityLetter(Environment.GetEnvironmentVariable(PriorityTalentEnvironmentVariable));
        string? priorityTalentChoice = NormalizeNullable(Environment.GetEnvironmentVariable(PriorityTalentChoiceEnvironmentVariable));

        if (!string.Equals(buildMethod, "Priority", StringComparison.OrdinalIgnoreCase))
        {
            priorityHeritage = null;
            priorityTalent = null;
            priorityTalentChoice = null;
        }

        string scenarioId = NormalizeOptional(
            Environment.GetEnvironmentVariable(ScenarioIdEnvironmentVariable),
            BuildScenarioId(rulesetId, buildMethod, metatypeCategory, priorityHeritage, metatype, priorityTalentChoice));

        return new DesktopMouseFirstJourneyPlan(
            ScenarioId: scenarioId,
            CharacterName: characterName,
            CharacterAlias: characterAlias,
            RulesetId: rulesetId,
            BuildMethod: buildMethod,
            MetatypeCategory: metatypeCategory,
            PriorityHeritage: priorityHeritage,
            Metatype: metatype,
            PriorityTalent: priorityTalent,
            PriorityTalentChoice: priorityTalentChoice);
    }

    public static void WriteSuccessReceipt(
        DesktopMouseFirstJourneyContext context,
        DesktopMouseFirstJourneyEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(evidence);

        if (string.IsNullOrWhiteSpace(context.ReceiptPath))
        {
            return;
        }

        DesktopMouseFirstJourneyReceipt receipt = new(
            Status: "pass",
            JourneyMode: "mouse_first_live_binary",
            HeadId: context.HeadId,
            Version: context.Version,
            ReleaseVersion: context.ReleaseVersion,
            ChannelId: context.ChannelId,
            Platform: context.Platform,
            Arch: context.Arch,
            Rid: context.Rid,
            HostClass: context.HostClass,
            ScenarioId: evidence.ScenarioId,
            ProcessPath: context.ProcessPath,
            ArtifactDigest: context.ArtifactDigest,
            ArtifactDigestSource: context.ArtifactDigestSource,
            Framework: context.Framework,
            OperatingSystem: context.OperatingSystem,
            StartedAtUtc: context.StartedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            RecordedAtUtc: DateTimeOffset.UtcNow,
            ScreenshotDirectory: context.ScreenshotDirectory,
            TracePath: context.TracePath,
            Steps: evidence.Steps,
            ScreenshotPaths: evidence.ScreenshotPaths,
            PointerActionCount: evidence.PointerActionCount,
            TextEntryActionCount: evidence.TextEntryActionCount,
            DirectTextMutationCount: evidence.DirectTextMutationCount,
            UsedForcedComboDropdownOpen: evidence.UsedForcedComboDropdownOpen,
            UsedComboSelectionFallback: evidence.UsedComboSelectionFallback,
            ObservedInputEvents: evidence.ObservedInputEvents,
            WorkspaceId: evidence.WorkspaceId,
            CharacterName: evidence.CharacterName,
            CharacterAlias: evidence.CharacterAlias,
            RulesetId: evidence.RulesetId,
            BuildMethod: evidence.BuildMethod,
            MetatypeCategory: evidence.MetatypeCategory,
            PriorityHeritage: evidence.PriorityHeritage,
            Metatype: evidence.Metatype,
            PriorityTalent: evidence.PriorityTalent,
            PriorityTalentChoice: evidence.PriorityTalentChoice,
            HasSavedWorkspace: evidence.HasSavedWorkspace,
            AuthenticationPortalOpened: evidence.AuthenticationPortalOpened,
            AuthenticationPortalUri: evidence.AuthenticationPortalUri,
            ActiveDialogId: evidence.ActiveDialogId,
            VerificationNotes: evidence.VerificationNotes,
            Error: null);
        WriteJson(context.ReceiptPath, receipt);
    }

    public static void WriteFailureArtifacts(
        DesktopMouseFirstJourneyContext context,
        Exception ex,
        IReadOnlyList<string> steps,
        IReadOnlyList<string>? screenshotPaths = null,
        int pointerActionCount = 0,
        int textEntryActionCount = 0,
        int directTextMutationCount = 0,
        bool usedForcedComboDropdownOpen = false,
        bool usedComboSelectionFallback = false,
        IReadOnlyList<DesktopMouseFirstJourneyObservedInputEvent>? observedInputEvents = null,
        string? scenarioId = null,
        string? buildMethod = null,
        string? metatypeCategory = null,
        string? priorityHeritage = null,
        string? metatype = null,
        string? priorityTalent = null,
        string? priorityTalentChoice = null,
        string? activeDialogId = null,
        string? workspaceId = null,
        bool authenticationPortalOpened = false,
        string? authenticationPortalUri = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(steps);

        if (!string.IsNullOrWhiteSpace(context.ReceiptPath))
        {
            DesktopMouseFirstJourneyReceipt receipt = new(
                Status: "fail",
                JourneyMode: "mouse_first_live_binary",
                HeadId: context.HeadId,
                Version: context.Version,
                ReleaseVersion: context.ReleaseVersion,
                ChannelId: context.ChannelId,
                Platform: context.Platform,
                Arch: context.Arch,
                Rid: context.Rid,
                HostClass: context.HostClass,
                ScenarioId: scenarioId,
                ProcessPath: context.ProcessPath,
                ArtifactDigest: context.ArtifactDigest,
                ArtifactDigestSource: context.ArtifactDigestSource,
                Framework: context.Framework,
                OperatingSystem: context.OperatingSystem,
                StartedAtUtc: context.StartedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                RecordedAtUtc: DateTimeOffset.UtcNow,
                ScreenshotDirectory: context.ScreenshotDirectory,
                TracePath: context.TracePath,
                Steps: steps,
                ScreenshotPaths: screenshotPaths ?? [],
                PointerActionCount: pointerActionCount,
                TextEntryActionCount: textEntryActionCount,
                DirectTextMutationCount: directTextMutationCount,
                UsedForcedComboDropdownOpen: usedForcedComboDropdownOpen,
                UsedComboSelectionFallback: usedComboSelectionFallback,
                ObservedInputEvents: observedInputEvents ?? [],
                WorkspaceId: workspaceId,
                CharacterName: null,
                CharacterAlias: null,
                RulesetId: null,
                BuildMethod: buildMethod,
                MetatypeCategory: metatypeCategory,
                PriorityHeritage: priorityHeritage,
                Metatype: metatype,
                PriorityTalent: priorityTalent,
                PriorityTalentChoice: priorityTalentChoice,
                HasSavedWorkspace: false,
                AuthenticationPortalOpened: authenticationPortalOpened,
                AuthenticationPortalUri: authenticationPortalUri,
                ActiveDialogId: activeDialogId,
                VerificationNotes: [],
                Error: ex.Message);
            WriteJson(context.ReceiptPath, receipt);
        }

        if (string.IsNullOrWhiteSpace(context.FailurePacketPath))
        {
            return;
        }

        DesktopMouseFirstJourneyFailurePacket packet = new(
            SignalClass: "desktop_mouse_first_journey_failure",
            HeadId: context.HeadId,
            Platform: context.Platform,
            Rid: context.Rid,
            ArtifactDigest: context.ArtifactDigest,
            ArtifactDigestSource: context.ArtifactDigestSource,
            StartedAtUtc: context.StartedAtUtc,
            RecordedAtUtc: DateTimeOffset.UtcNow,
            ScenarioId: scenarioId,
            BuildMethod: buildMethod,
            MetatypeCategory: metatypeCategory,
            PriorityHeritage: priorityHeritage,
            Metatype: metatype,
            PriorityTalent: priorityTalent,
            PriorityTalentChoice: priorityTalentChoice,
            Error: ex.ToString(),
            ActiveDialogId: activeDialogId,
            WorkspaceId: workspaceId,
            PointerActionCount: pointerActionCount,
            TextEntryActionCount: textEntryActionCount,
            DirectTextMutationCount: directTextMutationCount,
            UsedForcedComboDropdownOpen: usedForcedComboDropdownOpen,
            UsedComboSelectionFallback: usedComboSelectionFallback,
            ObservedInputEvents: observedInputEvents ?? [],
            AuthenticationPortalOpened: authenticationPortalOpened,
            AuthenticationPortalUri: authenticationPortalUri,
            Steps: steps);
        WriteJson(context.FailurePacketPath, packet);
    }

    public static void WriteObservedInputTrace(
        DesktopMouseFirstJourneyContext context,
        IReadOnlyList<DesktopMouseFirstJourneyObservedInputEvent> observedInputEvents)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observedInputEvents);

        if (string.IsNullOrWhiteSpace(context.TracePath))
        {
            return;
        }

        WriteJson(
            context.TracePath,
            new DesktopMouseFirstJourneyObservedInputTrace(
                HeadId: context.HeadId,
                Platform: context.Platform,
                Rid: context.Rid,
                HostClass: context.HostClass,
                ProcessPath: context.ProcessPath,
                RecordedAtUtc: DateTimeOffset.UtcNow,
                ObservedInputEvents: observedInputEvents));
    }

    private static string DetectPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        return RuntimeInformation.OSDescription;
    }

    private static string DetectArchitecture()
        => RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };

    private static (string? ArtifactDigest, string ArtifactDigestSource) ResolveArtifactDigest(string processPath)
    {
        string? configuredDigest = Environment.GetEnvironmentVariable(ArtifactDigestEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredDigest))
        {
            return (NormalizeSha256Digest(configuredDigest), "environment");
        }

        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
        {
            return (null, "unavailable");
        }

        try
        {
            using FileStream stream = File.OpenRead(processPath);
            using SHA256 sha256 = SHA256.Create();
            return ($"sha256:{Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant()}", "process_path");
        }
        catch
        {
            return (null, "unavailable");
        }
    }

    private static string NormalizeSha256Digest(string value)
    {
        string trimmed = value.Trim();
        return trimmed.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? $"sha256:{trimmed[7..].Trim().ToLowerInvariant()}"
            : $"sha256:{trimmed.ToLowerInvariant()}";
    }

    private static string? ReadAssemblyMetadata(Assembly assembly, string key)
    {
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?
            .Value;
    }

    private static string ResolveChannelId(Assembly assembly)
    {
        string? overrideChannel = Environment.GetEnvironmentVariable(ReleaseChannelEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideChannel))
        {
            return overrideChannel.Trim();
        }

        return ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseChannel") ?? "local";
    }

    private static string ResolveVersion(Assembly assembly)
    {
        string? overrideVersion = Environment.GetEnvironmentVariable(ReleaseVersionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideVersion))
        {
            return overrideVersion.Trim();
        }

        return ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseVersion")
            ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? string.Empty;
    }

    private static string ResolveReleaseVersion(Assembly assembly, string fallbackVersion)
    {
        string? overrideVersion = Environment.GetEnvironmentVariable(ReleaseVersionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideVersion))
        {
            return overrideVersion.Trim();
        }

        string? metadataVersion = ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseVersion");
        if (!string.IsNullOrWhiteSpace(metadataVersion))
        {
            return metadataVersion;
        }

        return fallbackVersion;
    }

    private static string ResolveRid()
    {
        string? overrideRid = Environment.GetEnvironmentVariable(RidEnvironmentVariable);
        return string.IsNullOrWhiteSpace(overrideRid) ? string.Empty : overrideRid.Trim().ToLowerInvariant();
    }

    private static string NormalizeOptional(string? value, string fallback)
    {
        string? normalized = NormalizeNullable(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NormalizeBuildMethod(string? value)
    {
        string normalized = NormalizeOptional(value, "Priority");
        if (normalized.Equals("karma", StringComparison.OrdinalIgnoreCase))
        {
            return "Karma";
        }

        if (normalized.Equals("bp", StringComparison.OrdinalIgnoreCase))
        {
            return "BP";
        }

        return normalized.Equals("priority", StringComparison.OrdinalIgnoreCase)
            ? "Priority"
            : normalized;
    }

    private static string? NormalizePriorityLetter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim().ToUpperInvariant();
        return normalized is "A" or "B" or "C" or "D" or "E"
            ? normalized
            : null;
    }

    private static string BuildScenarioId(
        string rulesetId,
        string buildMethod,
        string? metatypeCategory,
        string? priorityHeritage,
        string? metatype,
        string? priorityTalentChoice)
    {
        List<string> parts =
        [
            NormalizeOptional(rulesetId, "sr5").ToLowerInvariant(),
            NormalizeOptional(buildMethod, "Priority").ToLowerInvariant()
        ];

        if (!string.IsNullOrWhiteSpace(metatypeCategory))
        {
            parts.Add(Slugify(metatypeCategory));
        }

        if (!string.IsNullOrWhiteSpace(priorityHeritage))
        {
            parts.Add(priorityHeritage.ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(metatype))
        {
            parts.Add(Slugify(metatype));
        }

        if (!string.IsNullOrWhiteSpace(priorityTalentChoice))
        {
            parts.Add(Slugify(priorityTalentChoice));
        }

        return string.Join("-", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string Slugify(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        int index = 0;
        bool lastWasDash = false;
        foreach (char character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[index++] = character;
                lastWasDash = false;
                continue;
            }

            if (!lastWasDash && index > 0)
            {
                buffer[index++] = '-';
                lastWasDash = true;
            }
        }

        return new string(buffer[..index]).Trim('-');
    }

    private static void WriteJson<T>(string path, T payload)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(payload, JsonOptions);
        File.WriteAllText(path, json + Environment.NewLine);
    }
}

public sealed record DesktopMouseFirstJourneyContext(
    string HeadId,
    string Version,
    string ReleaseVersion,
    string ChannelId,
    string Platform,
    string Arch,
    string Rid,
    string HostClass,
    string ProcessPath,
    string? ArtifactDigest,
    string ArtifactDigestSource,
    string Framework,
    string OperatingSystem,
    DateTimeOffset StartedAtUtc,
    string? ReceiptPath,
    string? FailurePacketPath,
    string? ScreenshotDirectory,
    string? TracePath);

public sealed record DesktopMouseFirstJourneyEvidence(
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> ScreenshotPaths,
    int PointerActionCount,
    int TextEntryActionCount,
    int DirectTextMutationCount,
    bool UsedForcedComboDropdownOpen,
    bool UsedComboSelectionFallback,
    IReadOnlyList<DesktopMouseFirstJourneyObservedInputEvent> ObservedInputEvents,
    string ScenarioId,
    string? WorkspaceId,
    string? CharacterName,
    string? CharacterAlias,
    string? RulesetId,
    string BuildMethod,
    string? MetatypeCategory,
    string? PriorityHeritage,
    string? Metatype,
    string? PriorityTalent,
    string? PriorityTalentChoice,
    bool HasSavedWorkspace,
    bool AuthenticationPortalOpened,
    string? AuthenticationPortalUri,
    string? ActiveDialogId,
    IReadOnlyList<string> VerificationNotes);

public sealed record DesktopMouseFirstJourneyReceipt(
    string Status,
    string JourneyMode,
    string HeadId,
    string Version,
    string ReleaseVersion,
    string ChannelId,
    string Platform,
    string Arch,
    string Rid,
    string HostClass,
    string? ScenarioId,
    string ProcessPath,
    string? ArtifactDigest,
    string ArtifactDigestSource,
    string Framework,
    string OperatingSystem,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset RecordedAtUtc,
    string? ScreenshotDirectory,
    string? TracePath,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> ScreenshotPaths,
    int PointerActionCount,
    int TextEntryActionCount,
    int DirectTextMutationCount,
    bool UsedForcedComboDropdownOpen,
    bool UsedComboSelectionFallback,
    IReadOnlyList<DesktopMouseFirstJourneyObservedInputEvent> ObservedInputEvents,
    string? WorkspaceId,
    string? CharacterName,
    string? CharacterAlias,
    string? RulesetId,
    string? BuildMethod,
    string? MetatypeCategory,
    string? PriorityHeritage,
    string? Metatype,
    string? PriorityTalent,
    string? PriorityTalentChoice,
    bool HasSavedWorkspace,
    bool AuthenticationPortalOpened,
    string? AuthenticationPortalUri,
    string? ActiveDialogId,
    IReadOnlyList<string> VerificationNotes,
    string? Error);

public sealed record DesktopMouseFirstJourneyFailurePacket(
    string SignalClass,
    string HeadId,
    string Platform,
    string Rid,
    string? ArtifactDigest,
    string ArtifactDigestSource,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset RecordedAtUtc,
    string? ScenarioId,
    string? BuildMethod,
    string? MetatypeCategory,
    string? PriorityHeritage,
    string? Metatype,
    string? PriorityTalent,
    string? PriorityTalentChoice,
    string Error,
    string? ActiveDialogId,
    string? WorkspaceId,
    int PointerActionCount,
    int TextEntryActionCount,
    int DirectTextMutationCount,
    bool UsedForcedComboDropdownOpen,
    bool UsedComboSelectionFallback,
    bool AuthenticationPortalOpened,
    string? AuthenticationPortalUri,
    IReadOnlyList<DesktopMouseFirstJourneyObservedInputEvent> ObservedInputEvents,
    IReadOnlyList<string> Steps);

public sealed record DesktopMouseFirstJourneyObservedInputTrace(
    string HeadId,
    string Platform,
    string Rid,
    string HostClass,
    string ProcessPath,
    DateTimeOffset RecordedAtUtc,
    IReadOnlyList<DesktopMouseFirstJourneyObservedInputEvent> ObservedInputEvents);

public sealed record DesktopMouseFirstJourneyObservedInputEvent(
    string EventKind,
    string ControlType,
    string? ControlName,
    string? ControlTag,
    string? DialogId,
    DateTimeOffset RecordedAtUtc);

public sealed record DesktopMouseFirstJourneyPlan(
    string ScenarioId,
    string CharacterName,
    string CharacterAlias,
    string RulesetId,
    string BuildMethod,
    string? MetatypeCategory,
    string? PriorityHeritage,
    string? Metatype,
    string? PriorityTalent,
    string? PriorityTalentChoice);
