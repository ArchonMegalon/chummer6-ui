#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class DownloadPublishAurSidecarGuardTests
{
    [TestMethod]
    public void Publish_download_bundle_regenerates_and_mirrors_aur_sidecars()
    {
        string publisherPath = FindPath("scripts", "publish-download-bundle.sh");
        string text = File.ReadAllText(publisherPath);

        StringAssert.Contains(text, "resolve_aur_materializer()");
        StringAssert.Contains(text, "materialize_aur_sidecar()");
        StringAssert.Contains(text, "--manifest \"$DEPLOY_DIR/RELEASE_CHANNEL.generated.json\"");
        StringAssert.Contains(text, "--files-root \"$DEPLOY_DIR/files\"");
        StringAssert.Contains(text, "--output-root \"$DEPLOY_DIR\"");
        StringAssert.Contains(text, "materialize_aur_sidecar");
        StringAssert.Contains(text, "cp \"$DEPLOY_DIR/aur-packages.json\" \"$target_dir/aur-packages.json\"");
        StringAssert.Contains(text, "for file_name in chummer6-bin-aur-source.tar.gz chummer6-bin.PKGBUILD chummer6-bin.SRCINFO");
        StringAssert.Contains(text, "remove_aur_sidecar");
        StringAssert.Contains(text, "AUR materializer not found; removed stale AUR sidecar files");
    }

    private static string FindPath(params string[] parts)
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            string candidate = Path.Combine(new[] { current }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException($"Could not locate {Path.Combine(parts)} from the test output directory.");
    }
}
