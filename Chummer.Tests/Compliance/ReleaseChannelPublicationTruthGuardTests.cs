#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class ReleaseChannelPublicationTruthGuardTests
{
    [TestMethod]
    public void Published_release_channel_truth_matches_across_registry_and_download_shelves()
    {
        string repoRoot = FindRepoRoot();
        string workspaceRoot = Directory.GetParent(repoRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not resolve workspace root for {repoRoot}.");
        string hubRegistryRoot = Path.Combine(workspaceRoot, "chummer-hub-registry");

        ReleaseChannelSnapshot canonical = Load(
            Path.Combine(hubRegistryRoot, ".codex-studio", "published", "RELEASE_CHANNEL.generated.json"),
            "canonical hub-registry release channel");
        ReleaseChannelSnapshot dockerDownloads = Load(
            Path.Combine(repoRoot, "Docker", "Downloads", "RELEASE_CHANNEL.generated.json"),
            "Docker downloads release channel");
        ReleaseChannelSnapshot portalDownloads = Load(
            Path.Combine(repoRoot, "Chummer.Portal", "downloads", "RELEASE_CHANNEL.generated.json"),
            "portal downloads release channel");
        ReleaseChannelSnapshot portalReleases = Load(
            Path.Combine(repoRoot, "Chummer.Portal", "downloads", "releases.json"),
            "portal releases manifest");
        ReleaseChannelSnapshot canonicalReleases = Load(
            Path.Combine(hubRegistryRoot, ".codex-studio", "published", "releases.json"),
            "canonical hub-registry releases manifest");

        AssertShelfMatches(canonical, dockerDownloads);
        AssertShelfMatches(canonical, portalDownloads);
        AssertShelfMatches(canonical, portalReleases);
        AssertShelfMatches(canonical, canonicalReleases);

        CollectionAssert.AreEquivalent(
            new[] { "avalonia-linux-x64-installer", "avalonia-win-x64-installer" },
            canonical.ArtifactIds.ToArray(),
            "Every published release must expose the current Windows and Linux installer artifacts on every shelf.");
        Assert.AreEqual(canonical.ChannelId, canonical.Channel);
        Assert.AreEqual("published", canonical.Status);
        if (!canonical.ArtifactIds.Any(id => id.Contains("osx", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.AreNotEqual(
                "public_stable",
                canonical.ChannelId,
                "A shelf without promoted macOS media must not claim a stable public channel.");
            Assert.AreEqual(
                "coverage_incomplete",
                canonical.RolloutState,
                "An honestly published partial desktop shelf must remain coverage_incomplete.");
        }
    }

    private static void AssertShelfMatches(ReleaseChannelSnapshot expected, ReleaseChannelSnapshot actual)
    {
        Assert.AreEqual(expected.Version, actual.Version, $"{actual.Label} version must match {expected.Label}.");
        Assert.AreEqual(expected.ChannelId, actual.ChannelId, $"{actual.Label} channelId must match {expected.Label}.");
        Assert.AreEqual(expected.Channel, actual.Channel, $"{actual.Label} channel must match {expected.Label}.");
        Assert.AreEqual(expected.Status, actual.Status, $"{actual.Label} status must match {expected.Label}.");
        Assert.AreEqual(expected.RolloutState, actual.RolloutState, $"{actual.Label} rolloutState must match {expected.Label}.");
        CollectionAssert.AreEquivalent(
            expected.ArtifactIds.ToArray(),
            actual.ArtifactIds.ToArray(),
            $"{actual.Label} artifact IDs must match {expected.Label}.");
        CollectionAssert.AreEquivalent(
            expected.ArtifactDigests.ToArray(),
            actual.ArtifactDigests.ToArray(),
            $"{actual.Label} artifact digests must match {expected.Label}.");
    }

    private static ReleaseChannelSnapshot Load(string path, string label)
    {
        Assert.IsTrue(File.Exists(path), $"{label} is missing: {path}");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        SortedSet<string> ids = new(StringComparer.Ordinal);
        SortedSet<string> digests = new(StringComparer.Ordinal);

        CollectArtifacts(root, "artifacts", ids, digests);
        CollectArtifacts(root, "downloads", ids, digests);

        return new ReleaseChannelSnapshot(
            label,
            GetRequiredString(root, "version", label),
            GetRequiredAliasString(root, label, "channelId", "channel"),
            GetRequiredString(root, "channel", label),
            GetRequiredString(root, "status", label),
            GetRequiredString(root, "rolloutState", label),
            ids,
            digests);
    }

    private static void CollectArtifacts(
        JsonElement root,
        string propertyName,
        ISet<string> ids,
        ISet<string> digests)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement artifacts)
            || artifacts.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement artifact in artifacts.EnumerateArray())
        {
            string id = GetOptionalString(artifact, "artifactId")
                ?? GetOptionalString(artifact, "id")
                ?? GetOptionalString(artifact, "downloadId")
                ?? string.Empty;
            string digest = GetOptionalString(artifact, "sha256")
                ?? GetOptionalString(artifact, "sha256Hex")
                ?? GetOptionalString(artifact, "digest")
                ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }

            if (!string.IsNullOrWhiteSpace(digest))
            {
                digests.Add(NormalizeDigest(digest));
            }
        }
    }

    private static string GetRequiredString(JsonElement root, string propertyName, string label)
    {
        string value = GetOptionalString(root, propertyName) ?? string.Empty;
        Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"{label} is missing {propertyName}.");
        return value;
    }

    private static string GetRequiredAliasString(JsonElement root, string label, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            string? value = GetOptionalString(root, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        Assert.Fail($"{label} is missing all channel identity aliases: {string.Join(", ", propertyNames)}.");
        throw new InvalidOperationException("Unreachable after Assert.Fail.");
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string NormalizeDigest(string digest)
    {
        return digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? digest["sha256:".Length..]
            : digest;
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.Tests", "Chummer.Tests.csproj")))
            {
                return current;
            }

            string? parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate chummer-presentation repository root.");
    }

    private sealed record ReleaseChannelSnapshot(
        string Label,
        string Version,
        string ChannelId,
        string Channel,
        string Status,
        string RolloutState,
        SortedSet<string> ArtifactIds,
        SortedSet<string> ArtifactDigests);
}
