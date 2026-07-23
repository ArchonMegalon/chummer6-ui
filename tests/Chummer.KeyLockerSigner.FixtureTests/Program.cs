using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Chummer.KeyLockerSigner;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("linux")]

namespace Chummer.KeyLockerSigner.FixtureTests;

internal static class Program
{
    private const string SignerCertificateDerSha256 =
        "c260b938e9a523e4e83d875754d2f1ec8badce04fa45ed1058e792ff5d28080f";
    private const string SignerSpkiSha256 =
        "ece7f11e5ab439b81efac96d6db68eeaf01e7813dfdeed6395d1f7b68442c423";
    private const string SignerPackageLockSha256 =
        "f1cf04d5f641bc62903122f700f9714cf693bf3f3b80eeb79810e741e07eb73d";
    private const string Rfc3161AttributeOid =
        "1.3.6.1.4.1.311.3.3.1";

    private static readonly IReadOnlyDictionary<string, string> FixtureHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MANIFEST.json"] =
                "fcb63c2fcce8f778855afc742f295fd37dbd208753564bb8268079b74d006423",
            ["fixture-rfc3161-signature.der"] =
                "2d2e3808145dd16d8d9a83d03fb20bf51518f6efeb84d0da46c84b27c00046b5",
            ["fixture-rfc3161-signed-installer.exe"] =
                "9794b47812c35e1c648cd3026ec152b5bbad85b4c1a3c360d459c331fd602449",
            ["fixture-rfc3161-signed-installer.tampered.exe"] =
                "eb3009da1fb93582750d827919d6088bc7935f2857815f27141f84e902719e0c",
            ["fixture-signed-without-timestamp.exe"] =
                "c8af5e8edc3d23497f2dab18e8c5d15e2fc5e76c229339c4e5a64182b66cda14",
            ["local-fixture-code-signing.crt"] =
                "31e03e7471cfc28198d9fefc790e22566295aace99be9ce13530c1a27e2adcc3",
            ["local-fixture-root.crt"] =
                "7dcdaaca0b71c6a4f6d7dd5d89ce31d8b463c8bab5aabefee74731b124e06d59",
            ["local-fixture-tsa.crt"] =
                "4a5a8a0e50032d30de134e4fa7d7dd8acbf9cf2f8991fdc47e7365d4a8359053",
            ["osslsigncode-rfc3161-verification.txt"] =
                "2527569760d837f14fd961ae6de3b7b58697ef89ccf117add1b430b9cce3876b",
        };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var fixtures = PreflightFixtures(args);
            var positivePath =
                fixtures["fixture-rfc3161-signed-installer.exe"];
            var noTimestampPath =
                fixtures["fixture-signed-without-timestamp.exe"];
            var tamperedPath =
                fixtures["fixture-rfc3161-signed-installer.tampered.exe"];

            using var signerCertificate =
                X509CertificateLoader.LoadCertificateFromFile(
                    fixtures["local-fixture-code-signing.crt"]);
            using var wrongCertificate =
                X509CertificateLoader.LoadCertificateFromFile(
                    fixtures["local-fixture-tsa.crt"]);
            using var rootCertificate =
                X509CertificateLoader.LoadCertificateFromFile(
                    fixtures["local-fixture-root.crt"]);
            var customRoots = new X509Certificate2Collection(rootCertificate);

            var evidence = AuthenticodeVerifier.Verify(
                positivePath,
                signerCertificate,
                customRoots,
                onlineRevocation: false);
            RequireEqual(
                SignerCertificateDerSha256,
                evidence.Signer.CertificateSha256,
                "embedded signer certificate DER pin");
            RequireEqual(
                SignerSpkiSha256,
                evidence.Signer.SpkiSha256,
                "embedded signer SPKI pin");
            RequireEqual(
                "sha256",
                evidence.DigestAlgorithm,
                "Authenticode digest algorithm");
            RequireEqual(
                "rfc3161",
                evidence.Timestamp.Format,
                "timestamp format");
            RequireEqual(
                "sha256",
                evidence.Timestamp.DigestAlgorithm,
                "timestamp digest algorithm");
            RequireEqual(
                "external_test_trust_store",
                evidence.SignerChain.TrustAnchorSource,
                "signer trust-anchor source");
            RequireEqual(
                "external_test_trust_store",
                evidence.Timestamp.Chain.TrustAnchorSource,
                "timestamp trust-anchor source");
            Pass("positive RFC3161 fixture");

            MustReject(
                "missing RFC3161 timestamp",
                "exactly one Microsoft Authenticode RFC3161",
                () => AuthenticodeVerifier.Verify(
                    noTimestampPath,
                    signerCertificate,
                    customRoots,
                    onlineRevocation: false));
            MustReject(
                "tampered PE",
                "PE Authenticode certificate table",
                () => AuthenticodeVerifier.Verify(
                    tamperedPath,
                    signerCertificate,
                    customRoots,
                    onlineRevocation: false));
            MustReject(
                "wrong signer certificate pin",
                "identity differs from the configured KeyLocker",
                () => AuthenticodeVerifier.Verify(
                    positivePath,
                    wrongCertificate,
                    customRoots,
                    onlineRevocation: false));
            MustReject(
                "pre-existing signature",
                "refuses to add or replace",
                () => AuthenticodeVerifier.RequireUnsignedPe(positivePath));

            var javaTreeSha256 =
                await ToolchainPolicy.ComputeCanonicalJavaTreeSha256Async(
                    "/home/tibor/.local/share/ea-tools/chummer-signing/java/"
                    + "temurin-21.0.11+10",
                    TimeSpan.FromMinutes(2));
            RequireEqual(
                ToolchainPolicy.ApprovedJavaTreeSha256,
                javaTreeSha256,
                "canonical Temurin tree");
            ToolchainPolicy.RequireApprovedJavaTreeSha256(javaTreeSha256);
            MustReject(
                "canonical Temurin tree mismatch",
                "differs from the approved canonical tree",
                () => ToolchainPolicy.RequireApprovedJavaTreeSha256(
                    new string('0', 64)));
            Pass("canonical Temurin tree");

            await RunRuntimeHostControls();
            RunRuntimeEnvironmentControls();
            RunStartupCredentialRedactionControl();
            await RunOfflineSealedSignerPreflightControl(
                positivePath,
                fixtures["local-fixture-code-signing.crt"]);

            var buildEvidence = VerifierBuildEvidence.Current();
            RequireEqual(
                "10.0.110",
                buildEvidence.DotnetSdkVersion,
                "build SDK version");
            RequireEqual(
                SignerPackageLockSha256,
                buildEvidence.PackageLockSha256,
                "signer package lock");
            RequireEqual(
                SdkPinPolicy.ApprovedSdkVersion,
                buildEvidence.SdkPin.Version,
                "signer build SDK pin");
            RequireEqual(
                SdkPinPolicy.ApprovedSha256,
                buildEvidence.SdkPin.Sha256,
                "signer global.json identity");
            Pass("verifier build identity");

            var temporaryDirectory =
                Directory.CreateTempSubdirectory(
                    "chummer-keylocker-fixture-tests-").FullName;
            File.SetUnixFileMode(
                temporaryDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
            try
            {
                RunPeMutationMatrix(
                    positivePath,
                    temporaryDirectory,
                    signerCertificate,
                    customRoots);
                RunArtifactBindingControl(
                    positivePath,
                    temporaryDirectory,
                    signerCertificate,
                    customRoots);
            }
            finally
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }

            await RunPipeLifecycleControl();
            await RunCredentialedProcessIsolationControl();

            Console.WriteLine(
                "[fixture-tests] PASS: all verifier controls");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"[fixture-tests] FAIL: {exception.GetType().Name}: "
                + exception.Message);
            return 1;
        }
    }

    private static IReadOnlyDictionary<string, string> PreflightFixtures(
        string[] args)
    {
        if (args.Length != 1
            || !Path.IsPathFullyQualified(args[0])
            || !string.Equals(
                args[0],
                Path.GetFullPath(args[0]),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Usage: fixture test <normalized-absolute-fixture-directory>");
        }
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in FixtureHashes)
        {
            var path = Path.Combine(args[0], pair.Key);
            path = GovernedPath.ResolveRegularFile(
                path,
                $"sealed fixture {pair.Key}",
                [Path.GetExtension(pair.Key).ToLowerInvariant()],
                pair.Key,
                executable: false);
            RequireEqual(
                pair.Value,
                Hashing.FileSha256(path),
                $"sealed fixture {pair.Key}");
            paths[pair.Key] = path;
        }
        Pass("all sealed fixture identities");
        return paths;
    }

    private static void RunPeMutationMatrix(
        string positivePath,
        string temporaryDirectory,
        X509Certificate2 signerCertificate,
        X509Certificate2Collection customRoots)
    {
        void VerifyMutation(
            string label,
            string reason,
            Func<byte[], byte[]> mutation)
        {
            var path = CreateMutation(
                positivePath,
                temporaryDirectory,
                label,
                mutation);
            MustReject(
                label,
                reason,
                () => AuthenticodeVerifier.Verify(
                    path,
                    signerCertificate,
                    customRoots,
                    onlineRevocation: false));
        }

        VerifyMutation(
            "non-terminal certificate table",
            "non-terminal",
            bytes =>
            {
                Array.Resize(ref bytes, bytes.Length + 1);
                return bytes;
            });
        VerifyMutation(
            "too few PE data directories",
            "fewer than five data directories",
            bytes =>
            {
                var headers = ReadHeaders(bytes);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(headers.NumberOfRvaAndSizesOffset, 4),
                    4);
                return bytes;
            });
        VerifyMutation(
            "out-of-range PE section count",
            "section count is outside",
            bytes =>
            {
                var headers = ReadHeaders(bytes);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    bytes.AsSpan(headers.PeOffset + 6, 2),
                    97);
                return bytes;
            });
        VerifyMutation(
            "certificate-table size overflow",
            "certificate table is absent",
            bytes =>
            {
                var headers = ReadHeaders(bytes);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(headers.SecurityDirectoryOffset + 4, 4),
                    checked((uint)headers.CertificateSize + 8));
                return bytes;
            });
        VerifyMutation(
            "mapped section overlaps certificate table",
            "overlaps mapped PE content",
            bytes =>
            {
                var headers = ReadHeaders(bytes);
                var lastSection = Enumerable.Range(
                        0,
                        headers.NumberOfSections)
                    .Select(index =>
                    {
                        var offset = headers.SectionTableOffset + (index * 40);
                        return new
                        {
                            Offset = offset,
                            Pointer = BinaryPrimitives.ReadUInt32LittleEndian(
                                bytes.AsSpan(offset + 20, 4)),
                        };
                    })
                    .OrderByDescending(section => section.Pointer)
                    .First();
                var overlappingSize = checked(
                    (uint)(headers.CertificateOffset + 8)
                    - lastSection.Pointer);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(lastSection.Offset + 16, 4),
                    overlappingSize);
                return bytes;
            });
        VerifyMutation(
            "unauthenticated trailing certificate record",
            "exactly one aligned PKCS#7",
            bytes =>
            {
                var headers = ReadHeaders(bytes);
                Array.Resize(ref bytes, bytes.Length + 8);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(headers.SecurityDirectoryOffset + 4, 4),
                    checked((uint)headers.CertificateSize + 8));
                BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(headers.CertificateOffset
                        + headers.CertificateSize, 4),
                    8);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    bytes.AsSpan(headers.CertificateOffset
                        + headers.CertificateSize + 4, 2),
                    0x0200);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    bytes.AsSpan(headers.CertificateOffset
                        + headers.CertificateSize + 6, 2),
                    0x0002);
                return bytes;
            });
        VerifyMutation(
            "non-zero certificate alignment padding",
            "alignment padding must be zero",
            bytes =>
            {
                var headers = ReadHeaders(bytes);
                bytes[headers.CertificateOffset
                    + headers.CertificateSize - 1] = 1;
                return bytes;
            });
        VerifyMutation(
            "multiple RFC3161 timestamp attributes",
            "exactly one Microsoft Authenticode RFC3161",
            AddDuplicateTimestampAttribute);
    }

    private static void RunArtifactBindingControl(
        string positivePath,
        string temporaryDirectory,
        X509Certificate2 signerCertificate,
        X509Certificate2Collection customRoots)
    {
        var path = CreateMutation(
            positivePath,
            temporaryDirectory,
            "artifact-binding",
            bytes => bytes);
        var evidence = AuthenticodeVerifier.Verify(
            path,
            signerCertificate,
            customRoots,
            onlineRevocation: false);
        using (var stream = new FileStream(
                   path,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.None))
        {
            stream.Position = 2;
            var value = stream.ReadByte();
            stream.Position = 2;
            stream.WriteByte(checked((byte)(value ^ 1)));
            stream.Flush(flushToDisk: true);
        }
        MustReject(
            "verification-to-receipt artifact replacement",
            "verification-to-receipt binding",
            () => ArtifactBinding.RequireUnchanged(
                path,
                evidence.ArtifactSha256,
                "verification-to-receipt binding"));
    }

    private static async Task RunRuntimeHostControls()
    {
        RuntimeHostPolicy.RequireDotnetInstallation(
            RuntimeHostPolicy.ApprovedDotnetRoot,
            RuntimeHostPolicy.ApprovedDotnetPath,
            RuntimeHostPolicy.ApprovedDotnetSha256);
        var dotnetTreeSha256 =
            await RuntimeHostPolicy.ComputeCanonicalDotnetTreeSha256Async(
                RuntimeHostPolicy.ApprovedDotnetRoot,
                TimeSpan.FromMinutes(2));
        RequireEqual(
            RuntimeHostPolicy.ApprovedDotnetTreeSha256,
            dotnetTreeSha256,
            "canonical .NET tree");
        RuntimeHostPolicy.RequireApprovedDotnetTreeSha256(
            dotnetTreeSha256);
        MustReject(
            "canonical .NET tree mismatch",
            "differs from the approved canonical tree",
            () => RuntimeHostPolicy.RequireApprovedDotnetTreeSha256(
                new string('0', 64)));
        Pass("direct .NET host identity and root-owned tree topology");

        var runtimeParent = Directory.CreateTempSubdirectory(
            "chummer-keylocker-runtime-host-tests-").FullName;
        File.SetUnixFileMode(
            runtimeParent,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute);
        var outputDirectory = Path.Combine(runtimeParent, "publish");
        Directory.CreateDirectory(outputDirectory);
        var sourceAssembly =
            Path.GetFullPath(typeof(AuthenticodeVerifier).Assembly.Location);
        var sourceDirectory = Path.GetDirectoryName(sourceAssembly)
            ?? throw new InvalidOperationException(
                "Fixture signer assembly directory is absent.");
        var assemblyPath = Path.Combine(
            outputDirectory,
            SignerOutputPolicy.AssemblyFileName);
        var runtimeConfigPath = Path.Combine(
            outputDirectory,
            SignerOutputPolicy.RuntimeConfigFileName);
        var depsPath = Path.Combine(
            outputDirectory,
            SignerOutputPolicy.DepsFileName);
        var sdkPinPath = Path.Combine(
            outputDirectory,
            SdkPinPolicy.FileName);
        File.Copy(sourceAssembly, assemblyPath);
        File.Copy(
            Path.Combine(
                sourceDirectory,
                SignerOutputPolicy.RuntimeConfigFileName),
            runtimeConfigPath);
        File.Copy(
            Path.Combine(sourceDirectory, SignerOutputPolicy.DepsFileName),
            depsPath);
        File.Copy(
            Path.Combine(sourceDirectory, SdkPinPolicy.FileName),
            sdkPinPath);
        foreach (var path in new[]
                 {
                     assemblyPath,
                     runtimeConfigPath,
                     depsPath,
                     sdkPinPath,
                 })
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead);
        }
        File.SetUnixFileMode(
            outputDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            var assemblySha256 = Hashing.FileSha256(assemblyPath);
            var runtimeConfigSha256 =
                Hashing.FileSha256(runtimeConfigPath);
            var depsSha256 = Hashing.FileSha256(depsPath);
            var output = SignerOutputPolicy.RequireSealedOutput(
                assemblyPath,
                assemblySha256,
                runtimeConfigSha256,
                depsSha256);
            RequireEqual(
                assemblySha256,
                output.AssemblySha256,
                "sealed signer DLL");
            RequireEqual(
                runtimeConfigSha256,
                output.RuntimeConfigSha256,
                "sealed signer runtime configuration");
            RequireEqual(
                depsSha256,
                output.DepsSha256,
                "sealed signer dependency manifest");
            RequireEqual(
                SdkPinPolicy.ApprovedSha256,
                output.SdkPin.Sha256,
                "sealed signer SDK pin");
            var outputTreeSha256 =
                await CanonicalTreeHasher.ComputeSha256Async(
                    runtimeParent,
                    Path.GetFileName(outputDirectory),
                    TimeSpan.FromMinutes(1),
                    "fixture signer output");
            SignerOutputPolicy.RequireTreeSha256(
                outputTreeSha256,
                outputTreeSha256);

            MustReject(
                "sealed signer DLL mismatch",
                "DLL, runtime configuration, or dependency manifest",
                () => SignerOutputPolicy.RequireSealedOutput(
                    assemblyPath,
                    new string('0', 64),
                    runtimeConfigSha256,
                    depsSha256));
            MustReject(
                "sealed signer runtime configuration mismatch",
                "DLL, runtime configuration, or dependency manifest",
                () => SignerOutputPolicy.RequireSealedOutput(
                    assemblyPath,
                    assemblySha256,
                    new string('0', 64),
                    depsSha256));
            MustReject(
                "sealed signer dependency manifest mismatch",
                "DLL, runtime configuration, or dependency manifest",
                () => SignerOutputPolicy.RequireSealedOutput(
                    assemblyPath,
                    assemblySha256,
                    runtimeConfigSha256,
                    new string('0', 64)));
            MustReject(
                "sealed signer output tree mismatch",
                "output tree changed after preflight",
                () => SignerOutputPolicy.RequireTreeSha256(
                    outputTreeSha256,
                    new string('0', 64)));

            var approvedSdkPinBytes = File.ReadAllBytes(sdkPinPath);
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
            File.SetUnixFileMode(
                sdkPinPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.WriteAllText(
                sdkPinPath,
                "{\"sdk\":{\"version\":\"0.0.0\","
                + "\"rollForward\":\"disable\","
                + "\"allowPrerelease\":false}}\n");
            File.SetUnixFileMode(sdkPinPath, UnixFileMode.UserRead);
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserExecute);
            MustReject(
                "wrong signer SDK pin",
                "differs from its exact approved global.json identity",
                () => SignerOutputPolicy.RequireSealedOutput(
                    assemblyPath,
                    assemblySha256,
                    runtimeConfigSha256,
                    depsSha256));
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
            File.SetUnixFileMode(
                sdkPinPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.WriteAllBytes(sdkPinPath, approvedSdkPinBytes);
            File.SetUnixFileMode(sdkPinPath, UnixFileMode.UserRead);
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserExecute);

            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
            File.SetUnixFileMode(
                sdkPinPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Delete(sdkPinPath);
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserExecute);
            MustReject(
                "missing signer SDK pin",
                "regular non-link file",
                () => SignerOutputPolicy.RequireSealedOutput(
                    assemblyPath,
                    assemblySha256,
                    runtimeConfigSha256,
                    depsSha256));
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
            File.WriteAllBytes(sdkPinPath, approvedSdkPinBytes);
            File.SetUnixFileMode(sdkPinPath, UnixFileMode.UserRead);
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserExecute);

            File.SetUnixFileMode(
                assemblyPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            MustReject(
                "writable signer output file",
                "exact mode 0400",
                () => SignerOutputPolicy.RequireSealedTree(
                    outputDirectory));
            File.SetUnixFileMode(assemblyPath, UnixFileMode.UserRead);

            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
            var linkPath = Path.Combine(outputDirectory, "escape.dll");
            File.CreateSymbolicLink(linkPath, "/etc/passwd");
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserExecute);
            MustReject(
                "signer output symbolic link",
                "single-link regular files",
                () => SignerOutputPolicy.RequireSealedTree(
                    outputDirectory));
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
            File.Delete(linkPath);

            MustReject(
                "writable signer output directory",
                "exact mode 0500",
                () => SignerOutputPolicy.RequireSealedTree(
                    outputDirectory));
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserExecute);
            Pass("sealed signer output identities and topology");
        }
        finally
        {
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
            foreach (var path in Directory.EnumerateFiles(outputDirectory))
            {
                File.SetUnixFileMode(
                    path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            Directory.Delete(outputDirectory, recursive: true);
            Directory.Delete(runtimeParent);
        }
    }

    private static void RunRuntimeEnvironmentControls()
    {
        var safe = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_ROOT"] = "/usr/lib/dotnet",
            ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_EnableDiagnostics"] = "0",
            ["DOTNET_EnableDiagnostics_IPC"] = "0",
            ["DOTNET_EnableDiagnostics_Debugger"] = "0",
            ["DOTNET_EnableDiagnostics_Profiler"] = "0",
            ["CHUMMER_WINDOWS_SIGNING_BACKEND"] =
                "digicert_keylocker_linux_jsign",
        };
        RuntimeEnvironmentPolicy.RequireCleanHostEnvironment(safe);

        var startupHook = new Dictionary<string, string>(
            safe,
            StringComparer.Ordinal)
        {
            ["DOTNET_STARTUP_HOOKS"] = "/tmp/hostile.dll",
        };
        MustReject(
            "hostile .NET startup hook",
            "rejects inherited runtime hooks",
            () => RuntimeEnvironmentPolicy.RequireCleanHostEnvironment(
                startupHook));

        var additionalDeps = new Dictionary<string, string>(
            safe,
            StringComparer.Ordinal)
        {
            ["DOTNET_ADDITIONAL_DEPS"] = "/tmp/hostile",
        };
        MustReject(
            "hostile .NET additional dependencies",
            "rejects inherited runtime hooks",
            () => RuntimeEnvironmentPolicy.RequireCleanHostEnvironment(
                additionalDeps));

        var loaderHook = new Dictionary<string, string>(
            safe,
            StringComparer.Ordinal)
        {
            ["LD_PRELOAD"] = "/tmp/hostile.so",
        };
        MustReject(
            "hostile native loader hook",
            "rejects inherited runtime hooks",
            () => RuntimeEnvironmentPolicy.RequireCleanHostEnvironment(
                loaderHook));

        var enabledDiagnostics = new Dictionary<string, string>(
            safe,
            StringComparer.Ordinal)
        {
            ["DOTNET_EnableDiagnostics"] = "1",
        };
        MustReject(
            "enabled .NET diagnostics",
            "diagnostics-disabled direct .NET host environment",
            () => RuntimeEnvironmentPolicy.RequireCleanHostEnvironment(
                enabledDiagnostics));
        Pass("clean direct .NET host environment");
    }

    private static void RunStartupCredentialRedactionControl()
    {
        var apiKey = $"fixture-api-{Guid.NewGuid():N}";
        var certificatePath =
            $"/tmp/fixture-client-{Guid.NewGuid():N}.p12";
        var password = $"fixture-password-{Guid.NewGuid():N}";
        var composite =
            $"{apiKey}|{certificatePath}|{password}";
        var redactor = Redactor.ForTransientCredential(composite);
        var sanitized = redactor.Sanitize(
            "The process cannot access the file '"
            + certificatePath
            + $"' while handling {apiKey}, {password}, and {composite}.");
        foreach (var secret in new[]
                 {
                     composite,
                     apiKey,
                     certificatePath,
                     password,
                 })
        {
            if (sanitized.Contains(secret, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Startup credential redaction retained an exact secret "
                    + "or client-certificate path.");
            }
        }
        if (!sanitized.Contains("[REDACTED]", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Startup credential redaction did not mark removed values.");
        }
        Pass("pre-configuration startup credential and path redaction");
    }

    private static async Task RunOfflineSealedSignerPreflightControl(
        string signedArtifactPath,
        string publicCertificatePath)
    {
        var runtimeParent = Directory.CreateTempSubdirectory(
            "chummer-keylocker-offline-host-").FullName;
        File.SetUnixFileMode(
            runtimeParent,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute);
        var outputDirectory = Path.Combine(runtimeParent, "publish");
        var sourceAssembly =
            Path.GetFullPath(typeof(AuthenticodeVerifier).Assembly.Location);
        var sourceDirectory = Path.GetDirectoryName(sourceAssembly)
            ?? throw new InvalidOperationException(
                "Fixture signer assembly directory is absent.");
        CopyDirectoryTree(sourceDirectory, outputDirectory);
        SealDirectoryTree(outputDirectory);

        var credentialPath = Path.Combine(
            runtimeParent,
            "fixture-client.p12");
        using (var credential = new FileStream(
                   credentialPath,
                   new FileStreamOptions
                   {
                       Mode = FileMode.CreateNew,
                       Access = FileAccess.Write,
                       Share = FileShare.None,
                       UnixCreateMode = UnixFileMode.UserRead,
                   }))
        {
            credential.WriteByte(1);
            credential.Flush(flushToDisk: true);
        }
        File.SetUnixFileMode(credentialPath, UnixFileMode.UserRead);

        var assemblyPath = Path.Combine(
            outputDirectory,
            SignerOutputPolicy.AssemblyFileName);
        var runtimeConfigPath = Path.Combine(
            outputDirectory,
            SignerOutputPolicy.RuntimeConfigFileName);
        var depsPath = Path.Combine(
            outputDirectory,
            SignerOutputPolicy.DepsFileName);
        var outputTreeSha256 =
            await CanonicalTreeHasher.ComputeSha256Async(
                runtimeParent,
                Path.GetFileName(outputDirectory),
                TimeSpan.FromMinutes(1),
                "offline signer output");
        var apiKey = $"fixture-api-{Guid.NewGuid():N}";
        var password = $"fixture-password-{Guid.NewGuid():N}";
        var composite = $"{apiKey}|{credentialPath}|{password}";

        try
        {
            var environment = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["DOTNET_ROOT"] = RuntimeHostPolicy.ApprovedDotnetRoot,
                ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
                ["DOTNET_EnableDiagnostics"] = "0",
                ["DOTNET_EnableDiagnostics_IPC"] = "0",
                ["DOTNET_EnableDiagnostics_Debugger"] = "0",
                ["DOTNET_EnableDiagnostics_Profiler"] = "0",
                ["CHUMMER_WINDOWS_SIGNING_BACKEND"] =
                    "digicert_keylocker_linux_jsign",
                ["CHUMMER_WINDOWS_KEYLOCKER_HOST"] =
                    "https://clientauth.one.digicert.com",
                ["CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS"] =
                    "fixture-key",
                ["CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH"] =
                    publicCertificatePath,
                ["CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256"] =
                    SignerCertificateDerSha256,
                ["CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256"] =
                    SignerSpkiSha256,
                ["CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS"] =
                    composite,
                ["CHUMMER_KEYLOCKER_JAVA_HOME"] =
                    "/home/tibor/.local/share/ea-tools/chummer-signing/"
                    + "java/temurin-21.0.11+10",
                ["CHUMMER_KEYLOCKER_JAVA_BIN"] =
                    "/home/tibor/.local/share/ea-tools/chummer-signing/"
                    + "java/temurin-21.0.11+10/bin/java",
                ["CHUMMER_KEYLOCKER_JAVA_BIN_SHA256"] =
                    "fd85538801d8ca61d3558c87a57a600e1868d8ac9e918d0860dd64281b548643",
                ["CHUMMER_KEYLOCKER_JAVA_TREE_SHA256"] =
                    "3ea9bb5c7fcda4e7b69af5150df3fd9400edbee192998698fa580c26012a9cd5",
                ["CHUMMER_KEYLOCKER_JSIGN_JAR"] =
                    "/home/tibor/.local/share/ea-tools/chummer-signing/"
                    + "jsign/7.5/jsign-7.5.jar",
                ["CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256"] =
                    "602a51c3545a6dc4fb99bd2ea7152b26d1345916d0c93ddfbd5936cb735af91c",
                ["CHUMMER_KEYLOCKER_DOTNET_ROOT"] =
                    RuntimeHostPolicy.ApprovedDotnetRoot,
                ["CHUMMER_KEYLOCKER_DOTNET_BIN"] =
                    RuntimeHostPolicy.ApprovedDotnetPath,
                ["CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256"] =
                    RuntimeHostPolicy.ApprovedDotnetSha256,
                ["CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256"] =
                    RuntimeHostPolicy.ApprovedDotnetTreeSha256,
                ["CHUMMER_KEYLOCKER_SIGNER_DLL"] = assemblyPath,
                ["CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256"] =
                    Hashing.FileSha256(assemblyPath),
                ["CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256"] =
                    Hashing.FileSha256(runtimeConfigPath),
                ["CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256"] =
                    Hashing.FileSha256(depsPath),
                ["CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256"] =
                    outputTreeSha256,
            };
            var startInfo = new ProcessStartInfo
            {
                FileName = RuntimeHostPolicy.ApprovedDotnetPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = "/tmp",
            };
            startInfo.ArgumentList.Add(assemblyPath);
            startInfo.ArgumentList.Add("--artifact");
            startInfo.ArgumentList.Add(signedArtifactPath);
            startInfo.Environment.Clear();
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }

            async Task<(int ExitCode, string Output)> RunSignerAsync()
            {
                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "Offline sealed signer preflight did not start.");
                }
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                using var cancellation =
                    new CancellationTokenSource(TimeSpan.FromMinutes(2));
                try
                {
                    await process.WaitForExitAsync(cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    throw new InvalidOperationException(
                        "Offline sealed signer preflight timed out.");
                }
                return (
                    process.ExitCode,
                    await stdoutTask + await stderrTask);
            }

            void RequireNoCredentialDisclosure(
                string output,
                string phase)
            {
                foreach (var secret in new[]
                         {
                             composite,
                             apiKey,
                             credentialPath,
                             password,
                         })
                {
                    if (output.Contains(secret, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Offline sealed signer {phase} emitted a "
                            + "credential or its client-certificate path.");
                    }
                }
            }

            using (var credentialLock = new FileStream(
                       credentialPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.None))
            {
                var lockedCredentialResult = await RunSignerAsync();
                if (lockedCredentialResult.ExitCode != 2
                    || !lockedCredentialResult.Output.Contains(
                        "client-auth certificate could not be read after its "
                        + "filesystem identity was established",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Offline sealed signer did not fail safely when its "
                        + "client certificate was exclusively locked.");
                }
                RequireNoCredentialDisclosure(
                    lockedCredentialResult.Output,
                    "locked-client-certificate failure");
            }

            var preflightResult = await RunSignerAsync();
            if (preflightResult.ExitCode != 2
                || !preflightResult.Output.Contains(
                    "refuses to add or replace a pre-existing "
                    + "Authenticode signature",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Offline sealed signer did not reach the expected "
                    + "pre-network pre-existing-signature gate.");
            }
            RequireNoCredentialDisclosure(
                preflightResult.Output,
                "preflight");
            Pass("offline direct-host signer preflight without provider contact");
        }
        finally
        {
            MakeDirectoryTreeWritable(runtimeParent);
            Directory.Delete(runtimeParent, recursive: true);
        }
    }

    private static void CopyDirectoryTree(
        string sourceDirectory,
        string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var sourcePath in Directory.EnumerateFileSystemEntries(
                     sourceDirectory))
        {
            var destinationPath = Path.Combine(
                destinationDirectory,
                Path.GetFileName(sourcePath));
            if (Directory.Exists(sourcePath))
            {
                CopyDirectoryTree(sourcePath, destinationPath);
            }
            else
            {
                File.Copy(sourcePath, destinationPath);
            }
        }
    }

    private static void SealDirectoryTree(string directory)
    {
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead);
        }
        foreach (var path in Directory.EnumerateDirectories(directory))
        {
            SealDirectoryTree(path);
        }
        File.SetUnixFileMode(
            directory,
            UnixFileMode.UserRead | UnixFileMode.UserExecute);
    }

    private static void MakeDirectoryTreeWritable(string directory)
    {
        File.SetUnixFileMode(
            directory,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute);
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        foreach (var path in Directory.EnumerateDirectories(directory))
        {
            MakeDirectoryTreeWritable(path);
        }
    }

    private static async Task RunPipeLifecycleControl()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await GovernedProcess.RunAsync(
            "/bin/sh",
            ["-c", "(/bin/sleep 30) & exit 0"],
            "fixture",
            "pipe_lifecycle",
            artifactPath: null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOME"] = "/tmp",
                ["TMPDIR"] = "/tmp",
                ["LANG"] = "C",
                ["LC_ALL"] = "C",
            },
            new Redactor([]),
            TimeSpan.FromSeconds(10),
            credentialed: false);
        stopwatch.Stop();
        if (!result.Transaction.PipeDrainTimedOut
            || stopwatch.Elapsed > TimeSpan.FromSeconds(10))
        {
            throw new InvalidOperationException(
                "Descendant-held pipe lifecycle was not bounded.");
        }
        Pass("descendant-held pipe lifecycle");
    }

    private static async Task RunCredentialedProcessIsolationControl()
    {
        var apiKey = $"fixture-api-{Guid.NewGuid():N}";
        var certificatePath =
            $"/tmp/fixture-client-{Guid.NewGuid():N}.p12";
        var password = $"fixture-password-{Guid.NewGuid():N}";
        var composite =
            $"{apiKey}|{certificatePath}|{password}";
        const string variable =
            "CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS";
        const string script =
            "secret=\"$CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS\"; "
            + "[ -n \"$secret\" ] || exit 24; "
            + "cmdline=\"$(/usr/bin/tr '\\000' '\\n' "
            + "</proc/self/cmdline)\"; "
            + "case \"$cmdline\" in *\"$secret\"*) exit 23;; esac; "
            + "api=\"${secret%%|*}\"; tail=\"${secret#*|}\"; "
            + "cert=\"${tail%%|*}\"; pass=\"${tail#*|}\"; "
            + "printf '%s' \"$api\"; printf '%s' '|' >&2; "
            + "printf '%s' \"$cert\" >&2; printf '%s' '|'; "
            + "printf '%s' \"$pass\"";
        var result = await GovernedProcess.RunAsync(
            "/bin/sh",
            ["-c", script],
            "fixture",
            "credential_isolation",
            artifactPath: null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [variable] = composite,
                ["LANG"] = "C",
                ["LC_ALL"] = "C",
            },
            new Redactor([composite, apiKey, certificatePath, password]),
            TimeSpan.FromSeconds(10),
            credentialed: true);
        if (result.Transaction.ExitCode != 0
            || result.SanitizedOutput.Length != 0
            || result.Transaction.SanitizedOutputSha256 is not null
            || result.Transaction.SanitizedOutputLengthBytes is not null
            || result.Transaction.OutputTruncatedAtBytes is not null
            || !string.Equals(
                result.Transaction.OutputRetention,
                "suppressed_unrecorded",
                StringComparison.Ordinal)
            || !string.Equals(
                result.Transaction.RedactionPolicy,
                "credentialed_output_never_retained_or_hashed",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Credentialed process output or lifecycle metadata was "
                + "retained.");
        }
        foreach (var secret in new[]
                 {
                     composite,
                     apiKey,
                     certificatePath,
                     password,
                 })
        {
            if (result.Transaction.TransactionSha256.Contains(
                    secret,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Credentialed process transaction retained a secret.");
            }
        }
        Pass("credential environment-only argv and output isolation");
    }

    private static byte[] AddDuplicateTimestampAttribute(byte[] bytes)
    {
        var headers = ReadHeaders(bytes);
        var declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(
            bytes.AsSpan(headers.CertificateOffset, 4));
        var cms = new SignedCms();
        cms.Decode(bytes.AsSpan(
            headers.CertificateOffset + 8,
            checked((int)declaredSize - 8)).ToArray());
        var signer = cms.SignerInfos[0];
        var timestamp = signer.UnsignedAttributes
            .Cast<CryptographicAttributeObject>()
            .Single(attribute => attribute.Oid.Value == Rfc3161AttributeOid);
        signer.AddUnsignedAttribute(
            new AsnEncodedData(
                new Oid(Rfc3161AttributeOid),
                timestamp.Values[0].RawData));
        var encodedCms = cms.Encode();
        var newDeclaredSize = checked(encodedCms.Length + 8);
        var newCertificateSize = (newDeclaredSize + 7) & ~7;
        var mutated = new byte[
            checked(headers.CertificateOffset + newCertificateSize)];
        bytes.AsSpan(0, headers.CertificateOffset).CopyTo(mutated);
        BinaryPrimitives.WriteUInt32LittleEndian(
            mutated.AsSpan(headers.CertificateOffset, 4),
            checked((uint)newDeclaredSize));
        BinaryPrimitives.WriteUInt16LittleEndian(
            mutated.AsSpan(headers.CertificateOffset + 4, 2),
            0x0200);
        BinaryPrimitives.WriteUInt16LittleEndian(
            mutated.AsSpan(headers.CertificateOffset + 6, 2),
            0x0002);
        encodedCms.CopyTo(mutated, headers.CertificateOffset + 8);
        BinaryPrimitives.WriteUInt32LittleEndian(
            mutated.AsSpan(headers.SecurityDirectoryOffset + 4, 4),
            checked((uint)newCertificateSize));
        return mutated;
    }

    private static string CreateMutation(
        string sourcePath,
        string temporaryDirectory,
        string label,
        Func<byte[], byte[]> mutation)
    {
        var safeName = string.Concat(
            label.Select(character =>
                char.IsAsciiLetterOrDigit(character) ? character : '-'));
        var path = Path.Combine(
            temporaryDirectory,
            $"{safeName}-{Path.GetRandomFileName()}.exe");
        var bytes = mutation(File.ReadAllBytes(sourcePath));
        using (var stream = new FileStream(
                   path,
                   new FileStreamOptions
                   {
                       Mode = FileMode.CreateNew,
                       Access = FileAccess.Write,
                       Share = FileShare.None,
                       UnixCreateMode =
                           UnixFileMode.UserRead
                           | UnixFileMode.UserWrite,
                   }))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        return path;
    }

    private static PeHeaders ReadHeaders(byte[] bytes)
    {
        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(
            bytes.AsSpan(0x3c, 4));
        var numberOfSections = BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.AsSpan(peOffset + 6, 2));
        var optionalOffset = peOffset + 24;
        var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.AsSpan(peOffset + 20, 2));
        var magic = BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.AsSpan(optionalOffset, 2));
        var numberOfRvaAndSizesOffset = magic == 0x10b
            ? optionalOffset + 92
            : optionalOffset + 108;
        var dataDirectoryOffset = magic == 0x10b
            ? optionalOffset + 96
            : optionalOffset + 112;
        var securityDirectoryOffset = dataDirectoryOffset + (4 * 8);
        return new PeHeaders(
            peOffset,
            numberOfSections,
            optionalOffset + optionalSize,
            numberOfRvaAndSizesOffset,
            securityDirectoryOffset,
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(securityDirectoryOffset, 4))),
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(securityDirectoryOffset + 4, 4))));
    }

    private static void MustReject(
        string label,
        string expectedReason,
        Action operation)
    {
        try
        {
            operation();
        }
        catch (SigningFailureException exception)
        {
            if (!exception.Message.Contains(
                    expectedReason,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{label} failed for the wrong policy reason: "
                    + exception.Message);
            }
            Pass(label);
            return;
        }
        throw new InvalidOperationException(
            $"{label} unexpectedly passed verification.");
    }

    private static void RequireEqual(
        string expected,
        string actual,
        string label)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} differs from its approved identity.");
        }
    }

    private static void Pass(string label)
    {
        Console.WriteLine($"[fixture-tests] PASS: {label}");
    }

    private sealed record PeHeaders(
        int PeOffset,
        int NumberOfSections,
        int SectionTableOffset,
        int NumberOfRvaAndSizesOffset,
        int SecurityDirectoryOffset,
        int CertificateOffset,
        int CertificateSize);
}
