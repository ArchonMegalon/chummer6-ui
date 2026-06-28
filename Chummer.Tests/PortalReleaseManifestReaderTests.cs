#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class PortalReleaseManifestReaderTests
{
    [TestMethod]
    public void Read_uses_explicit_download_rows_from_primary_manifest()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            string releasesPath = Path.Combine(tempRoot, "releases.json");
            File.WriteAllText(
                releasesPath,
                """
                {
                  "status": "published",
                  "version": "run-primary",
                  "downloads": [
                    {
                      "label": "Stable Linux",
                      "platform": "linux",
                      "url": "https://chummer.run/downloads/files/stable-linux.deb",
                      "artifactId": "stable-linux",
                      "publicInstallRoute": "/downloads/install/stable-linux"
                    }
                  ]
                }
                """);

            ReleaseManifestSummary summary = PortalReleaseManifestReader.Read(releasesPath);

            Assert.AreEqual("published", summary.Status);
            Assert.AreEqual("run-primary", summary.Version);
            Assert.AreEqual(1, summary.Downloads.Count);
            Assert.AreEqual("Stable Linux", summary.Downloads[0].Label);
            Assert.AreEqual("/downloads/install/stable-linux", summary.Downloads[0].PublicInstallRoute);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Read_maps_install_route_from_binding_when_download_row_omits_public_install_route()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            string releasesPath = Path.Combine(tempRoot, "releases.json");
            File.WriteAllText(
                releasesPath,
                """
                {
                  "status": "published",
                  "version": "run-binding-map",
                  "downloads": [
                    {
                      "platform": "Avalonia Desktop Linux X64 Installer",
                      "url": "https://chummer.run/downloads/files/chummer-avalonia-linux-x64-installer.deb",
                      "artifactId": "avalonia-linux-x64-installer"
                    }
                  ],
                  "artifactPublicationBindings": [
                    {
                      "artifactId": "avalonia-linux-x64-installer",
                      "publicInstallRoute": "/downloads/install/avalonia-linux-x64-installer",
                      "promotionState": "promoted",
                      "installPosture": "installer_first"
                    }
                  ]
                }
                """);

            ReleaseManifestSummary summary = PortalReleaseManifestReader.Read(releasesPath);

            Assert.AreEqual(1, summary.Downloads.Count);
            Assert.AreEqual("avalonia-linux-x64-installer", summary.Downloads[0].ArtifactId);
            Assert.AreEqual("/downloads/install/avalonia-linux-x64-installer", summary.Downloads[0].PublicInstallRoute);
            Assert.AreEqual(1, summary.InstallRoutes.Count);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Read_falls_back_to_release_channel_artifacts_when_primary_manifest_has_no_download_rows()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            string releasesPath = Path.Combine(tempRoot, "releases.json");
            File.WriteAllText(
                releasesPath,
                """
                {
                  "status": "published",
                  "version": "run-fallback",
                  "artifactPublicationBindings": [
                    {
                      "artifactId": "avalonia-linux-x64-installer",
                      "publicInstallRoute": "/downloads/install/avalonia-linux-x64-installer",
                      "promotionState": "promoted",
                      "installPosture": "installer_first"
                    }
                  ]
                }
                """);

            File.WriteAllText(
                Path.Combine(tempRoot, "RELEASE_CHANNEL.generated.json"),
                """
                {
                  "status": "published",
                  "version": "run-fallback",
                  "artifacts": [
                    {
                      "artifactId": "avalonia-linux-x64-installer",
                      "platformLabel": "Avalonia Desktop Linux X64 Installer",
                      "platform": "linux",
                      "downloadUrl": "https://chummer.run/downloads/files/chummer-avalonia-linux-x64-installer.deb"
                    }
                  ]
                }
                """);

            ReleaseManifestSummary summary = PortalReleaseManifestReader.Read(releasesPath);

            Assert.AreEqual("published", summary.Status);
            Assert.AreEqual("run-fallback", summary.Version);
            Assert.AreEqual(1, summary.Downloads.Count);
            Assert.AreEqual("Avalonia Desktop Linux X64 Installer", summary.Downloads[0].Label);
            Assert.AreEqual("https://chummer.run/downloads/files/chummer-avalonia-linux-x64-installer.deb", summary.Downloads[0].Url);
            Assert.AreEqual("avalonia-linux-x64-installer", summary.Downloads[0].ArtifactId);
            Assert.AreEqual("/downloads/install/avalonia-linux-x64-installer", summary.Downloads[0].PublicInstallRoute);
            Assert.AreEqual(1, summary.InstallRoutes.Count);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"chummer-portal-release-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }
}
