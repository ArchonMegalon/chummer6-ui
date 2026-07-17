using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chummer.Desktop.Runtime;

public static class DesktopMouseFirstJourneyRuntime
{
    private const string PortableProcessPathDisclosure = "file_name_only";
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
    public const string UserJourneyTraceOutputEnvironmentVariable = "CHUMMER_DESKTOP_USER_JOURNEY_TRACE_OUTPUT";
    public const string UserJourneyTesterShardIdEnvironmentVariable = "CHUMMER_DESKTOP_USER_JOURNEY_TESTER_SHARD_ID";
    public const string UserJourneyFixShardIdEnvironmentVariable = "CHUMMER_DESKTOP_USER_JOURNEY_FIX_SHARD_ID";

    private const string UserJourneyTraceContractName = "chummer6-ui.user_journey_tester_trace";
    private const string CanonicalUserJourneyTraceFileName = "USER_JOURNEY_TESTER_TRACE.generated.json";
    private static readonly IReadOnlyDictionary<string, string[]> RequiredUserJourneyAssertions =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["master_index_search_focus_stability"] =
            [
                "focus_preserved_after_typing",
                "search_text_accumulates_keyboard_input"
            ],
            ["file_new_character_visible_workspace"] =
            [
                "new_character_action_opened_visible_workspace",
                "visible_workspace_nonblank",
                "starter_attributes_match_seeded_workspace",
                "section_preview_omits_review_copy"
            ],
            ["minimal_character_build_save_reload"] =
            [
                "character_created_saved_reloaded",
                "reload_preserved_character_identity"
            ],
            ["major_navigation_sanity"] =
            [
                "primary_navigation_clicks_change_visible_content",
                "no_unhandled_errors"
            ],
            ["validation_or_export_smoke"] =
            [
                "validation_or_export_action_completed",
                "result_visible_or_file_created"
            ]
        };

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

        Assembly assembly = ResolveDesktopAssembly();
        string hostProcessPath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        (string? artifactDigest, string artifactDigestSource) = ResolveArtifactDigest(hostProcessPath);
        string resolvedVersion = ResolveVersion(assembly);
        string? screenshotDirectory = Environment.GetEnvironmentVariable(ScreenshotDirectoryEnvironmentVariable);
        string? tracePath = Environment.GetEnvironmentVariable(TracePathEnvironmentVariable);
        string? userJourneyTraceOutputPath = Environment.GetEnvironmentVariable(UserJourneyTraceOutputEnvironmentVariable);
        return new DesktopMouseFirstJourneyContext(
            HeadId: ReadAssemblyMetadata(assembly, "ChummerDesktopHeadId") ?? headId,
            Version: resolvedVersion,
            ReleaseVersion: ResolveReleaseVersion(assembly, resolvedVersion),
            ChannelId: ResolveChannelId(assembly),
            Platform: DetectPlatform(),
            Arch: DetectArchitecture(),
            Rid: ResolveRid(),
            HostClass: Environment.GetEnvironmentVariable(HostClassEnvironmentVariable) ?? Environment.MachineName,
            ProcessPath: ToPortableProcessReference(hostProcessPath),
            ArtifactDigest: artifactDigest,
            ArtifactDigestSource: artifactDigestSource,
            Framework: RuntimeInformation.FrameworkDescription,
            OperatingSystem: RuntimeInformation.OSDescription,
            StartedAtUtc: startedAtUtc,
            ReceiptPath: Environment.GetEnvironmentVariable(ReceiptEnvironmentVariable),
            FailurePacketPath: Environment.GetEnvironmentVariable(FailurePacketEnvironmentVariable),
            ScreenshotDirectory: string.IsNullOrWhiteSpace(screenshotDirectory) ? null : screenshotDirectory,
            TracePath: string.IsNullOrWhiteSpace(tracePath) ? null : tracePath,
            UserJourneyTraceOutputPath: NormalizeNullable(userJourneyTraceOutputPath),
            UserJourneyTesterShardId: NormalizeNullable(Environment.GetEnvironmentVariable(UserJourneyTesterShardIdEnvironmentVariable)),
            UserJourneyFixShardId: NormalizeNullable(Environment.GetEnvironmentVariable(UserJourneyFixShardIdEnvironmentVariable)));
    }

    public static void PrepareUserJourneyTraceOutput(DesktopMouseFirstJourneyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.UserJourneyTraceOutputPath))
        {
            return;
        }

        string outputPath = ValidateUserJourneyTraceTargetPath(context);
        DeleteUserJourneyTraceIfPresent(outputPath);
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

        bool userJourneyTraceRequested = !string.IsNullOrWhiteSpace(context.UserJourneyTraceOutputPath);
        if (string.IsNullOrWhiteSpace(context.ReceiptPath))
        {
            if (userJourneyTraceRequested)
            {
                throw new InvalidOperationException("An explicit mouse-first receipt path is required when the user-journey trace producer is enabled.");
            }

            return;
        }

        string? userJourneyTraceOutputPath = null;
        IReadOnlyList<DesktopUserJourneyTraceWorkflow>? boundUserJourneyWorkflows = null;
        if (userJourneyTraceRequested)
        {
            userJourneyTraceOutputPath = ValidateUserJourneyTraceTargetPath(context);
            ValidateUserJourneyRunBindings(context, evidence);
            boundUserJourneyWorkflows = ValidateAndBindUserJourneyWorkflows(
                context,
                evidence.UserJourneyWorkflows!);
        }

        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
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
            ProcessPath: ToPortableProcessReference(context.ProcessPath),
            ProcessPathDisclosure: PortableProcessPathDisclosure,
            ArtifactDigest: context.ArtifactDigest,
            ArtifactDigestSource: context.ArtifactDigestSource,
            Framework: context.Framework,
            OperatingSystem: context.OperatingSystem,
            StartedAtUtc: context.StartedAtUtc,
            CompletedAtUtc: completedAtUtc,
            RecordedAtUtc: completedAtUtc,
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

        if (!userJourneyTraceRequested)
        {
            return;
        }

        try
        {
            WriteUserJourneyTesterTrace(
                context,
                receipt,
                completedAtUtc,
                userJourneyTraceOutputPath!,
                boundUserJourneyWorkflows!);
        }
        catch
        {
            InvalidateUserJourneyTraceOutput(context);
            throw;
        }
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

        Exception artifactException = ex;
        try
        {
            InvalidateUserJourneyTraceOutput(context);
        }
        catch (Exception invalidationException)
        {
            artifactException = new AggregateException(
                "The journey failed and its staged user-journey trace output could not be safely invalidated.",
                ex,
                invalidationException);
        }

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
                ProcessPath: ToPortableProcessReference(context.ProcessPath),
                ProcessPathDisclosure: PortableProcessPathDisclosure,
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
                Error: artifactException.Message);
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
            Error: artifactException.ToString(),
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
                ProcessPath: ToPortableProcessReference(context.ProcessPath),
                ProcessPathDisclosure: PortableProcessPathDisclosure,
                RecordedAtUtc: DateTimeOffset.UtcNow,
                ObservedInputEvents: observedInputEvents));
    }

    public static void InvalidateUserJourneyTraceOutput(DesktopMouseFirstJourneyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.UserJourneyTraceOutputPath))
        {
            return;
        }

        string outputPath = ValidateUserJourneyTraceTargetPath(context);
        DeleteUserJourneyTraceIfPresent(outputPath);
    }

    private static void WriteUserJourneyTesterTrace(
        DesktopMouseFirstJourneyContext context,
        DesktopMouseFirstJourneyReceipt receipt,
        DateTimeOffset completedAtUtc,
        string outputPath,
        IReadOnlyList<DesktopUserJourneyTraceWorkflow> workflows)
    {
        string receiptPath = Path.GetFullPath(context.ReceiptPath!);
        if (!File.Exists(receiptPath))
        {
            throw new InvalidOperationException("The mouse-first source receipt must exist before the user-journey trace is emitted.");
        }

        RejectReparsePoint(receiptPath, "mouse-first source receipt");
        FileInfo receiptBeforeRead = new(receiptPath);
        byte[] receiptBytes = File.ReadAllBytes(receiptPath);
        FileInfo receiptAfterRead = new(receiptPath);
        if (receiptBeforeRead.Length != receiptBytes.LongLength
            || receiptAfterRead.Length != receiptBytes.LongLength
            || receiptBeforeRead.LastWriteTimeUtc != receiptAfterRead.LastWriteTimeUtc)
        {
            throw new InvalidOperationException("The mouse-first source receipt changed while its digest binding was being captured.");
        }

        ValidateWrittenSourceReceipt(receiptBytes, context, receipt);
        string receiptDigest = ToSha256Digest(SHA256.HashData(receiptBytes));

        DesktopUserJourneyTesterTrace trace = new(
            ContractName: UserJourneyTraceContractName,
            Status: "pass",
            GeneratedAtUtc: completedAtUtc,
            TesterShardId: context.UserJourneyTesterShardId!,
            FixShardId: context.UserJourneyFixShardId!,
            LinuxBinaryUnderTest: string.Equals(context.Platform, "linux", StringComparison.OrdinalIgnoreCase),
            UsedInternalApis: false,
            OpenBlockingFindings: [],
            ReleaseVersion: context.ReleaseVersion,
            ReleaseChannel: context.ChannelId,
            ArtifactDigest: context.ArtifactDigest!,
            ArtifactDigestSource: context.ArtifactDigestSource,
            SourceMouseReceiptName: Path.GetFileName(receiptPath),
            SourceMouseReceiptPath: receiptPath,
            SourceMouseReceiptSha256: receiptDigest,
            Workflows: workflows);

        WriteJsonAtomic(outputPath, trace);
    }

    private static void ValidateUserJourneyRunBindings(
        DesktopMouseFirstJourneyContext context,
        DesktopMouseFirstJourneyEvidence evidence)
    {
        if (!string.Equals(context.Platform, "linux", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The user-journey trace producer requires a Linux live binary run.");
        }

        if (!IsSha256Digest(context.ArtifactDigest))
        {
            throw new InvalidOperationException("The user-journey trace producer requires a canonical sha256 artifact digest.");
        }

        if (string.IsNullOrWhiteSpace(context.ReleaseVersion))
        {
            throw new InvalidOperationException("The user-journey trace producer requires an explicit release version binding.");
        }

        if (string.IsNullOrWhiteSpace(context.ChannelId))
        {
            throw new InvalidOperationException("The user-journey trace producer requires an explicit release channel binding.");
        }

        if (string.IsNullOrWhiteSpace(context.ScreenshotDirectory))
        {
            throw new InvalidOperationException("The user-journey trace producer requires an explicit screenshot directory.");
        }

        if (string.IsNullOrWhiteSpace(context.UserJourneyTesterShardId)
            || string.IsNullOrWhiteSpace(context.UserJourneyFixShardId)
            || string.Equals(context.UserJourneyTesterShardId, context.UserJourneyFixShardId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tester and fixer shard IDs must be explicit and distinct.");
        }

        if (evidence.UserJourneyWorkflows is null)
        {
            throw new InvalidOperationException("The user-journey trace producer requires all five routed workflow evidence rows.");
        }
    }

    private static IReadOnlyList<DesktopUserJourneyTraceWorkflow> ValidateAndBindUserJourneyWorkflows(
        DesktopMouseFirstJourneyContext context,
        IReadOnlyList<DesktopUserJourneyWorkflowEvidence> workflows)
    {
        if (workflows.Count != RequiredUserJourneyAssertions.Count)
        {
            throw new InvalidOperationException($"Expected exactly {RequiredUserJourneyAssertions.Count} user-journey workflows, but received {workflows.Count}.");
        }

        Dictionary<string, DesktopUserJourneyWorkflowEvidence> workflowsById = new(StringComparer.Ordinal);
        foreach (DesktopUserJourneyWorkflowEvidence workflow in workflows)
        {
            if (string.IsNullOrWhiteSpace(workflow.Id) || !workflowsById.TryAdd(workflow.Id, workflow))
            {
                throw new InvalidOperationException("User-journey workflow IDs must be non-empty and unique.");
            }
        }

        string screenshotRoot = Path.GetFullPath(context.ScreenshotDirectory!);
        if (!Directory.Exists(screenshotRoot))
        {
            throw new InvalidOperationException($"User-journey screenshot directory does not exist: {screenshotRoot}");
        }

        RejectReparsePoint(screenshotRoot, "screenshot directory");
        HashSet<string> screenshotPaths = new(StringComparer.Ordinal);
        HashSet<string> screenshotDigests = new(StringComparer.Ordinal);
        List<DesktopUserJourneyTraceWorkflow> boundWorkflows = [];

        foreach ((string workflowId, string[] requiredAssertions) in RequiredUserJourneyAssertions)
        {
            if (!workflowsById.TryGetValue(workflowId, out DesktopUserJourneyWorkflowEvidence? workflow))
            {
                throw new InvalidOperationException($"Missing required user-journey workflow '{workflowId}'.");
            }

            string[] failedOrMissingAssertions = requiredAssertions
                .Where(assertion => !workflow.Assertions.TryGetValue(assertion, out bool passed) || !passed)
                .ToArray();
            string[] unexpectedAssertions = workflow.Assertions.Keys
                .Where(assertion => !requiredAssertions.Contains(assertion, StringComparer.Ordinal))
                .OrderBy(static assertion => assertion, StringComparer.Ordinal)
                .ToArray();
            if (workflow.Assertions.Count != requiredAssertions.Length
                || failedOrMissingAssertions.Length > 0
                || unexpectedAssertions.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Workflow '{workflowId}' assertions invalid: "
                    + $"failed_or_missing=[{string.Join(", ", failedOrMissingAssertions)}]; "
                    + $"unexpected=[{string.Join(", ", unexpectedAssertions)}]; "
                    + $"expected_count={requiredAssertions.Length}; actual_count={workflow.Assertions.Count}.");
            }

            if (workflow.ScreenshotPaths.Count != 2)
            {
                throw new InvalidOperationException($"Workflow '{workflowId}' must bind exactly two screenshot frames.");
            }

            List<string> relativeScreenshotPaths = [];
            Dictionary<string, string> screenshotHashes = new(StringComparer.Ordinal);
            foreach (string configuredScreenshotPath in workflow.ScreenshotPaths)
            {
                string fullScreenshotPath = Path.GetFullPath(configuredScreenshotPath);
                string relativeScreenshotPath = Path.GetRelativePath(screenshotRoot, fullScreenshotPath);
                if (Path.IsPathRooted(relativeScreenshotPath)
                    || relativeScreenshotPath.Equals("..", StringComparison.Ordinal)
                    || relativeScreenshotPath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Workflow '{workflowId}' screenshot is outside the configured screenshot directory.");
                }

                if (!screenshotPaths.Add(fullScreenshotPath))
                {
                    throw new InvalidOperationException($"Workflow '{workflowId}' reuses a screenshot path.");
                }

                ValidatePngScreenshot(fullScreenshotPath, workflowId);
                string screenshotDigest = ToSha256Digest(SHA256.HashData(File.ReadAllBytes(fullScreenshotPath)));
                if (!screenshotDigests.Add(screenshotDigest))
                {
                    throw new InvalidOperationException("All ten user-journey screenshot frames must have unique SHA-256 digests.");
                }

                string normalizedRelativePath = relativeScreenshotPath.Replace(Path.DirectorySeparatorChar, '/');
                relativeScreenshotPaths.Add(normalizedRelativePath);
                screenshotHashes.Add(normalizedRelativePath, screenshotDigest);
            }

            boundWorkflows.Add(new DesktopUserJourneyTraceWorkflow(
                Id: workflowId,
                Status: "pass",
                Screenshots: relativeScreenshotPaths,
                ScreenshotSha256: screenshotHashes,
                Assertions: workflow.Assertions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                InteractionNotes: workflow.InteractionNotes));
        }

        if (screenshotDigests.Count != 10)
        {
            throw new InvalidOperationException("The user-journey trace must bind exactly ten unique screenshot frames.");
        }

        return boundWorkflows;
    }

    private static void ValidatePngScreenshot(string path, string workflowId)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Workflow '{workflowId}' screenshot does not exist: {path}");
        }

        RejectReparsePoint(path, "screenshot");
        using FileStream stream = File.OpenRead(path);
        if (stream.Length < 33)
        {
            throw new InvalidOperationException($"Workflow '{workflowId}' screenshot is not a complete PNG: {path}");
        }

        Span<byte> header = stackalloc byte[33];
        stream.ReadExactly(header);
        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        bool hasIhdr = BinaryPrimitives.ReadUInt32BigEndian(header[8..12]) == 13
            && header[12..16].SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(header[16..20]) > 0
            && BinaryPrimitives.ReadUInt32BigEndian(header[20..24]) > 0;
        if (!header[..8].SequenceEqual(pngSignature) || !hasIhdr)
        {
            throw new InvalidOperationException($"Workflow '{workflowId}' screenshot is not a valid PNG frame: {path}");
        }
    }

    private static void ValidateWrittenSourceReceipt(
        byte[] receiptBytes,
        DesktopMouseFirstJourneyContext context,
        DesktopMouseFirstJourneyReceipt expectedReceipt)
    {
        using JsonDocument document = JsonDocument.Parse(receiptBytes);
        JsonElement root = document.RootElement;
        if (!string.Equals(root.GetProperty("status").GetString(), "pass", StringComparison.Ordinal)
            || !string.Equals(root.GetProperty("journeyMode").GetString(), "mouse_first_live_binary", StringComparison.Ordinal)
            || !string.Equals(root.GetProperty("artifactDigest").GetString(), context.ArtifactDigest, StringComparison.Ordinal)
            || !string.Equals(root.GetProperty("releaseVersion").GetString(), context.ReleaseVersion, StringComparison.Ordinal)
            || !string.Equals(root.GetProperty("channelId").GetString(), context.ChannelId, StringComparison.Ordinal)
            || root.GetProperty("completedAtUtc").GetDateTimeOffset() != expectedReceipt.CompletedAtUtc)
        {
            throw new InvalidOperationException("The written mouse-first receipt does not match the completed live-binary run bindings.");
        }
    }

    private static string ValidateUserJourneyTraceTargetPath(DesktopMouseFirstJourneyContext context)
    {
        if (string.IsNullOrWhiteSpace(context.UserJourneyTraceOutputPath))
        {
            throw new InvalidOperationException("An explicit user-journey trace output path is required.");
        }

        if (!Path.IsPathRooted(context.UserJourneyTraceOutputPath))
        {
            throw new InvalidOperationException("The user-journey trace output path must be absolute and caller-staged.");
        }

        string outputPath = Path.GetFullPath(context.UserJourneyTraceOutputPath);
        RejectExistingReparsePoints(outputPath, "user-journey trace output path");
        string normalizedOutputPath = outputPath.Replace('\\', '/');
        if (normalizedOutputPath.EndsWith($"/.codex-studio/published/{CanonicalUserJourneyTraceFileName}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The live producer must never overwrite the canonical published user-journey trace.");
        }

        string[] conflictingPaths =
        [
            context.ReceiptPath ?? string.Empty,
            context.FailurePacketPath ?? string.Empty,
            context.TracePath ?? string.Empty
        ];
        if (conflictingPaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Any(path => string.Equals(path, outputPath, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The user-journey trace output must be separate from mouse receipt, failure, and observed-input artifacts.");
        }

        if (Directory.Exists(outputPath))
        {
            throw new InvalidOperationException("The user-journey trace output path resolves to a directory.");
        }

        if (File.Exists(outputPath))
        {
            RejectReparsePoint(outputPath, "user-journey trace output");
        }

        return outputPath;
    }

    private static void DeleteUserJourneyTraceIfPresent(string outputPath)
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    private static void RejectReparsePoint(string path, string description)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException($"The {description} must not be a symbolic link or reparse point: {path}");
        }
    }

    private static void RejectExistingReparsePoints(string path, string description)
    {
        string? currentPath = Path.GetFullPath(path);
        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            if (File.Exists(currentPath) || Directory.Exists(currentPath))
            {
                RejectReparsePoint(currentPath, description);
            }

            string? parentPath = Directory.GetParent(currentPath)?.FullName;
            if (string.IsNullOrWhiteSpace(parentPath)
                || string.Equals(parentPath, currentPath, StringComparison.Ordinal))
            {
                break;
            }

            currentPath = parentPath;
        }
    }

    private static bool IsSha256Digest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith("sha256:", StringComparison.Ordinal)
            || value.Length != 71)
        {
            return false;
        }

        foreach (char character in value.AsSpan(7))
        {
            if (!(character is >= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static string ToSha256Digest(ReadOnlySpan<byte> digest)
        => $"sha256:{Convert.ToHexString(digest).ToLowerInvariant()}";

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

    private static string ToPortableProcessReference(string? hostProcessPath)
    {
        string normalized = (hostProcessPath ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "<redacted:process-path-unavailable>";
        }

        int separatorIndex = normalized.LastIndexOf('/');
        string leaf = separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;
        return string.IsNullOrWhiteSpace(leaf) ? "<redacted:process-path>" : leaf;
    }

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

        string? resolved = ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseVersion")
            ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();
        return string.IsNullOrWhiteSpace(resolved) ? "local-build" : resolved;
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

        return string.IsNullOrWhiteSpace(fallbackVersion) ? "local-build" : fallbackVersion;
    }

    private static string ResolveRid()
    {
        string? overrideRid = Environment.GetEnvironmentVariable(RidEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideRid))
        {
            return overrideRid.Trim().ToLowerInvariant();
        }

        string runtimeRid = RuntimeInformation.RuntimeIdentifier?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(runtimeRid))
        {
            return runtimeRid;
        }

        return $"{DetectPlatform()}-{DetectArchitecture()}";
    }

    private static Assembly ResolveDesktopAssembly()
    {
        Assembly? entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly is not null && !string.IsNullOrWhiteSpace(ReadAssemblyMetadata(entryAssembly, "ChummerDesktopHeadId")))
        {
            return entryAssembly;
        }

        return AppDomain.CurrentDomain.GetAssemblies()
                   .FirstOrDefault(static assembly => !string.IsNullOrWhiteSpace(
                       assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                           .FirstOrDefault(attribute => string.Equals(attribute.Key, "ChummerDesktopHeadId", StringComparison.Ordinal))?
                           .Value))
               ?? Assembly.GetExecutingAssembly();
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
        WriteJsonAtomic(path, payload);
    }

    private static void WriteJsonAtomic<T>(string path, T payload)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The user-journey trace output must have a parent directory.");
        RejectExistingReparsePoints(directory, "JSON artifact output directory path");
        Directory.CreateDirectory(directory);
        RejectExistingReparsePoints(directory, "JSON artifact output directory path");
        if (File.Exists(fullPath))
        {
            RejectReparsePoint(fullPath, "JSON artifact output");
        }

        string tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 16 * 1024,
                       FileOptions.WriteThrough))
            using (StreamWriter writer = new(
                       stream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       bufferSize: 4096,
                       leaveOpen: true))
            {
                writer.Write(JsonSerializer.Serialize(payload, JsonOptions));
                writer.Write(Environment.NewLine);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
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
    string? TracePath,
    string? UserJourneyTraceOutputPath = null,
    string? UserJourneyTesterShardId = null,
    string? UserJourneyFixShardId = null);

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
    IReadOnlyList<string> VerificationNotes,
    IReadOnlyList<DesktopUserJourneyWorkflowEvidence>? UserJourneyWorkflows = null);

public sealed record DesktopUserJourneyWorkflowEvidence(
    string Id,
    IReadOnlyList<string> ScreenshotPaths,
    IReadOnlyDictionary<string, bool> Assertions,
    IReadOnlyList<string>? InteractionNotes = null);

internal sealed record DesktopUserJourneyTesterTrace(
    [property: JsonPropertyName("contract_name")] string ContractName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("generated_at_utc")] DateTimeOffset GeneratedAtUtc,
    [property: JsonPropertyName("tester_shard_id")] string TesterShardId,
    [property: JsonPropertyName("fix_shard_id")] string FixShardId,
    [property: JsonPropertyName("linux_binary_under_test")] bool LinuxBinaryUnderTest,
    [property: JsonPropertyName("used_internal_apis")] bool UsedInternalApis,
    [property: JsonPropertyName("open_blocking_findings")] IReadOnlyList<string> OpenBlockingFindings,
    [property: JsonPropertyName("release_version")] string ReleaseVersion,
    [property: JsonPropertyName("release_channel")] string ReleaseChannel,
    [property: JsonPropertyName("artifact_digest")] string ArtifactDigest,
    [property: JsonPropertyName("artifact_digest_source")] string ArtifactDigestSource,
    [property: JsonPropertyName("source_mouse_receipt_name")] string SourceMouseReceiptName,
    [property: JsonPropertyName("source_mouse_receipt_path")] string SourceMouseReceiptPath,
    [property: JsonPropertyName("source_mouse_receipt_sha256")] string SourceMouseReceiptSha256,
    [property: JsonPropertyName("workflows")] IReadOnlyList<DesktopUserJourneyTraceWorkflow> Workflows);

internal sealed record DesktopUserJourneyTraceWorkflow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("screenshots")] IReadOnlyList<string> Screenshots,
    [property: JsonPropertyName("screenshot_sha256")] IReadOnlyDictionary<string, string> ScreenshotSha256,
    [property: JsonPropertyName("assertions")] IReadOnlyDictionary<string, bool> Assertions,
    [property: JsonPropertyName("interaction_notes")] IReadOnlyList<string>? InteractionNotes);

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
    string ProcessPathDisclosure,
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
    string ProcessPathDisclosure,
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
