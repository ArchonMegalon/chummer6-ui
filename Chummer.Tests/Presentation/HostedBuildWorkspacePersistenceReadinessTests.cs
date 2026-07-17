#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Workspaces;
using Chummer.Blazor.Services;
using Chummer.Contracts.Owners;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class HostedBuildWorkspacePersistenceReadinessTests
{
    [TestMethod]
    public void Check_uses_a_non_local_owner_scoped_probe()
    {
        var probe = new RecordingWorkspaceStoreReadinessProbe();
        var readiness = new HostedBuildWorkspacePersistenceReadiness(
            () => probe,
            NullLogger<HostedBuildWorkspacePersistenceReadiness>.Instance,
            HostedBuildWorkspacePersistenceReadinessOptions.Default);

        HostedBuildWorkspacePersistenceStatus status = readiness.Check();

        Assert.IsTrue(status.Ready);
        Assert.AreEqual("ready", status.Status);
        Assert.AreEqual(1, probe.Owners.Count);
        Assert.IsFalse(probe.Owners[0].IsLocalSingleUser);
        Assert.IsFalse(string.IsNullOrWhiteSpace(probe.Owners[0].NormalizedValue));
    }

    [TestMethod]
    public void Check_fails_closed_without_leaking_the_storage_error_and_can_recover()
    {
        var probe = new RecordingWorkspaceStoreReadinessProbe
        {
            FailuresRemaining = 1
        };
        var readiness = new HostedBuildWorkspacePersistenceReadiness(
            () => probe,
            NullLogger<HostedBuildWorkspacePersistenceReadiness>.Instance,
            new HostedBuildWorkspacePersistenceReadinessOptions(
                ProbeResponseTimeout: TimeSpan.FromSeconds(1),
                CacheDuration: TimeSpan.Zero));

        HostedBuildWorkspacePersistenceStatus unavailable = readiness.Check();
        HostedBuildWorkspacePersistenceStatus recovered = readiness.Check();

        Assert.IsFalse(unavailable.Ready);
        Assert.AreEqual("unavailable", unavailable.Status);
        Assert.IsFalse(
            unavailable.ToString().Contains(
                RecordingWorkspaceStoreReadinessProbe.PrivateFailureMarker,
                StringComparison.Ordinal));
        Assert.IsTrue(recovered.Ready);
        Assert.AreEqual("ready", recovered.Status);
        Assert.AreEqual(2, probe.Owners.Count);
        Assert.AreEqual(probe.Owners[0], probe.Owners[1]);
    }

    [TestMethod]
    public async Task Concurrent_checks_share_one_probe_and_reuse_its_cached_result()
    {
        using var probe = new BlockingWorkspaceStoreReadinessProbe();
        var readiness = new HostedBuildWorkspacePersistenceReadiness(
            () => probe,
            NullLogger<HostedBuildWorkspacePersistenceReadiness>.Instance,
            new HostedBuildWorkspacePersistenceReadinessOptions(
                ProbeResponseTimeout: TimeSpan.FromSeconds(2),
                CacheDuration: TimeSpan.FromMinutes(1)));

        Task<HostedBuildWorkspacePersistenceStatus>[] checks = Enumerable
            .Range(0, 32)
            .Select(_ => readiness.CheckAsync().AsTask())
            .ToArray();
        try
        {
            Assert.IsTrue(probe.Entered.Wait(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(1, probe.CallCount);

            probe.Release.Set();
            HostedBuildWorkspacePersistenceStatus[] statuses = await Task
                .WhenAll(checks)
                .WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(statuses.All(status => status.Ready));

            HostedBuildWorkspacePersistenceStatus cached = await readiness.CheckAsync();
            Assert.IsTrue(cached.Ready);
            Assert.AreEqual(1, probe.CallCount,
                "A cached public readiness read must not perform another durable write.");
        }
        finally
        {
            probe.Release.Set();
        }
    }

    [TestMethod]
    public async Task Eager_probe_and_public_wait_are_bounded_without_spawning_replacements()
    {
        using var probe = new BlockingWorkspaceStoreReadinessProbe();
        var readiness = new HostedBuildWorkspacePersistenceReadiness(
            () => probe,
            NullLogger<HostedBuildWorkspacePersistenceReadiness>.Instance,
            new HostedBuildWorkspacePersistenceReadinessOptions(
                ProbeResponseTimeout: TimeSpan.FromMilliseconds(50),
                CacheDuration: TimeSpan.FromMinutes(1)));

        try
        {
            await Task.Run(readiness.StartProbe).WaitAsync(TimeSpan.FromSeconds(1));
            Assert.IsTrue(probe.Entered.Wait(TimeSpan.FromSeconds(2)));

            HostedBuildWorkspacePersistenceStatus first = await readiness
                .CheckAsync()
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(1));
            ValueTask<HostedBuildWorkspacePersistenceStatus> cachedTimeout =
                readiness.CheckAsync();
            Assert.IsTrue(cachedTimeout.IsCompletedSuccessfully,
                "The bounded unavailable result must be cached for subsequent public requests.");
            HostedBuildWorkspacePersistenceStatus second = await cachedTimeout;

            Assert.IsFalse(first.Ready);
            Assert.IsFalse(second.Ready);
            Assert.AreEqual(1, probe.CallCount,
                "Timed-out callers must retain the one blocked probe instead of accumulating workers.");

            probe.Release.Set();
            await probe.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            HostedBuildWorkspacePersistenceStatus recovered = await readiness
                .CheckAsync()
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(recovered.Ready);
            Assert.AreEqual(1, probe.CallCount);
        }
        finally
        {
            probe.Release.Set();
        }
    }

    [TestMethod]
    public async Task Probe_resolution_can_block_without_delaying_startup_or_liveness()
    {
        Assert.AreEqual(
            1,
            typeof(HostedBuildWorkspacePersistenceReadiness).GetConstructors().Length,
            "The readiness coordinator must expose one unambiguous DI constructor.");
        using var resolverEntered = new ManualResetEventSlim(initialState: false);
        using var releaseResolver = new ManualResetEventSlim(initialState: false);
        var probe = new RecordingWorkspaceStoreReadinessProbe();
        int resolverCalls = 0;
        using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IWorkspaceStoreReadinessProbe>(_ =>
            {
                Interlocked.Increment(ref resolverCalls);
                resolverEntered.Set();
                if (!releaseResolver.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("The test did not release probe construction.");
                }

                return probe;
            })
            .AddHostedBuildWorkspacePersistenceReadiness()
            .BuildServiceProvider();

        try
        {
            HostedBuildWorkspacePersistenceReadiness readiness = await Task
                .Run(() => services.GetRequiredService<HostedBuildWorkspacePersistenceReadiness>())
                .WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(0, resolverCalls,
                "Resolving the readiness coordinator must not construct the filesystem store.");

            await Task.Run(readiness.StartProbe).WaitAsync(TimeSpan.FromSeconds(1));
            Assert.IsTrue(resolverEntered.Wait(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(1, resolverCalls);

            foreach (string configuredPathBase in new[] { "", "/blazor" })
            {
                PathString pathBase = new(configuredPathBase);
                var pipelineBuilder = new ApplicationBuilder(services);
                pipelineBuilder.UseHostedBuildHealthChecks(
                    pathBase,
                    () => Results.Ok(new { ok = true, check = "liveness" }),
                    _ => Task.FromResult<IResult>(Results.StatusCode(
                        StatusCodes.Status503ServiceUnavailable)));
                pipelineBuilder.Run(context =>
                {
                    context.Response.Cookies.Append(
                        "chummer_build_owner",
                        "should-not-be-issued");
                    return Task.CompletedTask;
                });
                RequestDelegate pipeline = pipelineBuilder.Build();
                DefaultHttpContext liveness = CreateHttpContext(
                    services,
                    pathBase.Add(new PathString("/health/live")),
                    HttpMethods.Get);

                await pipeline(liveness).WaitAsync(TimeSpan.FromSeconds(1));

                Assert.AreEqual(StatusCodes.Status200OK, liveness.Response.StatusCode);
                Assert.AreEqual("no-store", liveness.Response.Headers.CacheControl.ToString());
                Assert.IsFalse(liveness.Response.Headers.ContainsKey("Set-Cookie"));
            }

            releaseResolver.Set();
            HostedBuildWorkspacePersistenceStatus recovered = await readiness
                .CheckAsync()
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(recovered.Ready);
            Assert.AreEqual(1, resolverCalls);
            Assert.HasCount(1, probe.Owners);
        }
        finally
        {
            releaseResolver.Set();
        }
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("/blazor")]
    public async Task Exact_health_paths_terminate_before_owner_cookie_middleware(
        string configuredPathBase)
    {
        using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        int downstreamCalls = 0;
        var pipelineBuilder = new ApplicationBuilder(services);
        PathString pathBase = new(configuredPathBase);
        pipelineBuilder.UseHostedBuildHealthChecks(
            pathBase,
            () => Results.Ok(new { ok = true, check = "liveness" }),
            _ => Task.FromResult<IResult>(Results.Ok(new { ok = true, check = "readiness" })));
        pipelineBuilder.Run(context =>
        {
            Interlocked.Increment(ref downstreamCalls);
            context.Response.Cookies.Append("chummer_build_owner", "allocated-downstream");
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        RequestDelegate pipeline = pipelineBuilder.Build();

        foreach (string relativePath in new[] { "/health/live", "/health/ready", "/health" })
        {
            DefaultHttpContext context = CreateHttpContext(
                services,
                pathBase.Add(new PathString(relativePath)),
                HttpMethods.Get);
            context.Request.QueryString = new QueryString("?probe=operator");

            await pipeline(context);

            Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.AreEqual("no-store", context.Response.Headers.CacheControl.ToString());
            Assert.IsFalse(context.Response.Headers.ContainsKey("Set-Cookie"));
            Assert.AreEqual(0, downstreamCalls);
        }

        DefaultHttpContext rejectedMethod = CreateHttpContext(
            services,
            pathBase.Add(new PathString("/health/ready")),
            HttpMethods.Post);
        await pipeline(rejectedMethod);
        Assert.AreEqual(StatusCodes.Status405MethodNotAllowed, rejectedMethod.Response.StatusCode);
        Assert.AreEqual("GET, HEAD", rejectedMethod.Response.Headers.Allow.ToString());
        Assert.IsFalse(rejectedMethod.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.AreEqual(0, downstreamCalls);

        DefaultHttpContext application = CreateHttpContext(
            services,
            pathBase.Add(new PathString("/app")),
            HttpMethods.Get);
        await pipeline(application);
        Assert.AreEqual(StatusCodes.Status204NoContent, application.Response.StatusCode);
        StringAssert.Contains(
            application.Response.Headers.SetCookie.ToString(),
            "chummer_build_owner=allocated-downstream");
        Assert.AreEqual(1, downstreamCalls);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("/blazor")]
    public async Task Runtime_readiness_abuse_times_out_single_flight_without_cookie_and_recovers(
        string configuredPathBase)
    {
        using var probe = new BlockingFailureThenSuccessProbe();
        var readiness = new HostedBuildWorkspacePersistenceReadiness(
            () => probe,
            NullLogger<HostedBuildWorkspacePersistenceReadiness>.Instance,
            new HostedBuildWorkspacePersistenceReadinessOptions(
                ProbeResponseTimeout: TimeSpan.FromMilliseconds(50),
                CacheDuration: TimeSpan.FromMilliseconds(75)));
        using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        int downstreamCalls = 0;
        PathString pathBase = new(configuredPathBase);
        var pipelineBuilder = new ApplicationBuilder(services);
        pipelineBuilder.UseHostedBuildHealthChecks(
            pathBase,
            () => Results.Ok(new { ok = true, check = "liveness" }),
            async cancellationToken =>
            {
                HostedBuildWorkspacePersistenceStatus persistence =
                    await readiness.CheckAsync(cancellationToken);
                var payload = new
                {
                    ok = persistence.Ready,
                    check = "readiness",
                    workspacePersistence = persistence.Status
                };
                return persistence.Ready
                    ? Results.Ok(payload)
                    : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
            });
        pipelineBuilder.Run(context =>
        {
            Interlocked.Increment(ref downstreamCalls);
            context.Response.Cookies.Append("chummer_build_owner", "should-not-be-issued");
            return Task.CompletedTask;
        });
        RequestDelegate pipeline = pipelineBuilder.Build();

        readiness.StartProbe();
        try
        {
            Assert.IsTrue(probe.Entered.Wait(TimeSpan.FromSeconds(2)));
            Task<DefaultHttpContext>[] requests = Enumerable
                .Range(0, 32)
                .Select(async _ =>
                {
                    DefaultHttpContext context = CreateHttpContext(
                        services,
                        pathBase.Add(new PathString("/health/ready")),
                        HttpMethods.Get);
                    await pipeline(context);
                    return context;
                })
                .ToArray();
            DefaultHttpContext[] unavailable = await Task
                .WhenAll(requests)
                .WaitAsync(TimeSpan.FromSeconds(2));

            Assert.AreEqual(1, probe.CallCount,
                "Concurrent public requests must share the one durable probe.");
            Assert.AreEqual(0, downstreamCalls);
            foreach (DefaultHttpContext context in unavailable)
            {
                Assert.AreEqual(
                    StatusCodes.Status503ServiceUnavailable,
                    context.Response.StatusCode);
                Assert.AreEqual("no-store", context.Response.Headers.CacheControl.ToString());
                Assert.IsFalse(context.Response.Headers.ContainsKey("Set-Cookie"));
                string body = ReadResponseBody(context);
                Assert.IsFalse(body.Contains(
                    BlockingFailureThenSuccessProbe.PrivateFailureMarker,
                    StringComparison.Ordinal));
                using JsonDocument payload = JsonDocument.Parse(body);
                Assert.AreEqual(
                    "unavailable",
                    payload.RootElement.GetProperty("workspacePersistence").GetString());
            }

            probe.Release.Set();
            await probe.FirstCallCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            DefaultHttpContext? recovered = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                DefaultHttpContext candidate = CreateHttpContext(
                    services,
                    pathBase.Add(new PathString("/health/ready")),
                    HttpMethods.Get);
                await pipeline(candidate);
                if (candidate.Response.StatusCode == StatusCodes.Status200OK)
                {
                    recovered = candidate;
                    break;
                }
            }

            Assert.IsNotNull(recovered, "Readiness did not recover after a fresh successful probe.");
            Assert.AreEqual(2, probe.CallCount);
            Assert.IsFalse(recovered.Response.Headers.ContainsKey("Set-Cookie"));
            using JsonDocument recoveredPayload = JsonDocument.Parse(ReadResponseBody(recovered));
            Assert.AreEqual(
                "ready",
                recoveredPayload.RootElement.GetProperty("workspacePersistence").GetString());
        }
        finally
        {
            probe.Release.Set();
        }
    }

    [TestMethod]
    public void Hosted_blazor_compose_services_have_explicit_independent_durable_state()
    {
        string compose = File.ReadAllText(Path.Combine(
            TestContextLocator.ResolveChummerPresentationRepoRoot(),
            "docker-compose.yml"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        string standalone = ExtractService(compose, "chummer-blazor");
        string portal = ExtractService(compose, "chummer-blazor-portal");

        StringAssert.Contains(standalone, "CHUMMER_STATE_PATH: \"/app/state\"");
        StringAssert.Contains(standalone, "- chummer-blazor-state:/app/state");
        StringAssert.Contains(standalone, "/health/ready");
        Assert.IsFalse(standalone.Contains("chummer-blazor-portal-state", StringComparison.Ordinal));

        StringAssert.Contains(portal, "CHUMMER_STATE_PATH: \"/app/state\"");
        StringAssert.Contains(portal, "- chummer-blazor-portal-state:/app/state");
        StringAssert.Contains(portal, "/health/ready");
        Assert.IsFalse(portal.Contains("- chummer-blazor-state:/app/state", StringComparison.Ordinal));

        Assert.AreEqual(
            2,
            CountOccurrences(
                compose,
                "dockerfile: chummer-presentation/Chummer.Blazor/Dockerfile"));
        Assert.AreEqual(
            2,
            new[] { standalone, portal }
                .Sum(value => CountOccurrences(value, "CHUMMER_STATE_PATH: \"/app/state\"")));
        StringAssert.Contains(compose, "\n  chummer-blazor-state:\n");
        StringAssert.Contains(compose, "\n  chummer-blazor-portal-state:\n");
    }

    [TestMethod]
    public void Hosted_blazor_program_eagerly_probes_and_separates_liveness_from_readiness()
    {
        string program = File.ReadAllText(Path.Combine(
            TestContextLocator.ResolveChummerPresentationRepoRoot(),
            "Chummer.Blazor",
            "Program.cs"));

        StringAssert.Contains(
            program,
            "builder.Services.AddHostedBuildWorkspacePersistenceReadiness();");
        int hostBuilt = program.IndexOf("WebApplication app = builder.Build();", StringComparison.Ordinal);
        int eagerProbe = program.IndexOf("workspacePersistenceReadiness.StartProbe();", StringComparison.Ordinal);
        int healthChecks = program.IndexOf("app.UseHostedBuildHealthChecks(", StringComparison.Ordinal);
        int authentication = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        int ownerBoundary = program.IndexOf(
            "app.UseMiddleware<HostedBuildOwnerGrantMiddleware>();",
            StringComparison.Ordinal);
        int pwaMiddleware = program.IndexOf("app.UseBuildPwaReleaseContract(pathBase);", StringComparison.Ordinal);
        int routesMapped = program.IndexOf("if (pathBase.HasValue)", StringComparison.Ordinal);
        Assert.IsTrue(hostBuilt >= 0 && eagerProbe > hostBuilt);
        Assert.IsTrue(healthChecks > eagerProbe);
        Assert.IsTrue(authentication > healthChecks);
        Assert.IsTrue(ownerBoundary > healthChecks);
        Assert.IsTrue(pwaMiddleware > healthChecks);
        Assert.IsTrue(routesMapped > pwaMiddleware);
        Assert.AreEqual(0, CountOccurrences(program, "MapGet(\"/health/live\""));
        Assert.AreEqual(0, CountOccurrences(program, "MapGet(\"/health/ready\""));
        Assert.AreEqual(0, CountOccurrences(program, "MapGet(\"/health\""));
        StringAssert.Contains(program, "BuildLivenessHealth(");
        StringAssert.Contains(program, "BuildReadinessHealthAsync(");
        StringAssert.Contains(program, "await persistenceReadiness.CheckAsync(cancellationToken)");
        StringAssert.Contains(program, "StatusCodes.Status503ServiceUnavailable");
        StringAssert.Contains(program, "workspacePersistence = persistence.Status");
    }

    private static DefaultHttpContext CreateHttpContext(
        IServiceProvider services,
        PathString path,
        string method)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadResponseBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(
            context.Response.Body,
            leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string ExtractService(string compose, string serviceName)
    {
        string[] lines = compose
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        int start = Array.FindIndex(
            lines,
            line => string.Equals(line, $"  {serviceName}:", StringComparison.Ordinal));
        Assert.IsTrue(start >= 0, $"Compose service '{serviceName}' was not found.");

        int end = start + 1;
        while (end < lines.Length
               && !(lines[end].Length > 2
                    && lines[end][0] == ' '
                    && lines[end][1] == ' '
                    && lines[end][2] != ' '))
        {
            end++;
        }

        return string.Join("\n", lines[start..end]);
    }

    private static int CountOccurrences(string value, string marker)
        => value.Split(marker, StringSplitOptions.None).Length - 1;

    private sealed class RecordingWorkspaceStoreReadinessProbe : IWorkspaceStoreReadinessProbe
    {
        public const string PrivateFailureMarker = "/private/chummer-state/owner-secret";

        public List<OwnerScope> Owners { get; } = [];

        public int FailuresRemaining { get; set; }

        public void Probe(OwnerScope owner)
        {
            Owners.Add(owner);
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new IOException(PrivateFailureMarker);
            }
        }
    }

    private sealed class BlockingWorkspaceStoreReadinessProbe :
        IWorkspaceStoreReadinessProbe,
        IDisposable
    {
        private int _callCount;

        public ManualResetEventSlim Entered { get; } = new(initialState: false);

        public ManualResetEventSlim Release { get; } = new(initialState: false);

        public TaskCompletionSource Completed { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount => Volatile.Read(ref _callCount);

        public void Probe(OwnerScope owner)
        {
            _ = owner;
            Interlocked.Increment(ref _callCount);
            Entered.Set();
            try
            {
                if (!Release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("The test did not release the blocking readiness probe.");
                }
            }
            finally
            {
                Completed.TrySetResult();
            }
        }

        public void Dispose()
        {
            Release.Set();
            Entered.Dispose();
            Release.Dispose();
        }
    }

    private sealed class BlockingFailureThenSuccessProbe :
        IWorkspaceStoreReadinessProbe,
        IDisposable
    {
        public const string PrivateFailureMarker = "/private/chummer-state/runtime-owner-secret";
        private int _callCount;

        public ManualResetEventSlim Entered { get; } = new(initialState: false);

        public ManualResetEventSlim Release { get; } = new(initialState: false);

        public TaskCompletionSource FirstCallCompleted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount => Volatile.Read(ref _callCount);

        public void Probe(OwnerScope owner)
        {
            _ = owner;
            int call = Interlocked.Increment(ref _callCount);
            if (call != 1)
            {
                return;
            }

            Entered.Set();
            try
            {
                if (!Release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("The test did not release the runtime readiness probe.");
                }

                throw new IOException(PrivateFailureMarker);
            }
            finally
            {
                FirstCallCompleted.TrySetResult();
            }
        }

        public void Dispose()
        {
            Release.Set();
            Entered.Dispose();
            Release.Dispose();
        }
    }
}
