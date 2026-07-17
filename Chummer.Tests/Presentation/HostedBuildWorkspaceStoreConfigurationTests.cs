#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Chummer.Application.Workspaces;
using Chummer.Blazor.Services;
using Chummer.Infrastructure.Workspaces;
using Chummer.Workspaces.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class HostedBuildWorkspaceStoreConfigurationTests
{
    [TestMethod]
    public void Nonproduction_defaults_to_the_existing_file_store_and_reports_single_instance()
    {
        string stateDirectory = Path.Combine(
            Path.GetTempPath(),
            $"chummer-hosted-build-store-configuration-{Guid.NewGuid():N}");
        try
        {
            FileWorkspaceStore existingStore = new(stateDirectory);
            ServiceCollection services = new();
            services.AddSingleton<IWorkspaceStore>(existingStore);
            services.AddSingleton<IWorkspaceStoreReadinessProbe>(existingStore);

            services.AddHostedBuildWorkspaceStore(
                BuildConfiguration(),
                new TestHostEnvironment(Environments.Staging));

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            Assert.AreSame(existingStore, serviceProvider.GetRequiredService<IWorkspaceStore>());
            Assert.AreSame(existingStore, serviceProvider.GetRequiredService<IWorkspaceStoreReadinessProbe>());

            HostedBuildWorkspaceStoreSelection selection =
                serviceProvider.GetRequiredService<HostedBuildWorkspaceStoreSelection>();
            Assert.AreEqual("file", selection.Provider);
            Assert.IsFalse(selection.MultiInstanceSafe);
            Assert.AreEqual("single_instance_local_filesystem", selection.DurabilityBoundary);
        }
        finally
        {
            if (Directory.Exists(stateDirectory))
            {
                Directory.Delete(stateDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Production_requires_an_explicit_provider()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new ServiceCollection().AddHostedBuildWorkspaceStore(
                BuildConfiguration(),
                new TestHostEnvironment(Environments.Production)));

        StringAssert.Contains(
            exception.Message,
            HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey);
    }

    [TestMethod]
    public void File_provider_rejects_an_expected_replica_count_greater_than_one()
    {
        IConfiguration configuration = BuildConfiguration(
            (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "file"),
            (HostedBuildWorkspaceStoreConfiguration.ExpectedReplicaCountConfigurationKey, "2"));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new ServiceCollection().AddHostedBuildWorkspaceStore(
                configuration,
                new TestHostEnvironment(Environments.Staging)));

        StringAssert.Contains(
            exception.Message,
            HostedBuildWorkspaceStoreConfiguration.ExpectedReplicaCountConfigurationKey);
    }

    [TestMethod]
    public void Postgres_provider_requires_a_connection_string()
    {
        IConfiguration configuration = BuildConfiguration(
            (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql"));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new ServiceCollection().AddHostedBuildWorkspaceStore(
                configuration,
                new TestHostEnvironment(Environments.Staging)));

        StringAssert.Contains(
            exception.Message,
            HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey);
    }

    [TestMethod]
    public void Production_postgres_rejects_transport_without_hostname_verification()
    {
        string key = HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey;
        string? previousEnvironmentValue = Environment.GetEnvironmentVariable(key);
        string secretDirectory = CreateProductionSecretDirectory(
            "Host=database.internal;Database=chummer;Username=chummer;Password=test-only;SSL Mode=VerifyCA");
        try
        {
            Environment.SetEnvironmentVariable(key, null);
            IConfiguration configuration = BuildConfiguration(
                (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql"));

            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                new ServiceCollection().AddHostedBuildWorkspaceStore(
                    configuration,
                    new TestHostEnvironment(Environments.Production),
                    secretDirectory));

            StringAssert.Contains(exception.Message, "VerifyFull");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previousEnvironmentValue);
            Directory.Delete(secretDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Production_postgres_reads_only_the_approved_secret_file_source()
    {
        string key = HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey;
        string? previousEnvironmentValue = Environment.GetEnvironmentVariable(key);
        string secretDirectory = Path.Combine(
            Path.GetTempPath(),
            $"chummer-postgres-empty-secret-{Guid.NewGuid():N}");
        Directory.CreateDirectory(secretDirectory);
        SetSecureDirectoryPermissions(secretDirectory);
        try
        {
            Environment.SetEnvironmentVariable(key, null);
            IConfiguration configuration = BuildConfiguration(
                (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql"),
                (key, "Host=unapproved.example;Database=chummer;Username=chummer;Password=unapproved;SSL Mode=VerifyFull"));

            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                new ServiceCollection().AddHostedBuildWorkspaceStore(
                    configuration,
                    new TestHostEnvironment(Environments.Production),
                    secretDirectory));

            StringAssert.Contains(exception.Message, "secret");
            Assert.IsFalse(exception.ToString().Contains("unapproved", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previousEnvironmentValue);
            Directory.Delete(secretDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Production_postgres_accepts_a_secure_verify_full_secret_file()
    {
        string key = HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey;
        string? previousEnvironmentValue = Environment.GetEnvironmentVariable(key);
        string secretDirectory = CreateProductionSecretDirectory(
            "Host=database.internal;Database=chummer;Username=chummer;Password=test-only;SSL Mode=VerifyFull");
        try
        {
            Environment.SetEnvironmentVariable(key, null);
            ServiceCollection services = new();
            services.AddHostedBuildWorkspaceStore(
                BuildConfiguration(
                    (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql"),
                    (HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey, "10"),
                    (HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey, "10")),
                new TestHostEnvironment(Environments.Production),
                secretDirectory);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            Assert.AreEqual(
                "postgresql",
                serviceProvider.GetRequiredService<HostedBuildWorkspaceStoreSelection>().Provider);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previousEnvironmentValue);
            Directory.Delete(secretDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Production_postgres_rejects_a_symbolic_link_secret_file_without_logging_its_target()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Symbolic-link creation is not reliably available to an unprivileged Windows test process.");
            return;
        }

        const string secretMarker = "symlink-secret-4f43ec96";
        string secretDirectory = CreateSecureProductionSecretDirectory();
        string targetPath = Path.Combine(secretDirectory, "actual-connection-string");
        string secretPath = Path.Combine(
            secretDirectory,
            HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey);
        try
        {
            File.WriteAllText(
                targetPath,
                $"Host=database.internal;Database=chummer;Username=chummer;Password={secretMarker};SSL Mode=VerifyFull");
            SetSecureSecretFilePermissions(targetPath);
            File.CreateSymbolicLink(secretPath, targetPath);

            InvalidOperationException exception =
                ConfigureProductionSecretDirectoryExpectFailure(secretDirectory);

            AssertSanitizedSecretFailure(exception, secretMarker);
        }
        finally
        {
            Directory.Delete(secretDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Production_postgres_rejects_group_readable_secret_file_without_logging_it()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix mode validation is not available on Windows.");
            return;
        }

        const string secretMarker = "permission-secret-8c5bca72";
        string secretDirectory = CreateProductionSecretDirectory(
            $"Host=database.internal;Database=chummer;Username=chummer;Password={secretMarker};SSL Mode=VerifyFull");
        string secretPath = Path.Combine(
            secretDirectory,
            HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey);
        try
        {
            File.SetUnixFileMode(
                secretPath,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead);

            InvalidOperationException exception =
                ConfigureProductionSecretDirectoryExpectFailure(secretDirectory);

            StringAssert.Contains(exception.Message, "permissions");
            AssertSanitizedSecretFailure(exception, secretMarker);
        }
        finally
        {
            Directory.Delete(secretDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Production_postgres_rejects_group_writable_secret_directory_without_logging_the_secret()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix mode validation is not available on Windows.");
            return;
        }

        const string secretMarker = "parent-permission-secret-f5b8af69";
        string secretDirectory = CreateProductionSecretDirectory(
            $"Host=database.internal;Database=chummer;Username=chummer;Password={secretMarker};SSL Mode=VerifyFull");
        try
        {
            File.SetUnixFileMode(
                secretDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupWrite
                | UnixFileMode.GroupExecute);

            InvalidOperationException exception =
                ConfigureProductionSecretDirectoryExpectFailure(secretDirectory);

            StringAssert.Contains(exception.Message, "directory permissions");
            AssertSanitizedSecretFailure(exception, secretMarker);
        }
        finally
        {
            SetSecureDirectoryPermissions(secretDirectory);
            Directory.Delete(secretDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Production_postgres_rejects_a_secret_larger_than_the_bounded_reader_limit()
    {
        string secretDirectory = CreateProductionSecretDirectory(new string('x', 32769));
        try
        {
            InvalidOperationException exception =
                ConfigureProductionSecretDirectoryExpectFailure(secretDirectory);

            StringAssert.Contains(exception.Message, "secret");
            Assert.IsNull(exception.InnerException);
        }
        finally
        {
            Directory.Delete(secretDirectory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey)]
    [DataRow(HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey)]
    public void Production_postgres_requires_explicit_connection_budgets(string omittedKey)
    {
        List<(string Key, string? Value)> settings =
        [
            (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql")
        ];
        if (!string.Equals(
                omittedKey,
                HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey,
                StringComparison.Ordinal))
        {
            settings.Add(
                (HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey, "10"));
        }

        if (!string.Equals(
                omittedKey,
                HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey,
                StringComparison.Ordinal))
        {
            settings.Add(
                (HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey, "10"));
        }

        InvalidOperationException exception = ConfigureProductionPostgresExpectFailure(
            settings.ToArray());

        StringAssert.Contains(exception.Message, omittedKey);
    }

    [TestMethod]
    [DataRow(HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey, "0")]
    [DataRow(HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey, "257")]
    [DataRow(HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey, "0")]
    [DataRow(HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey, "262145")]
    [DataRow(HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey, "not-an-integer")]
    public void Production_postgres_rejects_invalid_connection_budgets(
        string invalidKey,
        string invalidValue)
    {
        string maximumPoolSize = string.Equals(
            invalidKey,
            HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey,
            StringComparison.Ordinal)
            ? invalidValue
            : "10";
        string aggregateBudget = string.Equals(
            invalidKey,
            HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey,
            StringComparison.Ordinal)
            ? invalidValue
            : "10";

        InvalidOperationException exception = ConfigureProductionPostgresExpectFailure(
            (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql"),
            (HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey, maximumPoolSize),
            (HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey, aggregateBudget));

        StringAssert.Contains(exception.Message, invalidKey);
    }

    [TestMethod]
    public void Production_postgres_rejects_replica_pool_product_above_aggregate_budget()
    {
        InvalidOperationException exception = ConfigureProductionPostgresExpectFailure(
            (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql"),
            (HostedBuildWorkspaceStoreConfiguration.ExpectedReplicaCountConfigurationKey, "1024"),
            (HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey, "256"),
            (HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey, "262143"));

        StringAssert.Contains(
            exception.Message,
            HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey);
    }

    [TestMethod]
    public void Nonproduction_postgres_applies_explicit_replica_and_pool_budget()
    {
        ServiceCollection services = new();
        services.AddHostedBuildWorkspaceStore(
            BuildConfiguration(
                (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql"),
                (HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey,
                    "Host=127.0.0.1;Database=chummer;Username=chummer;Password=test-only;SSL Mode=Disable"),
                (HostedBuildWorkspaceStoreConfiguration.ExpectedReplicaCountConfigurationKey, "2"),
                (HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey, "7"),
                (HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey, "14")),
            new TestHostEnvironment(Environments.Staging));

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        Assert.AreEqual(
            7,
            serviceProvider.GetRequiredService<PostgresWorkspaceStoreOptions>().MaximumPoolSize);
    }

    [TestMethod]
    public void Invalid_connection_configuration_does_not_retain_parser_diagnostics()
    {
        const string secretMarker = "do-not-log-7fdbb209";
        IConfiguration configuration = BuildConfiguration(
            (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql"),
            (HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey,
                $"Host=database.internal;Password={secretMarker};Unsupported Secret={secretMarker}"));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new ServiceCollection().AddHostedBuildWorkspaceStore(
                configuration,
                new TestHostEnvironment(Environments.Staging)));

        Assert.IsNull(exception.InnerException);
        Assert.IsFalse(exception.ToString().Contains(secretMarker, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Postgres_replaces_both_store_aliases_without_opening_a_connection_and_reports_shared_transactional()
    {
        string displacedStateDirectory = Path.Combine(
            Path.GetTempPath(),
            $"chummer-displaced-workspace-store-{Guid.NewGuid():N}");
        try
        {
            IConfiguration configuration = BuildConfiguration(
                (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgres"),
                (HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey,
                    "Host=127.0.0.1;Port=1;Database=chummer;Username=chummer;Password=test-only;SSL Mode=Disable;Timeout=1"));
            FileWorkspaceStore displacedStore = new(displacedStateDirectory);
            ServiceCollection services = new();
            services.AddSingleton<IWorkspaceStore>(displacedStore);
            services.AddSingleton<IWorkspaceStoreReadinessProbe>(displacedStore);

            services.AddHostedBuildWorkspaceStore(
                configuration,
                new TestHostEnvironment(Environments.Staging));

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            PostgresWorkspaceStore concrete = serviceProvider.GetRequiredService<PostgresWorkspaceStore>();
            Assert.AreSame(concrete, serviceProvider.GetRequiredService<IWorkspaceStore>());
            Assert.AreSame(concrete, serviceProvider.GetRequiredService<IWorkspaceStoreReadinessProbe>());
            Assert.AreNotSame<IWorkspaceStore>(displacedStore, concrete);

            HostedBuildWorkspaceStoreSelection selection =
                serviceProvider.GetRequiredService<HostedBuildWorkspaceStoreSelection>();
            Assert.AreEqual("postgresql", selection.Provider);
            Assert.IsTrue(selection.MultiInstanceSafe);
            Assert.AreEqual("shared_transactional_postgresql", selection.DurabilityBoundary);
            Assert.AreEqual(
                PostgresWorkspaceStoreOptions.DefaultMaximumPoolSize,
                serviceProvider.GetRequiredService<PostgresWorkspaceStoreOptions>().MaximumPoolSize);
        }
        finally
        {
            if (Directory.Exists(displacedStateDirectory))
            {
                Directory.Delete(displacedStateDirectory, recursive: true);
            }
        }
    }

    private static InvalidOperationException ConfigureProductionPostgresExpectFailure(
        params (string Key, string? Value)[] settings)
    {
        string key = HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey;
        string? previousEnvironmentValue = Environment.GetEnvironmentVariable(key);
        string secretDirectory = CreateProductionSecretDirectory(
            "Host=database.internal;Database=chummer;Username=chummer;Password=test-only;SSL Mode=VerifyFull");
        try
        {
            Environment.SetEnvironmentVariable(key, null);
            return Assert.ThrowsExactly<InvalidOperationException>(() =>
                new ServiceCollection().AddHostedBuildWorkspaceStore(
                    BuildConfiguration(settings),
                    new TestHostEnvironment(Environments.Production),
                    secretDirectory));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previousEnvironmentValue);
            Directory.Delete(secretDirectory, recursive: true);
        }
    }

    private static InvalidOperationException ConfigureProductionSecretDirectoryExpectFailure(
        string secretDirectory)
    {
        string key = HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey;
        string? previousEnvironmentValue = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, null);
            return Assert.ThrowsExactly<InvalidOperationException>(() =>
                new ServiceCollection().AddHostedBuildWorkspaceStore(
                    BuildConfiguration(
                        (HostedBuildWorkspaceStoreConfiguration.ProviderConfigurationKey, "postgresql"),
                        (HostedBuildWorkspaceStoreConfiguration.MaximumPoolSizeConfigurationKey, "10"),
                        (HostedBuildWorkspaceStoreConfiguration.AggregateConnectionBudgetConfigurationKey, "10")),
                    new TestHostEnvironment(Environments.Production),
                    secretDirectory));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previousEnvironmentValue);
        }
    }

    private static void AssertSanitizedSecretFailure(
        InvalidOperationException exception,
        string secretMarker)
    {
        StringAssert.Contains(exception.Message, "secret");
        Assert.IsNull(exception.InnerException);
        Assert.IsFalse(exception.ToString().Contains(secretMarker, StringComparison.Ordinal));
    }

    private static IConfiguration BuildConfiguration(
        params (string Key, string? Value)[] values)
    {
        List<KeyValuePair<string, string?>> settings = new(values.Length);
        foreach ((string key, string? value) in values)
        {
            settings.Add(new KeyValuePair<string, string?>(key, value));
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static string CreateProductionSecretDirectory(string connectionString)
    {
        string directory = CreateSecureProductionSecretDirectory();
        string path = Path.Combine(
            directory,
            HostedBuildWorkspaceStoreConfiguration.ConnectionStringConfigurationKey);
        File.WriteAllText(path, connectionString);
        SetSecureSecretFilePermissions(path);

        return directory;
    }

    private static string CreateSecureProductionSecretDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"chummer-postgres-secret-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        SetSecureDirectoryPermissions(directory);
        return directory;
    }

    private static void SetSecureDirectoryPermissions(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
        }
    }

    private static void SetSecureSecretFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Chummer.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
