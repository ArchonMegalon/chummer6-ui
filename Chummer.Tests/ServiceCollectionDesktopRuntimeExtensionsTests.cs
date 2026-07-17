#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public class ServiceCollectionDesktopRuntimeExtensionsTests
{
    private static readonly object EnvironmentLock = new();
    private const string DefaultRulesetEnvironmentVariable = "CHUMMER_DEFAULT_RULESET";
    private const string DesktopStateRootEnvironmentVariable = "CHUMMER_DESKTOP_STATE_ROOT";
    private const string StatePathEnvironmentVariable = "CHUMMER_STATE_PATH";

    [TestMethod]
    public void Default_mode_registers_inprocess_client()
    {
        lock (EnvironmentLock)
        {
            string root = CreateTempDirectory();
            try
            {
                ApplyEnvironment(mode: null, baseUrl: null, apiKey: null, () =>
                {
                    var services = new ServiceCollection();
                    services.AddChummerLocalRuntimeClient(root, root);

                    using ServiceProvider provider = services.BuildServiceProvider();
                    IChummerClient client = provider.GetRequiredService<IChummerClient>();
                    ISessionClient sessionClient = provider.GetRequiredService<ISessionClient>();
                    IOwnerContextAccessor ownerContextAccessor = provider.GetRequiredService<IOwnerContextAccessor>();
                    IDesktopWorkspaceRoamingSync roamingSync = provider.GetRequiredService<IDesktopWorkspaceRoamingSync>();
                    IWorkspaceStore workspaceStore = provider.GetRequiredService<IWorkspaceStore>();
                    IWorkspaceStoreReadinessProbe workspaceReadiness =
                        provider.GetRequiredService<IWorkspaceStoreReadinessProbe>();
                    IReadOnlyList<IRulesetPlugin> plugins = provider.GetServices<IRulesetPlugin>().ToArray();

                    Assert.IsInstanceOfType<InProcessChummerClient>(client);
                    Assert.IsInstanceOfType<InProcessSessionClient>(sessionClient);
                    Assert.IsInstanceOfType<NoOpDesktopWorkspaceRoamingSync>(roamingSync);
                    Assert.IsTrue(ReferenceEquals(workspaceStore, workspaceReadiness));
                    Assert.AreEqual(OwnerScope.LocalSingleUser.NormalizedValue, ownerContextAccessor.Current.NormalizedValue);
                    Assert.IsTrue(plugins.Any(plugin => string.Equals(plugin.Id.NormalizedValue, RulesetDefaults.Sr4, StringComparison.Ordinal)));
                    Assert.IsTrue(plugins.Any(plugin => string.Equals(plugin.Id.NormalizedValue, RulesetDefaults.Sr5, StringComparison.Ordinal)));
                    Assert.IsTrue(plugins.Any(plugin => string.Equals(plugin.Id.NormalizedValue, RulesetDefaults.Sr6, StringComparison.Ordinal)));
                });
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }
    }

    [TestMethod]
    public void Desktop_mode_registers_claim_aware_owner_context_and_shared_state_path()
    {
        lock (EnvironmentLock)
        {
            string root = CreateTempDirectory();
            string desktopStateRoot = CreateTempDirectory();
            try
            {
                WriteClaimedInstallState(desktopStateRoot, "avalonia", subjectId: "subject-alpha", userId: "user-alpha");

                ApplyEnvironment(mode: null, baseUrl: null, apiKey: null, () =>
                {
                    Environment.SetEnvironmentVariable(DesktopStateRootEnvironmentVariable, desktopStateRoot);
                    Environment.SetEnvironmentVariable(StatePathEnvironmentVariable, null);

                    var services = new ServiceCollection();
                    services.AddChummerLocalRuntimeClient(root, root, "avalonia");

                    using ServiceProvider provider = services.BuildServiceProvider();
                    IOwnerContextAccessor ownerContextAccessor = provider.GetRequiredService<IOwnerContextAccessor>();
                    IDesktopWorkspaceRoamingSync roamingSync = provider.GetRequiredService<IDesktopWorkspaceRoamingSync>();

                    Assert.AreEqual("install-account:subject-alpha", ownerContextAccessor.Current.NormalizedValue);
                    Assert.IsInstanceOfType<GrantBoundDesktopWorkspaceRoamingSync>(roamingSync);
                    Assert.AreEqual(
                        Path.Combine(desktopStateRoot, "Chummer6", "state"),
                        Environment.GetEnvironmentVariable(StatePathEnvironmentVariable));
                });
            }
            finally
            {
                DeleteTempDirectory(root);
                DeleteTempDirectory(desktopStateRoot);
            }
        }
    }

    [TestMethod]
    public async Task Linked_installs_for_same_claimed_owner_share_workspace_catalog_on_shared_desktop_state()
    {
        lock (EnvironmentLock)
        {
            string avaloniaRoot = CreateTempDirectory();
            string blazorRoot = CreateTempDirectory();
            string desktopStateRoot = CreateTempDirectory();
            try
            {
                WriteClaimedInstallState(desktopStateRoot, "avalonia", subjectId: "subject-shared", userId: "user-shared");
                WriteClaimedInstallState(desktopStateRoot, "blazor-desktop", subjectId: "subject-shared", userId: "user-shared");

                ApplyEnvironment(mode: null, baseUrl: null, apiKey: null, () =>
                {
                    Environment.SetEnvironmentVariable(DesktopStateRootEnvironmentVariable, desktopStateRoot);
                    Environment.SetEnvironmentVariable(StatePathEnvironmentVariable, null);

                    WorkspaceImportResult imported;
                    using (ServiceProvider firstProvider = BuildProvider(avaloniaRoot, "avalonia"))
                    {
                        IChummerClient firstClient = firstProvider.GetRequiredService<IChummerClient>();
                        imported = firstClient.ImportAsync(
                                new WorkspaceImportDocument(
                                    "<character><name>Shared Runner</name></character>",
                                    RulesetDefaults.Sr6,
                                    WorkspaceDocumentFormat.NativeXml),
                                CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();
                    }

                    using ServiceProvider secondProvider = BuildProvider(blazorRoot, "blazor-desktop");
                    IChummerClient secondClient = secondProvider.GetRequiredService<IChummerClient>();
                    IReadOnlyList<WorkspaceListItem> workspaces = secondClient.ListWorkspacesAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();

                    Assert.IsTrue(workspaces.Any(item => string.Equals(item.Id.Value, imported.Id.Value, StringComparison.Ordinal)));
                });
            }
            finally
            {
                DeleteTempDirectory(avaloniaRoot);
                DeleteTempDirectory(blazorRoot);
                DeleteTempDirectory(desktopStateRoot);
            }
        }
    }

    [TestMethod]
    public void Guest_install_does_not_see_claimed_owner_workspace_lane()
    {
        lock (EnvironmentLock)
        {
            string claimedRoot = CreateTempDirectory();
            string guestRoot = CreateTempDirectory();
            string desktopStateRoot = CreateTempDirectory();
            try
            {
                WriteClaimedInstallState(desktopStateRoot, "avalonia", subjectId: "subject-private", userId: "user-private");

                ApplyEnvironment(mode: null, baseUrl: null, apiKey: null, () =>
                {
                    Environment.SetEnvironmentVariable(DesktopStateRootEnvironmentVariable, desktopStateRoot);
                    Environment.SetEnvironmentVariable(StatePathEnvironmentVariable, null);

                    using (ServiceProvider claimedProvider = BuildProvider(claimedRoot, "avalonia"))
                    {
                        IChummerClient claimedClient = claimedProvider.GetRequiredService<IChummerClient>();
                        claimedClient.ImportAsync(
                                new WorkspaceImportDocument(
                                    "<character><name>Private Runner</name></character>",
                                    RulesetDefaults.Sr6,
                                    WorkspaceDocumentFormat.NativeXml),
                                CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();
                    }

                    using ServiceProvider guestProvider = BuildProvider(guestRoot, "blazor-desktop");
                    IChummerClient guestClient = guestProvider.GetRequiredService<IChummerClient>();
                    IReadOnlyList<WorkspaceListItem> guestWorkspaces = guestClient.ListWorkspacesAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();

                    Assert.AreEqual(0, guestWorkspaces.Count);
                });
            }
            finally
            {
                DeleteTempDirectory(claimedRoot);
                DeleteTempDirectory(guestRoot);
                DeleteTempDirectory(desktopStateRoot);
            }
        }
    }

    [TestMethod]
    public void Http_mode_requires_explicit_api_base_url()
    {
        lock (EnvironmentLock)
        {
            string root = CreateTempDirectory();
            try
            {
                ApplyEnvironment(mode: "http", baseUrl: null, apiKey: null, () =>
                {
                    var services = new ServiceCollection();

                    InvalidOperationException? ex = null;
                    try
                    {
                        services.AddChummerLocalRuntimeClient(root, root);
                    }
                    catch (InvalidOperationException captured)
                    {
                        ex = captured;
                    }

                    Assert.IsNotNull(ex);
                    StringAssert.Contains(ex.Message, "CHUMMER_API_BASE_URL");
                });
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }
    }

    [TestMethod]
    public void Http_mode_registers_http_client_and_api_key_header_when_configured()
    {
        lock (EnvironmentLock)
        {
            string root = CreateTempDirectory();
            try
            {
                ApplyEnvironment(mode: "http", baseUrl: "https://api.example.invalid/", apiKey: "test-key", () =>
                {
                    var services = new ServiceCollection();
                    services.AddChummerLocalRuntimeClient(root, root);

                    using ServiceProvider provider = services.BuildServiceProvider();
                    IChummerClient client = provider.GetRequiredService<IChummerClient>();
                    ISessionClient sessionClient = provider.GetRequiredService<ISessionClient>();
                    HttpClient httpClient = provider.GetRequiredService<HttpClient>();
                    IReadOnlyList<IRulesetPlugin> plugins = provider.GetServices<IRulesetPlugin>().ToArray();

                    Assert.IsInstanceOfType<HttpChummerClient>(client);
                    Assert.IsInstanceOfType<HttpSessionClient>(sessionClient);
                    Assert.IsNotNull(httpClient.BaseAddress);
                    Assert.AreEqual("https://api.example.invalid/", httpClient.BaseAddress!.ToString());
                    Assert.IsTrue(httpClient.DefaultRequestHeaders.Contains("X-Api-Key"));
                    Assert.IsTrue(plugins.Any(plugin => string.Equals(plugin.Id.NormalizedValue, RulesetDefaults.Sr4, StringComparison.Ordinal)));
                    Assert.IsTrue(plugins.Any(plugin => string.Equals(plugin.Id.NormalizedValue, RulesetDefaults.Sr5, StringComparison.Ordinal)));
                    Assert.IsTrue(plugins.Any(plugin => string.Equals(plugin.Id.NormalizedValue, RulesetDefaults.Sr6, StringComparison.Ordinal)));
                    string[] expectedApiKeyValues = ["test-key"];
                    CollectionAssert.AreEqual(
                        expectedApiKeyValues,
                        new List<string>(httpClient.DefaultRequestHeaders.GetValues("X-Api-Key")));
                });
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }
    }

    [TestMethod]
    public void Legacy_desktop_client_mode_environment_variable_remains_supported()
    {
        lock (EnvironmentLock)
        {
            string root = CreateTempDirectory();
            try
            {
                ApplyEnvironment(mode: null, legacyMode: "http", baseUrl: "https://legacy.example.invalid/", apiKey: null, () =>
                {
                    var services = new ServiceCollection();
                    services.AddChummerLocalRuntimeClient(root, root);

                    using ServiceProvider provider = services.BuildServiceProvider();
                    IChummerClient client = provider.GetRequiredService<IChummerClient>();
                    ISessionClient sessionClient = provider.GetRequiredService<ISessionClient>();
                    HttpClient httpClient = provider.GetRequiredService<HttpClient>();
                    IReadOnlyList<IRulesetPlugin> plugins = provider.GetServices<IRulesetPlugin>().ToArray();

                    Assert.IsInstanceOfType<HttpChummerClient>(client);
                    Assert.IsInstanceOfType<HttpSessionClient>(sessionClient);
                    Assert.IsNotNull(httpClient.BaseAddress);
                    Assert.AreEqual("https://legacy.example.invalid/", httpClient.BaseAddress!.ToString());
                    Assert.IsTrue(plugins.Any(plugin => string.Equals(plugin.Id.NormalizedValue, RulesetDefaults.Sr4, StringComparison.Ordinal)));
                    Assert.IsTrue(plugins.Any(plugin => string.Equals(plugin.Id.NormalizedValue, RulesetDefaults.Sr5, StringComparison.Ordinal)));
                    Assert.IsTrue(plugins.Any(plugin => string.Equals(plugin.Id.NormalizedValue, RulesetDefaults.Sr6, StringComparison.Ordinal)));
                });
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }
    }

    [TestMethod]
    public void Default_ruleset_environment_variable_controls_shell_catalog_resolution()
    {
        lock (EnvironmentLock)
        {
            string root = CreateTempDirectory();
            try
            {
                ApplyEnvironment(mode: null, baseUrl: null, apiKey: null, action: () =>
                {
                    var services = new ServiceCollection();
                    services.AddChummerLocalRuntimeClient(root, root);

                    using ServiceProvider provider = services.BuildServiceProvider();
                    IRulesetSelectionPolicy selectionPolicy = provider.GetRequiredService<IRulesetSelectionPolicy>();
                    IRulesetShellCatalogResolver shellCatalogResolver = provider.GetRequiredService<IRulesetShellCatalogResolver>();

                    Assert.AreEqual(RulesetDefaults.Sr6, selectionPolicy.GetDefaultRulesetId());

                    IReadOnlyList<AppCommandDefinition> commands = shellCatalogResolver.ResolveCommands(null);
                    IReadOnlyList<NavigationTabDefinition> tabs = shellCatalogResolver.ResolveNavigationTabs(null);

                    Assert.IsNotEmpty(commands, "Expected SR6 to expose shell commands.");
                    Assert.IsNotEmpty(tabs, "Expected SR6 to expose navigation tabs.");
                    Assert.IsTrue(commands.All(command => string.Equals(command.RulesetId, RulesetDefaults.Sr6, StringComparison.Ordinal)));
                    Assert.IsTrue(tabs.All(tab => string.Equals(tab.RulesetId, RulesetDefaults.Sr6, StringComparison.Ordinal)));
                }, defaultRulesetId: RulesetDefaults.Sr6);
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }
    }

    [TestMethod]
    public void Default_ruleset_environment_variable_supports_sr4_when_registered()
    {
        lock (EnvironmentLock)
        {
            string root = CreateTempDirectory();
            try
            {
                ApplyEnvironment(mode: null, baseUrl: null, apiKey: null, action: () =>
                {
                    var services = new ServiceCollection();
                    services.AddChummerLocalRuntimeClient(root, root);

                    using ServiceProvider provider = services.BuildServiceProvider();
                    IRulesetSelectionPolicy selectionPolicy = provider.GetRequiredService<IRulesetSelectionPolicy>();
                    IRulesetShellCatalogResolver shellCatalogResolver = provider.GetRequiredService<IRulesetShellCatalogResolver>();

                    Assert.AreEqual(RulesetDefaults.Sr4, selectionPolicy.GetDefaultRulesetId());
                    IReadOnlyList<AppCommandDefinition> commands = shellCatalogResolver.ResolveCommands(null);
                    IReadOnlyList<NavigationTabDefinition> tabs = shellCatalogResolver.ResolveNavigationTabs(null);

                    Assert.IsNotEmpty(commands, "Expected SR4 to expose shell commands.");
                    Assert.IsNotEmpty(tabs, "Expected SR4 to expose navigation tabs.");
                    Assert.IsTrue(commands.All(command => string.Equals(command.RulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal)));
                    Assert.IsTrue(tabs.All(tab => string.Equals(tab.RulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal)));
                }, defaultRulesetId: RulesetDefaults.Sr4);
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }
    }

    [TestMethod]
    public void Default_ruleset_environment_variable_fails_when_ruleset_is_unknown()
    {
        lock (EnvironmentLock)
        {
            string root = CreateTempDirectory();
            try
            {
                ApplyEnvironment(mode: null, baseUrl: null, apiKey: null, action: () =>
                {
                    var services = new ServiceCollection();
                    services.AddChummerLocalRuntimeClient(root, root);

                    using ServiceProvider provider = services.BuildServiceProvider();
                    IRulesetSelectionPolicy selectionPolicy = provider.GetRequiredService<IRulesetSelectionPolicy>();
                    IRulesetShellCatalogResolver shellCatalogResolver = provider.GetRequiredService<IRulesetShellCatalogResolver>();

                    InvalidOperationException selectionPolicyEx = Assert.ThrowsExactly<InvalidOperationException>(() =>
                        selectionPolicy.GetDefaultRulesetId());
                    InvalidOperationException shellCatalogEx = Assert.ThrowsExactly<InvalidOperationException>(() =>
                        shellCatalogResolver.ResolveCommands(null));

                    StringAssert.Contains(selectionPolicyEx.Message, "Configured default ruleset 'sr0'");
                    StringAssert.Contains(selectionPolicyEx.Message, $"environment:{DefaultRulesetEnvironmentVariable}");
                    StringAssert.Contains(shellCatalogEx.Message, "Configured default ruleset 'sr0'");
                    StringAssert.Contains(shellCatalogEx.Message, $"environment:{DefaultRulesetEnvironmentVariable}");
                }, defaultRulesetId: "sr0");
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }
    }

    private static void ApplyEnvironment(string? mode, string? baseUrl, string? apiKey, Action action, string? defaultRulesetId = null)
        => ApplyEnvironment(mode, legacyMode: mode, baseUrl, apiKey, action, defaultRulesetId);

    private static void ApplyEnvironment(
        string? mode,
        string? legacyMode,
        string? baseUrl,
        string? apiKey,
        Action action,
        string? defaultRulesetId = null)
    {
        string? previousMode = Environment.GetEnvironmentVariable("CHUMMER_CLIENT_MODE");
        string? previousLegacyMode = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_CLIENT_MODE");
        string? previousBaseUrl = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        string? previousApiKey = Environment.GetEnvironmentVariable("CHUMMER_API_KEY");
        string? previousDefaultRulesetId = Environment.GetEnvironmentVariable(DefaultRulesetEnvironmentVariable);
        string? previousDesktopStateRoot = Environment.GetEnvironmentVariable(DesktopStateRootEnvironmentVariable);
        string? previousStatePath = Environment.GetEnvironmentVariable(StatePathEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_CLIENT_MODE", mode);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_CLIENT_MODE", legacyMode);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", baseUrl);
            Environment.SetEnvironmentVariable("CHUMMER_API_KEY", apiKey);
            Environment.SetEnvironmentVariable(DefaultRulesetEnvironmentVariable, defaultRulesetId);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_CLIENT_MODE", previousMode);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_CLIENT_MODE", previousLegacyMode);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousBaseUrl);
            Environment.SetEnvironmentVariable("CHUMMER_API_KEY", previousApiKey);
            Environment.SetEnvironmentVariable(DefaultRulesetEnvironmentVariable, previousDefaultRulesetId);
            Environment.SetEnvironmentVariable(DesktopStateRootEnvironmentVariable, previousDesktopStateRoot);
            Environment.SetEnvironmentVariable(StatePathEnvironmentVariable, previousStatePath);
        }
    }

    private static ServiceProvider BuildProvider(string root, string desktopHeadId)
    {
        var services = new ServiceCollection();
        services.AddChummerLocalRuntimeClient(root, root, desktopHeadId);
        return services.BuildServiceProvider();
    }

    private static void WriteClaimedInstallState(string desktopStateRoot, string headId, string subjectId, string userId)
    {
        string platform = OperatingSystem.IsWindows()
            ? "windows"
            : OperatingSystem.IsMacOS()
                ? "macos"
                : OperatingSystem.IsLinux()
                    ? "linux"
                    : "unknown";
        string arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()
        };

        string stateDirectory = Path.Combine(desktopStateRoot, "Chummer6", "install-linking", headId, platform, arch);
        Directory.CreateDirectory(stateDirectory);

        DesktopInstallLinkingState state = new(
            InstallationId: $"ins-{headId}",
            HeadId: headId,
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: platform,
            Arch: arch,
            Status: "claimed",
            CreatedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            LaunchCount: 1,
            LastStartedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            ClaimedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            LastPromptDismissedAtUtc: null,
            PublicKey: "public-key",
            PrivateKey: OperatingSystem.IsWindows() ? string.Empty : "private-key",
            ClaimTicketId: "ticket-1",
            LastClaimCode: null,
            LastClaimMessage: "linked",
            LastClaimError: null,
            LastClaimAttemptUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            GrantId: "grant-1",
            GrantToken: "grant-token",
            GrantIssuedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            GrantExpiresAtUtc: DateTimeOffset.Parse("2026-07-03T12:00:00+00:00"),
            UserId: userId,
            SubjectId: subjectId);
        File.WriteAllText(Path.Combine(stateDirectory, "state.json"), JsonSerializer.Serialize(state));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "chummer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests.
        }
    }
}
