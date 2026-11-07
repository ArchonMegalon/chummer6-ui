using System.Security.Cryptography;
using System.Text.Json;
using Chummer.Presentation.Content;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationCoreContentBundleTests
{
    private const string Pinned = "c28c87a1abb6e72f8a3db45a2a2f9ae578fd103c";
    private readonly List<string> _temporary = [];
    [TestCleanup] public void Cleanup() { foreach (string path in _temporary) if (Directory.Exists(path)) Directory.Delete(path, true); }

    [TestMethod]
    public void Verified_bundle_survives_moving_without_git_or_environment()
    {
        string app = NewDirectory(); string original = Bundle(); string digest = Digest(original);
        string bundle = Path.Combine(app, "core-content"); Directory.Move(original, bundle);
        Assert.IsTrue(CoreContentRootDiscovery.TryResolve("/nonexistent", out string resolved,
            _ => throw new AssertFailedException("Packaged runtime must not run Git"), app,
            _ => throw new AssertFailedException("Packaged runtime must not use developer overrides"), digest));
        Assert.AreEqual(bundle, resolved);
        Assert.IsNull(CoreContentBundle.Validate(bundle, [Pinned], out string? commit, digest));
        Assert.AreEqual(Pinned, commit);
    }

    [TestMethod]
    [DataRow("changed")]
    [DataRow("missing")]
    [DataRow("extra")]
    [DataRow("manifest")]
    [DataRow("missing-manifest")]
    public void Tampered_or_missing_content_is_rejected(string mutation)
    {
        string bundle = Bundle(); string digest = Digest(bundle);
        string data = Path.Combine(bundle, "Chummer", "data", "lifemodules.xml");
        switch (mutation)
        {
            case "changed": File.WriteAllText(data, "<changed/>"); break;
            case "missing": File.Delete(data); break;
            case "extra": File.WriteAllText(Path.Combine(bundle, "Chummer", "data", "amend.xml"), "<amend/>"); break;
            case "manifest": File.AppendAllText(Path.Combine(bundle, CoreContentBundle.ManifestFileName), " "); break;
            case "missing-manifest": File.Delete(Path.Combine(bundle, CoreContentBundle.ManifestFileName)); break;
        }
        Assert.IsNotNull(CoreContentBundle.Validate(bundle, [Pinned], out _, digest));
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("null")]
    [DataRow("{\"schemaVersion\":\"2\",\"contentCommit\":\"c28c87a1abb6e72f8a3db45a2a2f9ae578fd103c\"}")]
    [DataRow("{\"schemaVersion\":1,\"contentCommit\":\"short\"}")]
    [DataRow("{broken")]
    public void Malformed_manifests_fail_without_throwing_or_git_fallback(string text)
    {
        string bundle = Bundle(); File.WriteAllText(Path.Combine(bundle, CoreContentBundle.ManifestFileName), text);
        Assert.IsFalse(CoreContentBundle.TryReadManifestCommit(bundle, out _, out _));
        Assert.IsNotNull(CoreContentBundle.Validate(bundle, [Pinned], out _, Digest(bundle)));
        Assert.IsNotNull(CoreContentRootDiscovery.ValidateAny(bundle,
            _ => throw new AssertFailedException("Malformed bundle must never fall back to Git"), out _, Digest(bundle)));
    }

    [TestMethod]
    public void Self_asserted_commit_and_self_rewritten_inventory_are_not_trusted()
    {
        string bundle = Bundle(); string digest = Digest(bundle);
        Assert.IsNotNull(CoreContentBundle.Validate(bundle, [Pinned], out _));
        File.WriteAllText(Path.Combine(bundle, "Chummer", "data", "settings.xml"), "<tampered/>");
        WriteManifest(bundle, Pinned);
        Assert.IsNotNull(CoreContentBundle.Validate(bundle, [Pinned], out _, digest));
    }

    [TestMethod]
    public void Missing_or_invalid_delivered_bundle_cannot_use_a_developer_checkout()
    {
        string app = NewDirectory(); string checkout = Bundle();
        Assert.Throws<InvalidOperationException>(() => CoreContentRootDiscovery.TryResolve(checkout, out _, _ => Pinned, app, _ => checkout, new string('a', 64)));
        string bundle = Path.Combine(app, "core-content"); Directory.Move(Bundle(), bundle);
        Assert.Throws<InvalidOperationException>(() => CoreContentRootDiscovery.TryResolve(checkout, out _, _ => Pinned, app, _ => checkout, new string('a', 64)));
    }

    [TestMethod]
    public void Wrong_content_commit_and_unsafe_inventory_paths_are_rejected()
    {
        string bundle = Bundle(); WriteManifest(bundle, new string('a', 40));
        Assert.IsNotNull(CoreContentBundle.Validate(bundle, [Pinned], out _, Digest(bundle)));
        File.WriteAllText(Path.Combine(bundle, CoreContentBundle.ManifestFileName), JsonSerializer.Serialize(new {
            schemaVersion = 2, contentCommit = Pinned, files = new[] { new { path = "Chummer/../../outside", bytes = 0, sha256 = new string('a', 64) } }
        }));
        Assert.IsNotNull(CoreContentBundle.Validate(bundle, [Pinned], out _, Digest(bundle)));
    }

    [TestMethod]
    public void Explicit_development_checkout_remains_available_without_a_packaged_identity()
    {
        string checkout = Bundle(); File.Delete(Path.Combine(checkout, CoreContentBundle.ManifestFileName));
        Assert.IsTrue(CoreContentRootDiscovery.TryResolve(NewDirectory(), out string found, _ => Pinned, NewDirectory(), _ => checkout));
        Assert.AreEqual(checkout, found);
        Assert.Throws<InvalidOperationException>(() => CoreContentRootDiscovery.TryResolve(checkout, out _, _ => new string('b', 40), NewDirectory(), _ => checkout));
    }

    private string NewDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "chummer-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); _temporary.Add(root); return root;
    }
    private string Bundle()
    {
        string root = NewDirectory();
        foreach (string name in new[] { "data", "lang", "customdata" }) Directory.CreateDirectory(Path.Combine(root, "Chummer", name));
        foreach (string name in new[] { "settings.xml", "settings-all-sources.xml", "lifemodules.xml" }) File.WriteAllText(Path.Combine(root, "Chummer", "data", name), "<data/>");
        WriteManifest(root, Pinned); return root;
    }
    private static string Digest(string root) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, CoreContentBundle.ManifestFileName))));
    private static void WriteManifest(string root, string commit)
    {
        var files = Directory.GetFiles(Path.Combine(root, "Chummer"), "*", SearchOption.AllDirectories).OrderBy(x => x)
            .Select(x => new { path = Path.GetRelativePath(root, x).Replace(Path.DirectorySeparatorChar, '/'), bytes = new FileInfo(x).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x))) });
        File.WriteAllText(Path.Combine(root, CoreContentBundle.ManifestFileName), JsonSerializer.Serialize(new { schemaVersion = 2, contentCommit = commit, files }));
    }
}
