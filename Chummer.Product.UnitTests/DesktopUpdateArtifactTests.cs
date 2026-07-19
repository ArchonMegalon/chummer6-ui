using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Product.UnitTests;

[TestClass]
public sealed class DesktopUpdateArtifactTests
{
    [DataTestMethod]
    [DataRow("chummer.zip", ".zip")]
    [DataRow("chummer.TAR.GZ", ".tar.gz")]
    [DataRow("chummer-installer.exe", ".exe")]
    public void ExtensionRecognizesReleaseArtifactSuffixes(string fileName, string expected)
    {
        Assert.AreEqual(expected, CreateArtifact(fileName: fileName).Extension);
    }

    [DataTestMethod]
    [DataRow("archive", "chummer.zip", true, false)]
    [DataRow("archive", "chummer.tar.gz", true, false)]
    [DataRow("installer", "chummer.exe", false, true)]
    [DataRow("", "chummer.deb", false, true)]
    [DataRow("metadata", "release.json", false, false)]
    public void SupportedDeliveryModeIsExplicit(
        string kind,
        string fileName,
        bool expectedInPlace,
        bool expectedHandoff)
    {
        DesktopUpdateArtifact artifact = CreateArtifact(kind, fileName);

        Assert.AreEqual(expectedInPlace, artifact.SupportsInPlaceApply);
        Assert.AreEqual(expectedHandoff, artifact.SupportsInstallerHandoff);
    }

    [TestMethod]
    public void PreferredArtifactStaysWithinTheRequestedHeadAndPlatform()
    {
        DesktopUpdateArtifact installer = CreateArtifact(
            kind: "installer",
            fileName: "chummer-avalonia-linux-x64-installer.deb",
            artifactId: "installer");
        DesktopUpdateArtifact archive = CreateArtifact(
            kind: "archive",
            fileName: "chummer-avalonia-linux-x64.zip",
            artifactId: "archive");
        DesktopUpdateArtifact wrongHead = archive with
        {
            ArtifactId = "blazor",
            HeadId = "blazor-desktop"
        };
        var manifest = new DesktopUpdateChannelManifest(
            ChannelId: "preview",
            Version: "run-test",
            Status: "preview_ready",
            PublishedAt: null,
            Artifacts: [installer, wrongHead, archive],
            DesktopSurfaceRefs: [],
            RolloutState: "preview",
            RolloutReason: null,
            SupportabilityState: "bounded",
            SupportabilitySummary: null,
            KnownIssueSummary: null,
            FixAvailabilitySummary: null,
            ProofStatus: "pass",
            ProofGeneratedAt: null,
            SourceUri: new Uri("https://chummer.run/releases/run-test/manifest.json"));

        DesktopUpdateArtifact? selected = DesktopUpdateManifestParser.SelectPreferredArtifact(
            manifest,
            "avalonia",
            new DesktopUpdatePlatformIdentity("linux", "x64"));

        Assert.IsNotNull(selected);
        Assert.AreEqual("archive", selected.ArtifactId);
    }

    private static DesktopUpdateArtifact CreateArtifact(
        string kind = "archive",
        string fileName = "chummer.zip",
        string artifactId = "artifact")
        => new(
            ArtifactId: artifactId,
            HeadId: "avalonia",
            Platform: "linux",
            Arch: "x64",
            Kind: kind,
            FileName: fileName,
            DownloadUrl: $"https://chummer.run/downloads/{fileName}",
            UpdateFeedUrl: null,
            Sha256: new string('a', 64),
            SizeBytes: 1024);
}
