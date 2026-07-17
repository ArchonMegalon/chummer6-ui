using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Chummer.Blazor.Services;

public sealed record BuildPwaReleaseAsset(
    string PublicPath,
    string Sha256Hex,
    string SubresourceIntegrity);

public sealed record BuildPwaReleaseSnapshot(
    string ContentRevision,
    IReadOnlyDictionary<string, BuildPwaReleaseAsset> Assets);

public static class BuildPwaReleaseContract
{
    public const string QueryKey = "build";
    public const string ResponseRevisionHeader = "X-Chummer-Build-Content-Revision";

    // Order is part of the canonical framing contract shared with the worker
    // and the publish-receipt finalizer. These are the exact public bytes that
    // can bootstrap or visually define a Build PWA document.
    public static readonly IReadOnlyList<string> AssetPaths = Array.AsReadOnly(new[]
    {
        "service-worker.js",
        "offline.html",
        "app.css",
        "build-pwa-install.css",
        "Chummer.Blazor.styles.css",
        "manifest.webmanifest",
        "js/build-pwa-recovery.js",
        "js/build-pwa-integrity.js",
        "js/build-pwa-install.js",
        "js/build-pwa-layout.js",
        "js/privacy-boundaries.js",
        "_framework/blazor.web.js",
        "icons/chummer-build-180.png",
        "icons/chummer-build-192.png",
        "icons/chummer-build-512.png",
        "icons/chummer-build-maskable-512.png",
        "icons/chummer-pwa.svg",
        "icons/chummer-pwa-maskable.svg"
    });

    private static readonly ConditionalWeakTable<IFileProvider, Lazy<BuildPwaReleaseSnapshot>> Snapshots = new();

    public static BuildPwaReleaseSnapshot GetSnapshot(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        IFileProvider provider = environment.WebRootFileProvider
            ?? throw new InvalidOperationException("Build PWA web-root file provider is unavailable.");
        return Snapshots.GetValue(
            provider,
            static fileProvider => new Lazy<BuildPwaReleaseSnapshot>(
                () => BuildSnapshot(fileProvider),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public static bool IsValidContentRevision(string? revision)
    {
        if (revision is null || revision.Length != 64)
            return false;

        foreach (char value in revision)
        {
            if (value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                return false;
        }

        return true;
    }

    private static BuildPwaReleaseSnapshot BuildSnapshot(IFileProvider fileProvider)
    {
        using IncrementalHash aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Dictionary<string, BuildPwaReleaseAsset> assets = new(StringComparer.Ordinal);
        Span<byte> pathLength = stackalloc byte[sizeof(uint)];
        Span<byte> contentLength = stackalloc byte[sizeof(ulong)];

        foreach (string publicPath in AssetPaths)
        {
            IFileInfo file = fileProvider.GetFileInfo(publicPath);
            if (!file.Exists || file.IsDirectory)
                throw new InvalidOperationException(
                    $"Build PWA release asset '{publicPath}' is not available from the composed web root.");

            byte[] content;
            using (Stream input = file.CreateReadStream())
            using (MemoryStream buffer = new())
            {
                input.CopyTo(buffer);
                content = buffer.ToArray();
            }

            byte[] encodedPath = Encoding.UTF8.GetBytes(publicPath);
            BinaryPrimitives.WriteUInt32BigEndian(pathLength, checked((uint)encodedPath.Length));
            BinaryPrimitives.WriteUInt64BigEndian(contentLength, checked((ulong)content.LongLength));
            aggregate.AppendData(pathLength);
            aggregate.AppendData(encodedPath);
            aggregate.AppendData(contentLength);
            aggregate.AppendData(content);

            byte[] assetDigest = SHA256.HashData(content);
            assets.Add(publicPath, new BuildPwaReleaseAsset(
                publicPath,
                Convert.ToHexString(assetDigest).ToLowerInvariant(),
                $"sha256-{Convert.ToBase64String(assetDigest)}"));
        }

        string contentRevision = Convert.ToHexString(aggregate.GetHashAndReset()).ToLowerInvariant();
        if (!IsValidContentRevision(contentRevision))
            throw new InvalidOperationException("Build PWA release revision generation failed closed.");

        return new BuildPwaReleaseSnapshot(
            contentRevision,
            new ReadOnlyDictionary<string, BuildPwaReleaseAsset>(assets));
    }
}
