#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class ExternalHostProofBlockersTests
{
    private static readonly string[] RequiredPlatformHeadRidTuples = { "avalonia:osx-arm64:macos" };
    private static readonly string[] RequiredProofs = { "promoted_installer_artifact", "startup_smoke_receipt" };
    private static readonly string?[] RepoRootCandidates =
    {
        Environment.GetEnvironmentVariable("CHUMMER_REPO_ROOT"),
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory,
        "/docker/chummercomplete/chummer-presentation",
        "/docker/chummercomplete/chummer6-ui",
    };

    [TestMethod]
    public async Task Materializer_accepts_auth_challenge_for_account_required_macos_external_proof_route() 
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-external-host-proof-blockers.py");
        string tempRoot = Path.Combine(Path.GetTempPath(), $"external-host-proof-blockers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            string downloadsDir = Path.Combine(tempRoot, "files");
            string startupSmokeDir = Path.Combine(tempRoot, "startup-smoke");
            Directory.CreateDirectory(downloadsDir);
            Directory.CreateDirectory(startupSmokeDir);

            string installerFileName = "chummer-avalonia-osx-arm64-installer.dmg";
            string installerPath = Path.Combine(downloadsDir, installerFileName);
            byte[] installerBytes = Encoding.UTF8.GetBytes("macos-proof-installer");
            await File.WriteAllBytesAsync(installerPath, installerBytes).ConfigureAwait(false);
            string installerSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(installerBytes)).ToLowerInvariant();

            string receiptPath = Path.Combine(startupSmokeDir, "startup-smoke-avalonia-osx-arm64.receipt.json");
            await File.WriteAllTextAsync(
                receiptPath,
                JsonSerializer.Serialize(
                    new
                    {
                        status = "pass",
                        channelId = "docker",
                        version = "unpublished",
                        recordedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        artifactSha256 = installerSha,
                    }),
                Encoding.UTF8).ConfigureAwait(false);

            string manifestPath = Path.Combine(tempRoot, "RELEASE_CHANNEL.generated.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(
                    new
                    {
                        channelId = "docker",
                        version = "unpublished",
                        artifacts = Array.Empty<object>(),
                        desktopTupleCoverage = new
                        {
                            missingRequiredPlatformHeadRidTuples = RequiredPlatformHeadRidTuples,
                            externalProofRequests = new object[]
                            {
                                new
                                {
                                    tupleId = "avalonia:osx-arm64:macos",
                                    requiredHost = "macos",
                                    expectedPublicInstallRoute = "/downloads/install/avalonia-osx-arm64-installer",
                                    requiredProofs = RequiredProofs,
                                    expectedArtifactId = "avalonia-osx-arm64-installer",
                                    expectedInstallerFileName = installerFileName,
                                    expectedInstallerRelativePath = $"files/{installerFileName}",
                                    expectedInstallerSha256 = installerSha,
                                    expectedStartupSmokeReceiptPath = "startup-smoke/startup-smoke-avalonia-osx-arm64.receipt.json",
                                },
                            },
                        },
                    }),
                Encoding.UTF8).ConfigureAwait(false);

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task serverTask = Task.Run(async () =>
            {
                using TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[4096];
                _ = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response.AsMemory()).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            });

            string outputPath = Path.Combine(tempRoot, "UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json");
            (int exitCode, string output) = RunProcess(
                fileName: "python3",
                arguments:
                    $"\"{scriptPath}\" --manifest \"{manifestPath}\" --downloads-dir \"{downloadsDir}\" --startup-smoke-dir \"{startupSmokeDir}\" --output \"{outputPath}\" --base-url \"http://127.0.0.1:{port}\"",
                workingDirectory: repoRoot);

            await serverTask.ConfigureAwait(false);

            Assert.AreEqual(0, exitCode, output);
            using JsonDocument payload = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath).ConfigureAwait(false));
            Assert.AreEqual("ready", payload.RootElement.GetProperty("status").GetString());

            JsonElement request = payload.RootElement.GetProperty("external_proof_requests")[0];
            Assert.AreEqual("account_required", request.GetProperty("installAccessClass").GetString());
            Assert.AreEqual(0, request.GetProperty("blockerCodes").GetArrayLength());

            JsonElement routeProbe = request.GetProperty("publicRouteProbe");
            Assert.AreEqual(403, routeProbe.GetProperty("http_status").GetInt32());
            Assert.IsTrue(routeProbe.GetProperty("authExpected").GetBoolean());
            Assert.IsTrue(routeProbe.GetProperty("authChallengeAccepted").GetBoolean());
            Assert.IsTrue(routeProbe.GetProperty("ok").GetBoolean());
            Assert.AreEqual(string.Empty, routeProbe.GetProperty("error").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task Materializer_blocks_receipt_captured_before_current_release_publication()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-external-host-proof-blockers.py");
        string tempRoot = Path.Combine(Path.GetTempPath(), $"external-host-proof-blockers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            string downloadsDir = Path.Combine(tempRoot, "files");
            string startupSmokeDir = Path.Combine(tempRoot, "startup-smoke");
            Directory.CreateDirectory(downloadsDir);
            Directory.CreateDirectory(startupSmokeDir);

            string installerFileName = "chummer-avalonia-osx-arm64-installer.dmg";
            string installerPath = Path.Combine(downloadsDir, installerFileName);
            byte[] installerBytes = Encoding.UTF8.GetBytes("macos-proof-installer");
            await File.WriteAllBytesAsync(installerPath, installerBytes).ConfigureAwait(false);
            string installerSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(installerBytes)).ToLowerInvariant();

            DateTimeOffset releasePublishedAt = DateTimeOffset.UtcNow;
            DateTimeOffset receiptRecordedAt = releasePublishedAt.AddHours(-1);
            string receiptPath = Path.Combine(startupSmokeDir, "startup-smoke-avalonia-osx-arm64.receipt.json");
            await File.WriteAllTextAsync(
                receiptPath,
                JsonSerializer.Serialize(
                    new
                    {
                        status = "pass",
                        channelId = "docker",
                        version = "unpublished",
                        recordedAtUtc = receiptRecordedAt.ToString("O"),
                        artifactSha256 = installerSha,
                    }),
                Encoding.UTF8).ConfigureAwait(false);

            string manifestPath = Path.Combine(tempRoot, "RELEASE_CHANNEL.generated.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(
                    new
                    {
                        channelId = "docker",
                        version = "unpublished",
                        publishedAt = releasePublishedAt.ToString("O"),
                        artifacts = Array.Empty<object>(),
                        desktopTupleCoverage = new
                        {
                            missingRequiredPlatformHeadRidTuples = RequiredPlatformHeadRidTuples,
                            externalProofRequests = new object[]
                            {
                                new
                                {
                                    tupleId = "avalonia:osx-arm64:macos",
                                    requiredHost = "macos",
                                    expectedPublicInstallRoute = "/downloads/install/avalonia-osx-arm64-installer",
                                    requiredProofs = RequiredProofs,
                                    expectedArtifactId = "avalonia-osx-arm64-installer",
                                    expectedInstallerFileName = installerFileName,
                                    expectedInstallerRelativePath = $"files/{installerFileName}",
                                    expectedInstallerSha256 = installerSha,
                                    expectedStartupSmokeReceiptPath = "startup-smoke/startup-smoke-avalonia-osx-arm64.receipt.json",
                                },
                            },
                        },
                    }),
                Encoding.UTF8).ConfigureAwait(false);

            string outputPath = Path.Combine(tempRoot, "UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json");
            (int exitCode, string output) = RunProcess(
                fileName: "python3",
                arguments:
                    $"\"{scriptPath}\" --manifest \"{manifestPath}\" --downloads-dir \"{downloadsDir}\" --startup-smoke-dir \"{startupSmokeDir}\" --output \"{outputPath}\" --skip-public-route-check",
                workingDirectory: repoRoot);

            Assert.AreEqual(0, exitCode, output);
            using JsonDocument payload = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath).ConfigureAwait(false));
            Assert.AreEqual("blocked", payload.RootElement.GetProperty("status").GetString());
            Assert.AreEqual(ToUtcZulu(releasePublishedAt), payload.RootElement.GetProperty("release_published_at").GetString());

            JsonElement request = payload.RootElement.GetProperty("external_proof_requests")[0];
            Assert.AreEqual(receiptRecordedAt.ToString("O"), request.GetProperty("startupSmokeReceiptRecordedAtUtc").GetString());
            StringAssert.Contains(request.GetProperty("blockerCodes").ToString(), "receipt_precedes_release_publication");
            StringAssert.Contains(request.GetProperty("blockerMessages").ToString(), "current release channel was published");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string FindRepoRoot()
    {
        foreach (string? candidate in RepoRootCandidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            string current = Path.GetFullPath(candidate);
            for (int depth = 0; depth < 8; depth++)
            {
                if (File.Exists(Path.Combine(current, "scripts", "materialize-external-host-proof-blockers.py")))
                {
                    return current;
                }

                DirectoryInfo? parent = Directory.GetParent(current);
                if (parent is null)
                {
                    break;
                }

                current = parent.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find chummer-presentation repo root.");
    }

    private static (int ExitCode, string Output) RunProcess(string fileName, string arguments, string workingDirectory)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        process.Start();
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        Assert.IsTrue(process.WaitForExit(30000), $"{fileName} {arguments} did not exit within 30 seconds.");
        return (process.ExitCode, standardOutput + standardError);
    }

    private static string ToUtcZulu(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}
