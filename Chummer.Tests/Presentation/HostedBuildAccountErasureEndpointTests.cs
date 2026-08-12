using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Chummer.Blazor.Services;
using Chummer.Contracts.Owners;
using Chummer.Workspaces.Postgres;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class HostedBuildAccountErasureEndpointTests
{
    private const string AdminKey = "privacy-admin-key-with-at-least-32-bytes";

    [TestMethod]
    public async Task Erase_derives_the_exact_authenticated_owner_and_returns_only_a_content_free_receipt()
    {
        var store = new RecordingPrivacyStore();
        using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IWorkspacePrivacyLifecycleStore>(store)
            .BuildServiceProvider();
        HostedBuildAccountErasureEndpoint endpoint = CreateEndpoint(services);
        DefaultHttpContext context = CreateContext(services, AdminKey);

        IResult result = endpoint.Erase(context.Request, "subject.private-user");
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.IsNotNull(store.Owner);
        StringAssert.StartsWith(store.Owner.Value.NormalizedValue, "authenticated-v2-");
        Assert.IsFalse(store.Owner.Value.NormalizedValue.Contains("subject.private-user", StringComparison.Ordinal));
        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body);
        JsonElement root = document.RootElement;
        Assert.IsTrue(root.GetProperty("erased").GetBoolean());
        Assert.AreEqual(2, root.GetProperty("workspaceRowsRemoved").GetInt32());
        Assert.AreEqual(new string('a', 64), root.GetProperty("receiptSha256").GetString());
        Assert.IsFalse(root.GetRawText().Contains("subject.private-user", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Erase_rejects_the_wrong_admin_key_without_touching_the_store()
    {
        var store = new RecordingPrivacyStore();
        using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IWorkspacePrivacyLifecycleStore>(store)
            .BuildServiceProvider();
        HostedBuildAccountErasureEndpoint endpoint = CreateEndpoint(services);
        DefaultHttpContext context = CreateContext(services, "wrong-key-with-at-least-thirty-two-bytes");

        IResult result = endpoint.Erase(context.Request, "subject.private-user");
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.IsNull(store.Owner);
    }

    private static HostedBuildAccountErasureEndpoint CreateEndpoint(IServiceProvider services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HostedBuildAccountErasureEndpoint.PrivacyAdminKeyConfiguration] = AdminKey
            })
            .Build();
        HostedBuildOwnerAuthenticationOptions authentication =
            HostedBuildOwnerAuthenticationOptions.Create(
                "https://identity.chummer.test",
                "chummer-build",
                "Bearer");
        var owners = new HostedBuildOwnerGrantService(
            new EphemeralDataProtectionProvider(),
            authentication);
        return new HostedBuildAccountErasureEndpoint(
            configuration,
            owners,
            services,
            NullLogger<HostedBuildAccountErasureEndpoint>.Instance);
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, string adminKey)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() }
        };
        context.Request.Headers[HostedBuildAccountErasureEndpoint.PrivacyAdminKeyHeader] = adminKey;
        return context;
    }

    private sealed class RecordingPrivacyStore : IWorkspacePrivacyLifecycleStore
    {
        public OwnerScope? Owner { get; private set; }

        public WorkspaceOwnerErasureResult EraseOwner(OwnerScope owner)
        {
            Owner = owner;
            return new WorkspaceOwnerErasureResult(
                true,
                Guid.NewGuid(),
                new DateTimeOffset(2026, 8, 12, 12, 0, 0, TimeSpan.Zero),
                2,
                new string('a', 64));
        }

        public WorkspacePrivacyMaintenanceResult ApplyDeletionReplay(OwnerScope owner)
            => throw new NotSupportedException();

        public WorkspacePrivacyMaintenanceResult ApplyAllDeletionReplay()
            => throw new NotSupportedException();

        public WorkspacePrivacyMaintenanceResult PurgeExpiredDeletionAuditReceipts()
            => throw new NotSupportedException();
    }
}
