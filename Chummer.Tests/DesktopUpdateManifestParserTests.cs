#nullable enable

using System;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopUpdateManifestParserTests
{
    [TestMethod]
    public void Parse_canonical_manifest_preserves_release_posture_fields()
    {
        const string json = """
            {
              "channelId": "preview",
              "version": "6.0.2-preview",
              "status": "published",
              "publishedAt": "2026-03-28T16:31:31Z",
              "rolloutState": "local_docker_preview",
              "rolloutReason": "Promoted from local docker proof.",
              "supportabilityState": "local_docker_proven",
              "supportabilitySummary": "Install, build, campaign recovery, and support closure all passed locally.",
              "knownIssueSummary": "Mac remains gated until notarization proof is available.",
              "fixAvailabilitySummary": "Only notify installs once the promoted artifact is visible on the shelf.",
              "releaseProof": {
                "status": "passed",
                "generatedAt": "2026-03-28T16:31:31Z"
              },
              "artifacts": [
                {
                  "artifactId": "avalonia-linux-x64-archive",
                  "head": "avalonia",
                  "platform": "linux",
                  "arch": "x64",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-linux-x64.tar.gz",
                  "downloadUrl": "/downloads/files/chummer-avalonia-linux-x64.tar.gz"
                }
              ]
            }
            """;

        DesktopUpdateChannelManifest manifest = DesktopUpdateManifestParser.Parse(
            json,
            new Uri("http://127.0.0.1:8091/downloads/RELEASE_CHANNEL.generated.json"));

        Assert.AreEqual("local_docker_preview", manifest.RolloutState);
        Assert.AreEqual("local_docker_proven", manifest.SupportabilityState);
        Assert.AreEqual("passed", manifest.ProofStatus);
        StringAssert.Contains(manifest.FixAvailabilitySummary ?? string.Empty, "promoted artifact");
    }

    [TestMethod]
    public void Parse_manifest_parses_size_bytes_and_sha256_with_prefix()
    {
        const string sha = "sha256:00d34c7514b9e44bd315c3d9914547d0c750865ddf5bffaf3e17f861648fe4b7";
        const long size = 12345;
        string json = $$"""
            {
              "channel": "preview",
              "version": "6.0.2-preview.2",
              "status": "published",
              "artifacts": [
                {
                  "artifactId": "avalonia-linux-x64-archive",
                  "head": "avalonia",
                  "platform": "linux",
                  "arch": "x64",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-linux-x64.tar.gz",
                  "downloadUrl": "/downloads/files/chummer-avalonia-linux-x64.tar.gz",
                  "sha256": "{{sha}}",
                  "sizeBytes": {{size}}
                }
              ]
            }
            """;

        DesktopUpdateChannelManifest manifest = DesktopUpdateManifestParser.Parse(
            json,
            new Uri("http://127.0.0.1:8091/downloads/manifest.json"));

        DesktopUpdateArtifact artifact = manifest.Artifacts[0];
        Assert.AreEqual("00d34c7514b9e44bd315c3d9914547d0c750865ddf5bffaf3e17f861648fe4b7", artifact.Sha256);
        Assert.AreEqual(size, artifact.SizeBytes);
    }

    [TestMethod]
    public void Parse_manifest_rejects_invalid_sha256_metadata()
    {
        const string json = """
            {
              "channel": "preview",
              "version": "6.0.2-preview.2",
              "status": "published",
              "artifacts": [
                {
                  "artifactId": "avalonia-linux-x64-archive",
                  "head": "avalonia",
                  "platform": "linux",
                  "arch": "x64",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-linux-x64.tar.gz",
                  "downloadUrl": "/downloads/files/chummer-avalonia-linux-x64.tar.gz",
                  "sha256": "sha256:0123abcd",
                  "sizeBytes": 12345
                }
              ]
            }
            """;

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            DesktopUpdateManifestParser.Parse(
                json,
                new Uri("https://chummer.run/downloads/manifest.json")));

        StringAssert.Contains(ex.Message, "invalid sha256");
        StringAssert.Contains(ex.Message, "64-character SHA-256");
    }

    [TestMethod]
    public void Parse_compatibility_manifest_rejects_bootstrap_payload_without_valid_download_metadata()
    {
        const string json = """
            {
              "channel": "public_stable",
              "version": "run-test",
              "downloads": [
                {
                  "id": "avalonia-win-x64-installer",
                  "head": "avalonia",
                  "platformId": "windows-x64",
                  "arch": "x64",
                  "kind": "installer",
                  "fileName": "chummer-avalonia-win-x64-installer.exe",
                  "url": "https://chummer.run/downloads/files/chummer-avalonia-win-x64-installer.exe",
                  "sha256": "2f4ad755491b86e3a4ae0fb3251b0c863552ec4f0ae29049cedb7973bc372a4f",
                  "sizeBytes": 51856809,
                  "installerMode": "bootstrap",
                  "payloadFileName": "chummer-avalonia-win-x64-payload.zip",
                  "payloadDownloadUrl": "http://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip",
                  "payloadSha256": "00d34c7514b9e44bd315c3d9914547d0c750865ddf5bffaf3e17f861648fe4b7",
                  "payloadSizeBytes": 47152146
                }
              ]
            }
            """;

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            DesktopUpdateManifestParser.Parse(
                json,
                new Uri("https://chummer.run/downloads/releases.json")));

        StringAssert.Contains(ex.Message, "payloadDownloadUrl must be an absolute HTTPS URL");
    }

    [TestMethod]
    public void Parse_canonical_manifest_rejects_bootstrap_payload_url_file_name_mismatch()
    {
        const string json = """
            {
              "channelId": "public_stable",
              "version": "run-test",
              "artifacts": [
                {
                  "artifactId": "avalonia-win-x64-installer",
                  "head": "avalonia",
                  "platform": "windows",
                  "arch": "x64",
                  "kind": "installer",
                  "fileName": "chummer-avalonia-win-x64-installer.exe",
                  "downloadUrl": "https://chummer.run/downloads/files/chummer-avalonia-win-x64-installer.exe",
                  "sha256": "2f4ad755491b86e3a4ae0fb3251b0c863552ec4f0ae29049cedb7973bc372a4f",
                  "sizeBytes": 51856809,
                  "installerMode": "bootstrap",
                  "payloadFileName": "chummer-avalonia-win-x64-payload.zip",
                  "payloadDownloadUrl": "https://chummer.run/downloads/files/not-the-payload.zip",
                  "payloadSha256": "00d34c7514b9e44bd315c3d9914547d0c750865ddf5bffaf3e17f861648fe4b7",
                  "payloadSizeBytes": 47152146
                }
              ]
            }
            """;

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            DesktopUpdateManifestParser.Parse(
                json,
                new Uri("https://chummer.run/downloads/RELEASE_CHANNEL.generated.json")));

        StringAssert.Contains(ex.Message, "payloadDownloadUrl file name must match payloadFileName");
    }
}
