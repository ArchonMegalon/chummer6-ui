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
        const string sha = "sha256:0123abcd";
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
        Assert.AreEqual("0123abcd", artifact.Sha256);
        Assert.AreEqual(size, artifact.SizeBytes);
    }
}
