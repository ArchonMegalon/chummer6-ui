using System.Net.Mime;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Hub.Registry.Contracts.InstallLinking;

namespace Chummer.Desktop.Runtime;

public sealed record DesktopInstallLinkingStartupContext(
    DesktopInstallLinkingState State,
    DesktopInstallClaimResult? ClaimResult,
    string? StartupClaimCode,
    bool ShouldPrompt,
    string PromptReason);

public sealed record DesktopInstallClaimResult(
    bool Succeeded,
    bool AlreadyClaimed,
    string Message,
    DesktopInstallLinkingState State);

public sealed record DesktopInstallLinkingState(
    string InstallationId,
    string HeadId,
    string ApplicationVersion,
    string ChannelId,
    string Platform,
    string Arch,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int LaunchCount,
    DateTimeOffset? LastStartedAtUtc,
    DateTimeOffset? ClaimedAtUtc,
    DateTimeOffset? LastPromptDismissedAtUtc,
    string PublicKey,
    string PrivateKey,
    string? ClaimTicketId = null,
    string? LastClaimCode = null,
    string? LastClaimMessage = null,
    string? LastClaimError = null,
    DateTimeOffset? LastClaimAttemptUtc = null,
    string? GrantId = null,
    string? GrantToken = null,
    DateTimeOffset? GrantIssuedAtUtc = null,
    DateTimeOffset? GrantExpiresAtUtc = null,
    string? UserId = null,
    string? SubjectId = null);

public static class DesktopInstallLinkingRuntime
{
    private const string ApiBaseUrlEnvironmentVariable = "CHUMMER_API_BASE_URL";
    private const string ApiKeyEnvironmentVariable = "CHUMMER_API_KEY";
    private const string WebBaseUrlEnvironmentVariable = "CHUMMER_WEB_BASE_URL";
    private const string ClaimCodeEnvironmentVariable = "CHUMMER_INSTALL_CLAIM_CODE";
    private const string ClaimCodeSwitch = "--install-claim-code";
    private const string StateRootDirectoryName = "install-linking";
    private const string GuestStatus = "guest";
    private const string ClaimedStatus = "claimed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<DesktopInstallLinkingStartupContext> InitializeForStartupAsync(
        string headId,
        string[] args,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        ArgumentNullException.ThrowIfNull(args);

        DesktopInstallLinkingState state = LoadOrCreateState(headId);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        state = RefreshRuntimeMetadata(state, now) with
        {
            LaunchCount = state.LaunchCount + 1,
            LastStartedAtUtc = now,
            UpdatedAtUtc = now
        };
        SaveState(state);

        string? startupClaimCode = ExtractStartupClaimCode(args);
        DesktopInstallClaimResult? claimResult = null;
        if (!string.IsNullOrWhiteSpace(startupClaimCode))
        {
            claimResult = await RedeemClaimCodeAsync(headId, startupClaimCode, state, cancellationToken).ConfigureAwait(false);
            state = claimResult.State;
        }

        bool shouldPrompt = !IsClaimed(state) && (state.LaunchCount == 1 || !string.IsNullOrWhiteSpace(startupClaimCode));
        if (claimResult?.Succeeded == true)
        {
            shouldPrompt = false;
        }

        string promptReason = !string.IsNullOrWhiteSpace(startupClaimCode)
            ? claimResult?.Succeeded == true ? "claim_applied" : "claim_code_present"
            : state.LaunchCount == 1 ? "first_launch" : "none";

        return new DesktopInstallLinkingStartupContext(
            State: state,
            ClaimResult: claimResult,
            StartupClaimCode: startupClaimCode,
            ShouldPrompt: shouldPrompt,
            PromptReason: promptReason);
    }

    public static DesktopInstallLinkingState LoadOrCreateState(string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopRuntimeReleaseMetadata release = DesktopRuntimeReleaseMetadata.Load(headId);
        DesktopRuntimePlatformIdentity identity = DesktopRuntimePlatformIdentity.Current();
        DesktopInstallLinkingPaths paths = DesktopInstallLinkingPaths.Create(headId, identity);
        DesktopInstallLinkingState? state = DesktopInstallLinkingStateStore.Load(paths.StateFilePath);
        if (state is null)
        {
            state = CreateInitialState(release, identity, DateTimeOffset.UtcNow);
            SaveState(state);
            return state;
        }

        if (string.IsNullOrWhiteSpace(state.PublicKey) || string.IsNullOrWhiteSpace(state.PrivateKey))
        {
            (string publicKey, string privateKey) = CreateInstallationKeyPair();
            state = state with
            {
                PublicKey = publicKey,
                PrivateKey = privateKey,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            SaveState(state);
        }

        return RefreshRuntimeMetadata(state, DateTimeOffset.UtcNow);
    }

    public static async Task<DesktopInstallClaimResult> RedeemClaimCodeAsync(
        string headId,
        string claimCode,
        CancellationToken cancellationToken)
        => await RedeemClaimCodeAsync(headId, claimCode, LoadOrCreateState(headId), cancellationToken).ConfigureAwait(false);

    public static void MarkPromptDismissed(string headId)
    {
        DesktopInstallLinkingState state = LoadOrCreateState(headId);
        SaveState(state with
        {
            LastPromptDismissedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    public static bool TryOpenAccountPortal()
    {
        return TryOpenPublicPortal("/account#desktop");
    }

    public static bool TryOpenSupportPortal()
    {
        return TryOpenPublicPortal("/account/support");
    }

    public static bool TryOpenDownloadsPortal()
    {
        return TryOpenPublicPortal("/downloads");
    }

    public static bool IsClaimed(DesktopInstallLinkingState state)
        => string.Equals(state.Status, ClaimedStatus, StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(state.GrantToken);

    private static async Task<DesktopInstallClaimResult> RedeemClaimCodeAsync(
        string headId,
        string claimCode,
        DesktopInstallLinkingState state,
        CancellationToken cancellationToken)
    {
        string? normalizedClaimCode = NormalizeClaimCode(claimCode);
        if (normalizedClaimCode is null)
        {
            DesktopInstallLinkingState invalidState = state with
            {
                LastClaimAttemptUtc = DateTimeOffset.UtcNow,
                LastClaimCode = null,
                LastClaimError = "Claim code is required.",
                LastClaimMessage = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            SaveState(invalidState);
            return new DesktopInstallClaimResult(false, false, "Claim code is required.", invalidState);
        }

        DesktopInstallLinkingState currentState = RefreshRuntimeMetadata(state, DateTimeOffset.UtcNow);
        DateTimeOffset attemptAtUtc = DateTimeOffset.UtcNow;
        try
        {
            RedeemInstallClaimRequestDto request = new(
                ClaimCode: normalizedClaimCode,
                InstallationId: currentState.InstallationId,
                HeadId: currentState.HeadId,
                ApplicationVersion: currentState.ApplicationVersion,
                ChannelId: currentState.ChannelId,
                Platform: currentState.Platform,
                Arch: currentState.Arch,
                PublicKey: currentState.PublicKey,
                HostLabel: null);

            using HttpClient client = CreateApiHttpClient(TimeSpan.FromSeconds(20));
            using StringContent content = new(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);
            using HttpResponseMessage response = await client.PostAsync("api/v1/install-linking/redeem", content, cancellationToken).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string error = BuildErrorMessage(response, responseText);
                DesktopInstallLinkingState failedState = currentState with
                {
                    LastClaimAttemptUtc = attemptAtUtc,
                    LastClaimCode = normalizedClaimCode,
                    LastClaimError = error,
                    LastClaimMessage = null,
                    UpdatedAtUtc = attemptAtUtc
                };
                SaveState(failedState);
                return new DesktopInstallClaimResult(false, false, error, failedState);
            }

            RedeemInstallClaimResponseDto? accepted = JsonSerializer.Deserialize<RedeemInstallClaimResponseDto>(responseText, JsonOptions);
            if (accepted?.Installation is null || accepted.Grant is null || accepted.Ticket is null)
            {
                string error = "Hub accepted install claim redemption but returned an unreadable payload.";
                DesktopInstallLinkingState invalidState = currentState with
                {
                    LastClaimAttemptUtc = attemptAtUtc,
                    LastClaimCode = normalizedClaimCode,
                    LastClaimError = error,
                    LastClaimMessage = null,
                    UpdatedAtUtc = attemptAtUtc
                };
                SaveState(invalidState);
                return new DesktopInstallClaimResult(false, false, error, invalidState);
            }

            DesktopInstallLinkingState claimedState = currentState with
            {
                Status = ClaimedStatus,
                ClaimedAtUtc = currentState.ClaimedAtUtc ?? attemptAtUtc,
                ClaimTicketId = accepted.Ticket.TicketId,
                LastClaimAttemptUtc = attemptAtUtc,
                LastClaimCode = normalizedClaimCode,
                LastClaimError = null,
                LastClaimMessage = accepted.AlreadyClaimed
                    ? "This copy was already linked. Hub refreshed the installation grant."
                    : "This copy is now linked to your Hub account.",
                UpdatedAtUtc = attemptAtUtc,
                HeadId = accepted.Installation.HeadId ?? currentState.HeadId,
                ApplicationVersion = accepted.Installation.Version,
                ChannelId = accepted.Installation.Channel,
                Platform = accepted.Installation.Platform ?? currentState.Platform,
                Arch = accepted.Installation.Arch ?? currentState.Arch,
                GrantId = accepted.Grant.GrantId,
                GrantToken = accepted.Grant.AccessToken,
                GrantIssuedAtUtc = accepted.Grant.IssuedAtUtc,
                GrantExpiresAtUtc = accepted.Grant.ExpiresAtUtc,
                UserId = accepted.Installation.UserId,
                SubjectId = accepted.Installation.SubjectId
            };
            SaveState(claimedState);
            return new DesktopInstallClaimResult(
                true,
                accepted.AlreadyClaimed,
                claimedState.LastClaimMessage ?? "This copy is linked.",
                claimedState);
        }
        catch (Exception ex)
        {
            DesktopInstallLinkingState failedState = currentState with
            {
                LastClaimAttemptUtc = attemptAtUtc,
                LastClaimCode = normalizedClaimCode,
                LastClaimError = ex.Message,
                LastClaimMessage = null,
                UpdatedAtUtc = attemptAtUtc
            };
            SaveState(failedState);
            return new DesktopInstallClaimResult(false, false, $"Install linking failed: {ex.Message}", failedState);
        }
    }

    private static DesktopInstallLinkingState CreateInitialState(
        DesktopRuntimeReleaseMetadata release,
        DesktopRuntimePlatformIdentity identity,
        DateTimeOffset now)
    {
        (string publicKey, string privateKey) = CreateInstallationKeyPair();
        return new DesktopInstallLinkingState(
            InstallationId: $"ins-{Guid.NewGuid():N}",
            HeadId: release.HeadId,
            ApplicationVersion: release.Version,
            ChannelId: release.ChannelId,
            Platform: identity.Platform,
            Arch: identity.Arch,
            Status: GuestStatus,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 0,
            LastStartedAtUtc: null,
            ClaimedAtUtc: null,
            LastPromptDismissedAtUtc: null,
            PublicKey: publicKey,
            PrivateKey: privateKey);
    }

    private static DesktopInstallLinkingState RefreshRuntimeMetadata(DesktopInstallLinkingState state, DateTimeOffset now)
    {
        DesktopRuntimeReleaseMetadata release = DesktopRuntimeReleaseMetadata.Load(state.HeadId);
        DesktopRuntimePlatformIdentity identity = DesktopRuntimePlatformIdentity.Current();
        return state with
        {
            HeadId = release.HeadId,
            ApplicationVersion = string.IsNullOrWhiteSpace(release.Version) ? state.ApplicationVersion : release.Version,
            ChannelId = string.IsNullOrWhiteSpace(release.ChannelId) ? state.ChannelId : release.ChannelId,
            Platform = identity.Platform,
            Arch = identity.Arch,
            UpdatedAtUtc = now
        };
    }

    private static void SaveState(DesktopInstallLinkingState state)
    {
        DesktopInstallLinkingPaths paths = DesktopInstallLinkingPaths.Create(
            state.HeadId,
            new DesktopRuntimePlatformIdentity(state.Platform, state.Arch));
        DesktopInstallLinkingStateStore.Save(paths.StateFilePath, state);
    }

    private static string? ExtractStartupClaimCode(IReadOnlyList<string> args)
    {
        string? fromEnvironment = NormalizeClaimCode(Environment.GetEnvironmentVariable(ClaimCodeEnvironmentVariable));
        if (fromEnvironment is not null)
        {
            return fromEnvironment;
        }

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, ClaimCodeSwitch, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                return NormalizeClaimCode(args[i + 1]);
            }

            string prefix = $"{ClaimCodeSwitch}=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeClaimCode(arg[prefix.Length..]);
            }
        }

        return null;
    }

    private static HttpClient CreateApiHttpClient(TimeSpan timeout)
    {
        HttpClient client = new()
        {
            BaseAddress = ResolveApiBaseAddress(),
            Timeout = timeout
        };

        string? apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return client;
    }

    private static Uri ResolveApiBaseAddress()
    {
        string? configured = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        return new Uri("http://chummer-api:8080", UriKind.Absolute);
    }

    private static Uri ResolvePublicWebAddress()
    {
        string? configured = Environment.GetEnvironmentVariable(WebBaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        return ResolveApiBaseAddress();
    }

    private static bool TryOpenPublicPortal(string relativePath)
    {
        Uri uri = ResolvePublicWebAddress();
        return DesktopCrashRuntime.TryOpenPathInShell(new Uri(uri, relativePath).ToString());
    }

    private static string BuildErrorMessage(HttpResponseMessage response, string responseText)
    {
        try
        {
            ProblemEnvelope? problem = JsonSerializer.Deserialize<ProblemEnvelope>(responseText, JsonOptions);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem.Detail.Trim();
            }

            if (!string.IsNullOrWhiteSpace(problem?.Title))
            {
                return problem.Title.Trim();
            }
        }
        catch
        {
            // Fall back to the HTTP status line below.
        }

        return $"Hub install linking returned {(int)response.StatusCode} {response.ReasonPhrase}.";
    }

    private static string? NormalizeClaimCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Concat(value.Trim().Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }

    private static (string PublicKey, string PrivateKey) CreateInstallationKeyPair()
    {
        using RSA rsa = RSA.Create(2048);
        return (rsa.ExportRSAPublicKeyPem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    private sealed record ProblemEnvelope(
        string? Title,
        string? Detail,
        int? Status);

    private sealed record DesktopRuntimeReleaseMetadata(
        string HeadId,
        string Version,
        string ChannelId)
    {
        public static DesktopRuntimeReleaseMetadata Load(string fallbackHeadId)
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return new DesktopRuntimeReleaseMetadata(
                HeadId: ReadAssemblyMetadata(assembly, "ChummerDesktopHeadId") ?? fallbackHeadId,
                Version: ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseVersion")
                    ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? assembly.GetName().Version?.ToString()
                    ?? string.Empty,
                ChannelId: ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseChannel") ?? "local");
        }

        private static string? ReadAssemblyMetadata(Assembly assembly, string key)
        {
            return assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?
                .Value;
        }
    }

    private sealed record DesktopRuntimePlatformIdentity(
        string Platform,
        string Arch)
    {
        public static DesktopRuntimePlatformIdentity Current()
            => new(
                Platform: ResolvePlatform(),
                Arch: NormalizeArchitecture(RuntimeInformation.OSArchitecture));

        private static string ResolvePlatform()
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

            return "unknown";
        }

        private static string NormalizeArchitecture(Architecture architecture)
            => architecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => architecture.ToString().ToLowerInvariant()
            };
    }

    private sealed record DesktopInstallLinkingPaths(string StateFilePath)
    {
        public static DesktopInstallLinkingPaths Create(string headId, DesktopRuntimePlatformIdentity identity)
        {
            string root = Path.Combine(
                GetStateRoot(),
                StateRootDirectoryName,
                headId,
                identity.Platform,
                identity.Arch);
            return new DesktopInstallLinkingPaths(Path.Combine(root, "state.json"));
        }
    }

    private static string GetStateRoot()
    {
        return DesktopStateRootResolver.Resolve("Chummer6", "Chummer6");
    }

    private static class DesktopInstallLinkingStateStore
    {
        public static DesktopInstallLinkingState? Load(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<DesktopInstallLinkingState>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
        }

        public static void Save(string path, DesktopInstallLinkingState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions), Encoding.UTF8);
        }
    }
}
