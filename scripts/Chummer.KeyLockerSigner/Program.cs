using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("linux")]

namespace Chummer.KeyLockerSigner;

internal static class Program
{
    private const string Backend = "digicert_keylocker_linux_jsign";
    private const string KeyLockerOrigin =
        "https://clientauth.one.digicert.com";
    private const string TimestampUrl = "http://timestamp.digicert.com";
    private const string JsignVersion = "7.5";
    private const string JsignStorepassVariable =
        "CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS";
    private static readonly TimeSpan ToolIdentityTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SigningTimeout = TimeSpan.FromMinutes(5);

    public static async Task<int> Main(string[] args)
    {
        var startupCredential =
            Environment.GetEnvironmentVariable(JsignStorepassVariable)
            ?? string.Empty;
        var startupRedactor = Redactor.ForTransientCredential(
            startupCredential);
        SigningConfiguration? configuration = null;
        var transactions = new List<ProcessTransaction>();
        var signatureEvidence = new List<ArtifactSignatureEvidence>();
        var resolvedArtifacts = new List<string>();
        string? safeFailure = null;

        try
        {
            configuration = SigningConfiguration.Load(args);
            resolvedArtifacts.AddRange(configuration.ArtifactPaths);
            await configuration.VerifyToolchainAsync();

            var identityResult = await GovernedProcess.RunAsync(
                configuration.JavaPath,
                [
                    "-Djava.net.useSystemProxies=false",
                    "-Dhttp.maxRedirects=0",
                    "-jar",
                    configuration.JsignJarPath,
                    "--version",
                ],
                "jsign",
                "tool_identity",
                null,
                configuration.CreateChildEnvironment(includeCredential: false),
                configuration.Redactor,
                ToolIdentityTimeout,
                credentialed: false);
            transactions.Add(identityResult.Transaction);
            if (identityResult.Transaction.ExitCode != 0
                || identityResult.SanitizedOutput.Trim() != $"Jsign {JsignVersion}")
            {
                throw new SigningFailureException(
                    "Pinned Jsign identity check failed.");
            }

            foreach (var artifactPath in configuration.ArtifactPaths)
            {
                AuthenticodeVerifier.RequireUnsignedPe(artifactPath);
                await configuration.VerifyToolchainAsync();
                configuration.RevalidateClientCertificate();
                var signResult = await GovernedProcess.RunAsync(
                    configuration.JavaPath,
                    [
                        "-Djava.net.useSystemProxies=false",
                        "-Dhttp.maxRedirects=0",
                        "-jar",
                        configuration.JsignJarPath,
                        "sign",
                        "--storetype",
                        "DIGICERTONE",
                        "--keystore",
                        configuration.HostOrigin,
                        "--storepass",
                        $"env:{JsignStorepassVariable}",
                        "--alias",
                        configuration.KeyAlias,
                        "--alg",
                        "SHA-256",
                        "--tsaurl",
                        TimestampUrl,
                        "--tsmode",
                        "RFC3161",
                        "--tsretries",
                        "3",
                        "--tsretrywait",
                        "10",
                        "--quiet",
                        artifactPath,
                    ],
                    "digicert_keylocker_jsign",
                    "sign",
                    artifactPath,
                    configuration.CreateChildEnvironment(includeCredential: true),
                    configuration.Redactor,
                    SigningTimeout,
                    credentialed: true);
                transactions.Add(signResult.Transaction);
                if (signResult.Transaction.ExitCode != 0)
                {
                    throw new SigningFailureException(
                        $"Jsign failed for {Path.GetFileName(artifactPath)}; "
                        + "RFC3161 timestamping is mandatory and no unsigned "
                        + "fallback is permitted.");
                }

                var evidence = AuthenticodeVerifier.Verify(
                    artifactPath,
                    configuration.PublicCertificate,
                    customTrustRoots: null,
                    onlineRevocation: true);
                if (signResult.Transaction.ArtifactSha256AfterOperation is null
                    || !Hashing.FixedHexEquals(
                        signResult.Transaction.ArtifactSha256AfterOperation,
                        evidence.ArtifactSha256))
                {
                    throw new SigningFailureException(
                        "Signed artifact changed between the signer and the "
                        + "independent verifier.");
                }
                if (signatureEvidence.Count != 0
                    && (!CryptographicOperations.FixedTimeEquals(
                            Convert.FromHexString(
                                signatureEvidence[0].Signer.CertificateSha256),
                            Convert.FromHexString(
                                evidence.Signer.CertificateSha256))
                        || !CryptographicOperations.FixedTimeEquals(
                            Convert.FromHexString(
                                signatureEvidence[0].Signer.SpkiSha256),
                            Convert.FromHexString(evidence.Signer.SpkiSha256))))
                {
                    throw new SigningFailureException(
                        "Windows artifacts were not signed by one consistent "
                        + "certificate identity.");
                }
                signatureEvidence.Add(evidence);
            }

            configuration.RequireArtifactHashes(signatureEvidence);
            ReceiptWriter.Write(
                configuration,
                resolvedArtifacts,
                "pass",
                string.Empty,
                transactions,
                signatureEvidence);
            return 0;
        }
        catch (Exception exception)
        {
            safeFailure = configuration?.Redactor.Sanitize(exception.Message)
                ?? startupRedactor.Sanitize(exception.Message);
            if (string.IsNullOrWhiteSpace(safeFailure))
            {
                safeFailure = "Linux KeyLocker signing failed closed.";
            }

            if (configuration is not null)
            {
                try
                {
                    ReceiptWriter.Write(
                        configuration,
                        resolvedArtifacts,
                        "fail",
                        safeFailure,
                        transactions,
                        signatureEvidence);
                }
                catch
                {
                    // Never replace the primary failure with receipt I/O detail.
                }
            }

            Console.Error.WriteLine($"[keylocker-signer] {safeFailure}");
            return 2;
        }
        finally
        {
            configuration?.Dispose();
        }
    }
}

internal sealed class SigningFailureException(string message) : Exception(message);

internal sealed class SigningConfiguration : IDisposable
{
    private const string Backend = "digicert_keylocker_linux_jsign";
    private const string KeyLockerOrigin =
        "https://clientauth.one.digicert.com";
    private const string TimestampUrl = "http://timestamp.digicert.com";
    private const string JsignVersion = "7.5";
    private const string ApprovedJavaHome =
        "/home/tibor/.local/share/ea-tools/chummer-signing/java/"
        + "temurin-21.0.11+10";
    private const string ApprovedJavaPath =
        ApprovedJavaHome + "/bin/java";
    private const string ApprovedJavaSha256 =
        "fd85538801d8ca61d3558c87a57a600e1868d8ac9e918d0860dd64281b548643";
    private const string ApprovedJavaTreeSha256 =
        "3ea9bb5c7fcda4e7b69af5150df3fd9400edbee192998698fa580c26012a9cd5";
    private const string ApprovedJsignPath =
        "/home/tibor/.local/share/ea-tools/chummer-signing/jsign/7.5/"
        + "jsign-7.5.jar";
    private const string ApprovedJsignSha256 =
        "602a51c3545a6dc4fb99bd2ea7152b26d1345916d0c93ddfbd5936cb735af91c";
    private const string StorepassVariable =
        "CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS";
    private const string HostVariable = "CHUMMER_WINDOWS_KEYLOCKER_HOST";
    private static readonly Regex AliasPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedKeyLockerVariables =
        new(StringComparer.Ordinal)
        {
            "CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS",
            "CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH",
            "CHUMMER_WINDOWS_KEYLOCKER_HOST",
            "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256",
            "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256",
        };
    private static readonly HashSet<string> AllowedToolchainVariables =
        new(StringComparer.Ordinal)
        {
            "CHUMMER_KEYLOCKER_JAVA_HOME",
            "CHUMMER_KEYLOCKER_JAVA_BIN",
            "CHUMMER_KEYLOCKER_JAVA_BIN_SHA256",
            "CHUMMER_KEYLOCKER_JAVA_TREE_SHA256",
            "CHUMMER_KEYLOCKER_JSIGN_JAR",
            "CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256",
            "CHUMMER_KEYLOCKER_DOTNET_ROOT",
            "CHUMMER_KEYLOCKER_DOTNET_BIN",
            "CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256",
            "CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_DLL",
            "CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256",
            "CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256",
        };

    private SigningConfiguration(
        IReadOnlyList<string> artifactPaths,
        string receiptPath,
        string appKey,
        string rid,
        string releaseChannel,
        string releaseVersion,
        string keyAlias,
        string publicCertificatePath,
        X509Certificate2 publicCertificate,
        CertificateEvidence publicCertificateEvidence,
        string hostOrigin,
        string clientCertificatePath,
        string clientCertificateSha256,
        string javaHome,
        string javaPath,
        string javaSha256,
        string javaTreeSha256,
        string jsignJarPath,
        string jsignJarSha256,
        RuntimeHostConfiguration runtimeHost,
        string temporaryRoot,
        string transientStorepass,
        Redactor redactor)
    {
        ArtifactPaths = artifactPaths;
        ReceiptPath = receiptPath;
        AppKey = appKey;
        Rid = rid;
        ReleaseChannel = releaseChannel;
        ReleaseVersion = releaseVersion;
        KeyAlias = keyAlias;
        PublicCertificatePath = publicCertificatePath;
        PublicCertificate = publicCertificate;
        PublicCertificateEvidence = publicCertificateEvidence;
        HostOrigin = hostOrigin;
        ClientCertificatePath = clientCertificatePath;
        ClientCertificateSha256 = clientCertificateSha256;
        JavaHome = javaHome;
        JavaPath = javaPath;
        JavaSha256 = javaSha256;
        JavaTreeSha256 = javaTreeSha256;
        JsignJarPath = jsignJarPath;
        JsignJarSha256 = jsignJarSha256;
        RuntimeHost = runtimeHost;
        TemporaryRoot = temporaryRoot;
        TransientStorepass = transientStorepass;
        Redactor = redactor;
    }

    public IReadOnlyList<string> ArtifactPaths { get; }
    public string ReceiptPath { get; }
    public string AppKey { get; }
    public string Rid { get; }
    public string ReleaseChannel { get; }
    public string ReleaseVersion { get; }
    public string KeyAlias { get; }
    public string PublicCertificatePath { get; }
    public X509Certificate2 PublicCertificate { get; }
    public CertificateEvidence PublicCertificateEvidence { get; }
    public string HostOrigin { get; }
    public string ClientCertificatePath { get; }
    private string ClientCertificateSha256 { get; }
    public string JavaHome { get; }
    public string JavaPath { get; }
    public string JavaSha256 { get; }
    public string JavaTreeSha256 { get; }
    public string JsignJarPath { get; }
    public string JsignJarSha256 { get; }
    public RuntimeHostConfiguration RuntimeHost { get; }
    public string TemporaryRoot { get; }
    public string TransientStorepass { get; private set; }
    public Redactor Redactor { get; }

    public static SigningConfiguration Load(string[] args)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new SigningFailureException(
                "The Linux KeyLocker Jsign backend requires Linux.");
        }
        RuntimeEnvironmentPolicy.RequireCurrentHostEnvironment();

        var transientStorepass =
            Environment.GetEnvironmentVariable(StorepassVariable) ?? string.Empty;
        var hostValue = Environment.GetEnvironmentVariable(HostVariable)
            ?? string.Empty;
        var inheritedBashEnv = Environment.GetEnvironmentVariable("BASH_ENV");
        var inheritedEnv = Environment.GetEnvironmentVariable("ENV");
        Environment.SetEnvironmentVariable(StorepassVariable, null);
        Environment.SetEnvironmentVariable(HostVariable, null);
        Environment.SetEnvironmentVariable("BASH_ENV", null);
        Environment.SetEnvironmentVariable("ENV", null);
        Environment.SetEnvironmentVariable("TMPDIR", null);

        var ambientSmNames = new List<string>();
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var name = entry.Key?.ToString() ?? string.Empty;
            if (name.StartsWith("SM_", StringComparison.Ordinal))
            {
                ambientSmNames.Add(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }
        if (ambientSmNames.Count != 0)
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer rejects ambient SM_* configuration; "
                + "credentials are accepted only through its transient fixed "
                + "signer variable.");
        }
        if (!string.IsNullOrEmpty(inheritedBashEnv)
            || !string.IsNullOrEmpty(inheritedEnv))
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer rejects inherited BASH_ENV and ENV "
                + "startup hooks.");
        }

        RejectUnknownSigningVariables();
        var artifactInputs = ParseArtifacts(args);
        var receiptPath = ReceiptPathPolicy.Validate(
            Environment.GetEnvironmentVariable(
                "CHUMMER_WINDOWS_SIGNING_RECEIPT_PATH")
            ?? string.Empty);
        var appKey = ExactText("CHUMMER_DESKTOP_APP_KEY", required: false);
        var rid = ExactText("CHUMMER_DESKTOP_RID", required: false);
        var releaseChannel =
            ExactText("CHUMMER_DESKTOP_RELEASE_CHANNEL", required: false);
        var releaseVersion =
            ExactText("CHUMMER_DESKTOP_RELEASE_VERSION", required: false);

        var configuredBackend =
            ExactText("CHUMMER_WINDOWS_SIGNING_BACKEND", required: true)
            .ToLowerInvariant();
        if (!string.Equals(configuredBackend, Backend, StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                $"This signer accepts only {Backend}.");
        }
        if (!string.Equals(
                Environment.GetEnvironmentVariable(
                    "CHUMMER_WINDOWS_TIMESTAMP_URL")
                    ?? TimestampUrl,
                TimestampUrl,
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Linux KeyLocker requires the fixed DigiCert RFC3161 timestamp "
                + "URL.");
        }

        var keyAlias =
            ExactText("CHUMMER_WINDOWS_KEYLOCKER_KEY_ALIAS", required: true);
        if (!AliasPattern.IsMatch(keyAlias)
            || keyAlias.Contains("..", StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Linux KeyLocker requires a portable exact key alias.");
        }

        var publicCertificatePath = GovernedPath.ResolveRegularFile(
            ExactText(
                "CHUMMER_WINDOWS_KEYLOCKER_CERTIFICATE_PATH",
                required: true),
            "Linux KeyLocker public code-signing certificate",
            [".cer", ".crt", ".pem"],
            allowedFileName: null,
            executable: false);
        var publicCertificate =
            X509CertificateLoader.LoadCertificateFromFile(
                publicCertificatePath);
        if (publicCertificate.HasPrivateKey)
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "Linux KeyLocker accepts only a public code-signing "
                + "certificate file.");
        }
        CertificatePolicy.RequireEku(
            publicCertificate,
            AuthenticodeVerifier.CodeSigningEkuOid,
            "Linux KeyLocker public code-signing certificate");
        CertificatePolicy.RequireLeafSigningPosture(
            publicCertificate,
            dedicatedTimestampAuthority: false,
            "Linux KeyLocker public code-signing certificate");
        var publicCertificateEvidence =
            CertificateEvidence.From(publicCertificate);
        var expectedCertificateSha256 = ExactLowerSha256(
            "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_CERTIFICATE_SHA256");
        var expectedSpkiSha256 = ExactLowerSha256(
            "CHUMMER_WINDOWS_KEYLOCKER_SIGNER_SPKI_SHA256");
        if (!Hashing.FixedHexEquals(
                expectedCertificateSha256,
                publicCertificateEvidence.CertificateSha256)
            || !Hashing.FixedHexEquals(
                expectedSpkiSha256,
                publicCertificateEvidence.SpkiSha256))
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "Linux KeyLocker public certificate does not match the "
                + "governed signer DER and SPKI pins.");
        }

        var hostOrigin = ValidateHost(hostValue);
        var credentialParts = transientStorepass.Split('|');
        if (credentialParts.Length != 3
            || credentialParts.Any(string.IsNullOrWhiteSpace)
            || credentialParts.Any(
                part => part.Any(character => char.IsControl(character))))
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "Linux KeyLocker transient Jsign DIGICERTONE credential must "
                + "contain exactly three nonempty fields.");
        }
        var clientCertificatePath = GovernedPath.ResolveRegularFile(
            credentialParts[1],
            "Linux KeyLocker client-auth certificate",
            [".p12", ".pfx"],
            allowedFileName: null,
            executable: false,
            confidential: true);
        var clientCertificateSha256 =
            CredentialFilePolicy.ValidateAndHash(clientCertificatePath);
        if (string.Equals(
                clientCertificatePath,
                publicCertificatePath,
                StringComparison.Ordinal))
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "The public code-signing certificate must not be the "
                + "KeyLocker client-auth certificate.");
        }

        var javaHome = ExactFixedText(
            "CHUMMER_KEYLOCKER_JAVA_HOME",
            ApprovedJavaHome);
        ToolchainPolicy.RequireJavaHome(javaHome);
        var javaPath = GovernedPath.ResolveRegularFile(
            ExactFixedText(
                "CHUMMER_KEYLOCKER_JAVA_BIN",
                ApprovedJavaPath),
            "Linux KeyLocker Java runtime",
            [],
            "java",
            executable: true);
        var javaPin = ExactLowerSha256(
            "CHUMMER_KEYLOCKER_JAVA_BIN_SHA256");
        if (!Hashing.FixedHexEquals(javaPin, ApprovedJavaSha256))
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "Linux KeyLocker Java pin is not the approved canonical "
                + "runtime digest.");
        }
        var javaTreePin = ExactLowerSha256(
            "CHUMMER_KEYLOCKER_JAVA_TREE_SHA256");
        if (!Hashing.FixedHexEquals(
                javaTreePin,
                ApprovedJavaTreeSha256))
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "Linux KeyLocker Java tree pin is not the approved canonical "
                + "Temurin tree digest.");
        }
        var javaSha = Hashing.FileSha256(javaPath);
        if (!Hashing.FixedHexEquals(ApprovedJavaSha256, javaSha))
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "Linux KeyLocker Java runtime differs from its pinned SHA-256.");
        }

        var jsignJarPath = GovernedPath.ResolveRegularFile(
            ExactFixedText(
                "CHUMMER_KEYLOCKER_JSIGN_JAR",
                ApprovedJsignPath),
            "Linux KeyLocker Jsign artifact",
            [".jar"],
            $"jsign-{JsignVersion}.jar",
            executable: false);
        var jsignPin = ExactLowerSha256(
            "CHUMMER_KEYLOCKER_JSIGN_JAR_SHA256");
        if (!Hashing.FixedHexEquals(jsignPin, ApprovedJsignSha256))
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "Linux KeyLocker Jsign pin is not the approved canonical "
                + "Jsign 7.5 digest.");
        }
        var jsignSha = Hashing.FileSha256(jsignJarPath);
        if (!Hashing.FixedHexEquals(ApprovedJsignSha256, jsignSha))
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "Linux KeyLocker Jsign artifact differs from its pinned "
                + "SHA-256.");
        }

        RuntimeHostConfiguration runtimeHost;
        try
        {
            runtimeHost = RuntimeHostConfiguration.LoadFromEnvironment();
        }
        catch
        {
            publicCertificate.Dispose();
            throw;
        }

        var artifacts = artifactInputs
            .Select(path => GovernedPath.ResolveRegularFile(
                path,
                "Linux KeyLocker artifact",
                [".exe", ".dll"],
                allowedFileName: null,
                executable: false))
            .ToArray();
        if (artifacts.Distinct(StringComparer.Ordinal).Count() != artifacts.Length)
        {
            publicCertificate.Dispose();
            throw new SigningFailureException(
                "Linux KeyLocker artifact paths must be unique.");
        }

        var redactor = new Redactor(
            [
                transientStorepass,
                credentialParts[0],
                credentialParts[1],
                credentialParts[2],
            ]);
        var temporaryRoot = GovernedTemporaryDirectory.Create();
        return new SigningConfiguration(
            artifacts,
            receiptPath,
            appKey,
            rid,
            releaseChannel,
            releaseVersion,
            keyAlias,
            publicCertificatePath,
            publicCertificate,
            publicCertificateEvidence,
            hostOrigin,
            clientCertificatePath,
            clientCertificateSha256,
            javaHome,
            javaPath,
            javaSha,
            javaTreePin,
            jsignJarPath,
            jsignSha,
            runtimeHost,
            temporaryRoot,
            transientStorepass,
            redactor);
    }

    public IReadOnlyDictionary<string, string> CreateChildEnvironment(
        bool includeCredential)
    {
        GovernedTemporaryDirectory.RequirePrivate(TemporaryRoot);

        var environment = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["HOME"] = TemporaryRoot,
            ["TMPDIR"] = TemporaryRoot,
            ["LANG"] = "C.UTF-8",
            ["LC_ALL"] = "C.UTF-8",
        };
        if (includeCredential)
        {
            environment[StorepassVariable] = TransientStorepass;
        }
        return environment;
    }

    public void RevalidateClientCertificate()
    {
        var currentSha256 =
            CredentialFilePolicy.ValidateAndHash(ClientCertificatePath);
        if (!Hashing.FixedHexEquals(
                ClientCertificateSha256,
                currentSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker client-auth certificate changed after "
                + "preflight.");
        }
    }

    public async Task VerifyToolchainAsync()
    {
        var javaTreeTask =
            ToolchainPolicy.ComputeCanonicalJavaTreeSha256Async(
                JavaHome,
                TimeSpan.FromMinutes(2));
        var runtimeHostTask = RuntimeHost.VerifyAsync(
            TimeSpan.FromMinutes(2));
        await Task.WhenAll(javaTreeTask, runtimeHostTask);
        var actualTreeSha256 = await javaTreeTask;
        ToolchainPolicy.RequireApprovedJavaTreeSha256(actualTreeSha256);
        if (!Hashing.FixedHexEquals(JavaTreeSha256, actualTreeSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker configured Java tree pin changed after "
                + "preflight.");
        }
        if (!Hashing.FixedHexEquals(
                JavaSha256,
                Hashing.FileSha256(JavaPath))
            || !Hashing.FixedHexEquals(
                JsignJarSha256,
                Hashing.FileSha256(JsignJarPath)))
        {
            throw new SigningFailureException(
                "Linux KeyLocker pinned Java or Jsign file changed after "
                + "preflight.");
        }
    }

    public void RequireArtifactHashes(
        IReadOnlyList<ArtifactSignatureEvidence> evidence)
    {
        if (evidence.Count != ArtifactPaths.Count)
        {
            throw new SigningFailureException(
                "Signing evidence does not cover every configured artifact.");
        }
        for (var index = 0; index < ArtifactPaths.Count; index++)
        {
            if (!string.Equals(
                    Path.GetFileName(ArtifactPaths[index]),
                    evidence[index].ArtifactFileName,
                    StringComparison.Ordinal))
            {
                throw new SigningFailureException(
                    "Signing evidence artifact identity is inconsistent.");
            }
            ArtifactBinding.RequireUnchanged(
                ArtifactPaths[index],
                evidence[index].ArtifactSha256,
                "verification-to-receipt binding");
        }
    }

    public void Dispose()
    {
        PublicCertificate.Dispose();
        TransientStorepass = string.Empty;
        try
        {
            GovernedTemporaryDirectory.Delete(TemporaryRoot);
        }
        catch
        {
            // Cleanup failure must not replace a signing result or safe error.
        }
    }

    private static string[] ParseArtifacts(string[] args)
    {
        var artifacts = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(
                    args[index],
                    "--artifact",
                    StringComparison.Ordinal)
                || index + 1 >= args.Length)
            {
                throw new SigningFailureException(
                    "Linux KeyLocker signer accepts only repeated "
                    + "--artifact <absolute-path> arguments.");
            }
            artifacts.Add(args[++index]);
        }
        if (artifacts.Count == 0)
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer requires at least one artifact.");
        }
        return [.. artifacts];
    }

    private static void RejectUnknownSigningVariables()
    {
        var forbiddenExact = new HashSet<string>(StringComparer.Ordinal)
        {
            "CHUMMER_WINDOWS_KEYLOCKER_COMMAND",
            "CHUMMER_WINDOWS_KEYLOCKER_SIGNING_COMMAND",
            "CHUMMER_WINDOWS_KEYLOCKER_CSP",
            "CHUMMER_WINDOWS_KEYLOCKER_JAVA_COMMAND",
            "CHUMMER_WINDOWS_KEYLOCKER_JSIGN_COMMAND",
            "CHUMMER_WINDOWS_KEYLOCKER_JSIGN_ARGUMENTS",
            "CHUMMER_WINDOWS_KEYLOCKER_STOREPASS",
            "CHUMMER_WINDOWS_KEYLOCKER_TSA_URL",
            "CHUMMER_WINDOWS_SIGN_PFX_BASE64",
            "CHUMMER_WINDOWS_SIGN_PFX_PATH",
            "CHUMMER_WINDOWS_SIGN_PFX_PASSWORD",
            "CHUMMER_WINDOWS_SIGN_CERT_PASSWORD",
        };
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var name = entry.Key?.ToString() ?? string.Empty;
            var value = entry.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }
            if (forbiddenExact.Contains(name)
                || (name.StartsWith(
                        "CHUMMER_WINDOWS_KEYLOCKER_",
                        StringComparison.Ordinal)
                    && !AllowedKeyLockerVariables.Contains(name))
                || (name.StartsWith(
                        "CHUMMER_WINDOWS_JSIGN_",
                        StringComparison.Ordinal)
                    && !string.Equals(
                        name,
                        StorepassVariable,
                        StringComparison.Ordinal))
                || (name.StartsWith(
                        "CHUMMER_KEYLOCKER_",
                        StringComparison.Ordinal)
                    && !AllowedToolchainVariables.Contains(name)))
            {
                throw new SigningFailureException(
                    "Linux KeyLocker signer rejects unknown, mixed-backend, "
                    + "command, argument, credential, CSP, or TSA overrides.");
            }
        }
    }

    internal static string ExactText(string name, bool required)
    {
        var value = Environment.GetEnvironmentVariable(name) ?? string.Empty;
        if ((required && string.IsNullOrWhiteSpace(value))
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(character => char.IsControl(character)))
        {
            throw new SigningFailureException(
                $"{name} must be exact non-control text.");
        }
        return value;
    }

    internal static string ExactLowerSha256(string name)
    {
        var value = ExactText(name, required: true);
        if (!Regex.IsMatch(
                value,
                "^[0-9a-f]{64}$",
                RegexOptions.CultureInvariant))
        {
            throw new SigningFailureException(
                $"{name} must be one exact lowercase SHA-256.");
        }
        return value;
    }

    internal static string ExactFixedText(
        string name,
        string expected)
    {
        var value = ExactText(name, required: true);
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                $"{name} differs from the approved flagship toolchain "
                + "identity.");
        }
        return expected;
    }

    private static string ValidateHost(string value)
    {
        if (!string.Equals(
                value,
                KeyLockerOrigin,
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                $"Linux KeyLocker requires the fixed credential destination "
                + $"{KeyLockerOrigin}.");
        }
        return KeyLockerOrigin;
    }
}

internal static class ArtifactBinding
{
    public static string CurrentSha256(string path)
    {
        return Hashing.FileSha256(path);
    }

    public static void RequireUnchanged(
        string path,
        string expectedSha256,
        string phase)
    {
        if (!Hashing.FixedHexEquals(
                CurrentSha256(path),
                expectedSha256))
        {
            throw new SigningFailureException(
                $"Signed artifact changed during {phase}.");
        }
    }
}

internal static class GovernedPath
{
    public static string ResolveRegularFile(
        string input,
        string label,
        IReadOnlyCollection<string> allowedExtensions,
        string? allowedFileName,
        bool executable,
        bool confidential = false)
    {
        if (string.IsNullOrWhiteSpace(input)
            || !string.Equals(input, input.Trim(), StringComparison.Ordinal)
            || input.Any(char.IsControl)
            || !Path.IsPathFullyQualified(input)
            || HasExplicitDotSegment(input))
        {
            throw new SigningFailureException(
                $"{label} must be one absolute normalized exact path.");
        }
        var fullPath = Path.GetFullPath(input);
        if (!string.Equals(input, fullPath, StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                $"{label} must be one absolute normalized exact path.");
        }
        RequireNoSymbolicLinkComponents(fullPath, label);

        var file = new FileInfo(fullPath);
        if (!file.Exists
            || file.LinkTarget is not null
            || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new SigningFailureException(
                $"{label} must be one regular non-link file.");
        }
        if (allowedFileName is not null
            && !string.Equals(
                file.Name,
                allowedFileName,
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                $"{label} has an unexpected file name.");
        }
        if (allowedExtensions.Count != 0
            && !allowedExtensions.Contains(
                file.Extension.ToLowerInvariant(),
                StringComparer.Ordinal))
        {
            throw new SigningFailureException(
                $"{label} has an unsupported file extension.");
        }
        if (executable)
        {
            var mode = File.GetUnixFileMode(fullPath);
            const UnixFileMode executeMask =
                UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherExecute;
            if ((mode & executeMask) == 0)
            {
                throw new SigningFailureException(
                    $"{label} must have an executable mode bit.");
            }
        }
        if (confidential)
        {
            var mode = File.GetUnixFileMode(fullPath);
            var ownerReadOnly = UnixFileMode.UserRead;
            var ownerReadWrite =
                UnixFileMode.UserRead | UnixFileMode.UserWrite;
            if (mode != ownerReadOnly && mode != ownerReadWrite)
            {
                throw new SigningFailureException(
                    $"{label} mode must be exactly 0400 or 0600.");
            }
        }
        return fullPath;
    }

    private static bool HasExplicitDotSegment(string path)
    {
        return path.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.None)
            .Any(segment => segment is "." or "..");
    }

    internal static void RequireNoSymbolicLinkComponents(
        string path,
        string label)
    {
        var root = Path.GetPathRoot(path)
            ?? throw new SigningFailureException(
                $"{label} has no filesystem root.");
        var current = root;
        foreach (var segment in path[root.Length..].Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var isDirectory = Directory.Exists(current);
            var isFile = File.Exists(current);
            FileSystemInfo info = isDirectory
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            info.Refresh();
            if (info.LinkTarget is not null
                || ((isDirectory || isFile)
                    && (info.Attributes & FileAttributes.ReparsePoint) != 0))
            {
                throw new SigningFailureException(
                    $"{label} must not traverse a link or reparse point.");
            }
            if (!isDirectory && !isFile)
            {
                if (string.Equals(
                        current,
                        path,
                        StringComparison.Ordinal))
                {
                    return;
                }
                throw new SigningFailureException(
                    $"{label} traverses an absent path component.");
            }
        }
    }
}

internal static class GovernedTemporaryDirectory
{
    private const string Prefix = "chummer-keylocker-jsign-";
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute;

    public static string Create()
    {
        var directory = Directory.CreateTempSubdirectory(Prefix);
        var path = directory.FullName;
        File.SetUnixFileMode(path, PrivateDirectoryMode);
        RequirePrivate(path);
        return path;
    }

    public static void RequirePrivate(string path)
    {
        if (!Path.IsPathFullyQualified(path)
            || !string.Equals(
                path,
                Path.GetFullPath(path),
                StringComparison.Ordinal)
            || !string.Equals(
                Path.GetDirectoryName(path),
                Path.TrimEndingDirectorySeparator(Path.GetTempPath()),
                StringComparison.Ordinal)
            || !Path.GetFileName(path).StartsWith(
                Prefix,
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Linux KeyLocker temporary directory identity is invalid.");
        }

        var directory = new DirectoryInfo(path);
        directory.Refresh();
        if (!directory.Exists
            || directory.LinkTarget is not null
            || (directory.Attributes & FileAttributes.ReparsePoint) != 0
            || File.GetUnixFileMode(path) != PrivateDirectoryMode)
        {
            throw new SigningFailureException(
                "Linux KeyLocker temporary directory is not a private "
                + "non-link directory.");
        }
    }

    public static void Delete(string path)
    {
        RequirePrivate(path);
        Directory.Delete(path, recursive: true);
    }
}

internal static class CredentialFilePolicy
{
    private const long MaximumClientCertificateBytes = 1024 * 1024;

    public static string ValidateAndHash(string path)
    {
        var resolved = GovernedPath.ResolveRegularFile(
            path,
            "Linux KeyLocker client-auth certificate",
            [".p12", ".pfx"],
            allowedFileName: null,
            executable: false,
            confidential: true);
        var status = LinuxFileSystem.ReadIdentity(resolved);
        var permissionBits = status.Mode & 0x1ff;
        if (!status.IsRegularFile
            || status.LinkCount != 1
            || status.UserId != LinuxFileSystem.EffectiveUserId
            || permissionBits is not 0x100 and not 0x180)
        {
            throw new SigningFailureException(
                "Linux KeyLocker client-auth certificate must be a "
                + "caller-owned, single-link regular file with mode 0400 or "
                + "0600.");
        }
        if (status.Size is < 1 or > MaximumClientCertificateBytes)
        {
            throw new SigningFailureException(
                "Linux KeyLocker client-auth certificate size is outside "
                + "the governed 1-byte to 1-MiB range.");
        }
        try
        {
            return Hashing.FileSha256(resolved);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new SigningFailureException(
                "Linux KeyLocker client-auth certificate could not be read "
                + "after its filesystem identity was established.");
        }
    }
}

internal sealed record LinuxFileIdentity(
    ushort Mode,
    uint UserId,
    uint LinkCount,
    long Size)
{
    public bool IsRegularFile => (Mode & 0xf000) == 0x8000;
    public bool IsDirectory => (Mode & 0xf000) == 0x4000;
    public bool IsSymbolicLink => (Mode & 0xf000) == 0xa000;
}

internal static class LinuxFileSystem
{
    private const int AtFileDescriptorCurrentWorkingDirectory = -100;
    private const int AtSymbolicLinkNoFollow = 0x100;
    private const uint StatxBasicStats = 0x7ff;
    private const uint RequiredStatxMask = 0x20f;

    public static uint EffectiveUserId => NativeMethods.GetEffectiveUserId();

    public static LinuxFileIdentity ReadIdentity(string path)
    {
        if (NativeMethods.Statx(
                AtFileDescriptorCurrentWorkingDirectory,
                path,
                AtSymbolicLinkNoFollow,
                StatxBasicStats,
                out var status) != 0
            || (status.Mask & RequiredStatxMask) != RequiredStatxMask)
        {
            throw new SigningFailureException(
                "Linux KeyLocker could not establish a governed filesystem "
                + "identity.");
        }
        return new LinuxFileIdentity(
            status.Mode,
            status.UserId,
            status.LinkCount,
            status.Size);
    }

    private static class NativeMethods
    {
        [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
        internal static extern int Statx(
            int directoryFileDescriptor,
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            string path,
            int flags,
            uint mask,
            out StatxBuffer buffer);

        [DllImport("libc", EntryPoint = "geteuid")]
        internal static extern uint GetEffectiveUserId();
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    internal struct StatxBuffer
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(16)]
        public uint LinkCount;

        [FieldOffset(20)]
        public uint UserId;

        [FieldOffset(28)]
        public ushort Mode;

        [FieldOffset(40)]
        public long Size;
    }
}

internal static class ToolchainPolicy
{
    private const string ApprovedJavaHome =
        "/home/tibor/.local/share/ea-tools/chummer-signing/java/"
        + "temurin-21.0.11+10";
    private const string ApprovedJavaParent =
        "/home/tibor/.local/share/ea-tools/chummer-signing/java";
    private const string ApprovedJavaDirectoryName = "temurin-21.0.11+10";
    public const string ApprovedJavaTreeSha256 =
        "3ea9bb5c7fcda4e7b69af5150df3fd9400edbee192998698fa580c26012a9cd5";

    public static void RequireJavaHome(string javaHome)
    {
        if (!string.Equals(
                javaHome,
                ApprovedJavaHome,
                StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(javaHome)
            || !string.Equals(
                javaHome,
                Path.GetFullPath(javaHome),
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Linux KeyLocker requires the fixed approved Temurin home.");
        }

        var root = Path.GetPathRoot(javaHome)
            ?? throw new SigningFailureException(
                "Approved Temurin home has no filesystem root.");
        var current = root;
        RequireTrustedDirectory(current);
        foreach (var segment in javaHome[root.Length..].Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            RequireTrustedDirectory(current);
        }
    }

    public static async Task<string> ComputeCanonicalJavaTreeSha256Async(
        string javaHome,
        TimeSpan timeout)
    {
        RequireJavaHome(javaHome);
        return await CanonicalTreeHasher.ComputeSha256Async(
            ApprovedJavaParent,
            ApprovedJavaDirectoryName,
            timeout,
            "Java");
    }

    public static void RequireApprovedJavaTreeSha256(string actual)
    {
        if (!Hashing.FixedHexEquals(
                actual,
                ApprovedJavaTreeSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker Temurin installation differs from the "
                + "approved canonical tree.");
        }
    }

    private static void RequireTrustedDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        directory.Refresh();
        var identity = LinuxFileSystem.ReadIdentity(path);
        const ushort groupWrite = 0x10;
        const ushort otherWrite = 0x02;
        var ownedByRoot = identity.UserId == 0;
        var ownedBySigner =
            identity.UserId == LinuxFileSystem.EffectiveUserId;
        if (!directory.Exists
            || directory.LinkTarget is not null
            || (directory.Attributes & FileAttributes.ReparsePoint) != 0
            || !identity.IsDirectory
            || (!ownedByRoot && !ownedBySigner)
            || (identity.Mode & otherWrite) != 0
            || (ownedByRoot && (identity.Mode & groupWrite) != 0))
        {
            throw new SigningFailureException(
                "Approved Temurin home must traverse only trusted, "
                + "non-writable directories owned by root or the signer.");
        }
    }
}

internal static class CanonicalTreeHasher
{
    private const string TarPath = "/usr/bin/tar";
    public const string Canonicalization =
        "gnu_tar_parent_and_root_name_v1";

    public static async Task<string> ComputeSha256Async(
        string parentPath,
        string rootName,
        TimeSpan timeout,
        string label)
    {
        if (!Path.IsPathFullyQualified(parentPath)
            || !string.Equals(
                parentPath,
                Path.GetFullPath(parentPath),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(rootName)
            || !string.Equals(
                rootName,
                Path.GetFileName(rootName),
                StringComparison.Ordinal)
            || rootName is "." or ".."
            || rootName[0] == '-'
            || rootName.Any(char.IsControl)
            || timeout <= TimeSpan.Zero)
        {
            throw new SigningFailureException(
                $"Linux KeyLocker canonical {label} tree inputs are invalid.");
        }
        var rootPath = Path.Combine(parentPath, rootName);
        if (!Directory.Exists(rootPath))
        {
            throw new SigningFailureException(
                $"Linux KeyLocker canonical {label} tree root is absent.");
        }

        var tarPath = SystemToolPolicy.RequireRootOwnedExecutable(
            TarPath,
            "Linux KeyLocker canonical tree archiver");
        var startInfo = new ProcessStartInfo
        {
            FileName = tarPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
                 {
                     "--sort=name",
                     "--mtime=UTC 1970-01-01",
                     "--owner=0",
                     "--group=0",
                     "--numeric-owner",
                     "-C",
                     parentPath,
                     "-cf",
                     "-",
                     rootName,
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.Environment.Clear();
        startInfo.Environment["LANG"] = "C";
        startInfo.Environment["LC_ALL"] = "C";

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new SigningFailureException(
                $"Linux KeyLocker canonical {label} tree hash did not start.");
        }
        using var cancellation = new CancellationTokenSource(timeout);
        var hashTask = SHA256.HashDataAsync(
                process.StandardOutput.BaseStream,
                cancellation.Token)
            .AsTask();
        var stderrTask = DrainAsync(
            process.StandardError.BaseStream,
            cancellation.Token);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            var digest = await hashTask;
            await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new SigningFailureException(
                    $"Linux KeyLocker canonical {label} tree hash failed.");
            }
            return Convert.ToHexString(digest).ToLowerInvariant();
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The archiver may have exited at the timeout boundary.
            }
            throw new SigningFailureException(
                $"Linux KeyLocker canonical {label} tree hash timed out.");
        }
    }

    private static async Task DrainAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (await stream.ReadAsync(
                   buffer.AsMemory(),
                   cancellationToken) != 0)
        {
            // Intentionally discard all archiver diagnostic output.
        }
    }
}

internal static class RuntimeEnvironmentPolicy
{
    private static readonly IReadOnlyDictionary<string, string>
        RequiredDotnetVariables =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOTNET_ROOT"] = "/usr/lib/dotnet",
                ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
                ["DOTNET_EnableDiagnostics"] = "0",
                ["DOTNET_EnableDiagnostics_IPC"] = "0",
                ["DOTNET_EnableDiagnostics_Debugger"] = "0",
                ["DOTNET_EnableDiagnostics_Profiler"] = "0",
            };
    private static readonly HashSet<string> ForbiddenExactVariables =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "PATH",
            "HOME",
            "TMPDIR",
            "BASH_ENV",
            "ENV",
            "LD_PRELOAD",
            "LD_LIBRARY_PATH",
            "HTTP_PROXY",
            "HTTPS_PROXY",
            "ALL_PROXY",
            "NO_PROXY",
            "LANG",
            "LOCPATH",
            "GCONV_PATH",
            "NLSPATH",
            "JAVA_HOME",
            "JAVA_TOOL_OPTIONS",
            "_JAVA_OPTIONS",
            "JDK_JAVA_OPTIONS",
            "CLASSPATH",
            "OPENSSL_CONF",
            "SSL_CERT_FILE",
            "SSL_CERT_DIR",
            "MONO_PATH",
            "MONO_ENV_OPTIONS",
            "CLR_OPENSSL_VERSION_OVERRIDE",
            "CLR_ICU_VERSION_OVERRIDE",
        };

    public static void RequireCurrentHostEnvironment()
    {
        var environment = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var name = entry.Key?.ToString() ?? string.Empty;
            environment[name] = entry.Value?.ToString() ?? string.Empty;
        }
        RequireCleanHostEnvironment(environment);
    }

    public static void RequireCleanHostEnvironment(
        IReadOnlyDictionary<string, string> environment)
    {
        foreach (var required in RequiredDotnetVariables)
        {
            if (!environment.TryGetValue(required.Key, out var actual)
                || !string.Equals(
                    actual,
                    required.Value,
                    StringComparison.Ordinal))
            {
                throw new SigningFailureException(
                    "Linux KeyLocker requires an exact diagnostics-disabled "
                    + "direct .NET host environment.");
            }
        }

        foreach (var pair in environment)
        {
            var name = pair.Key;
            var value = pair.Value;
            var unexpectedDotnet =
                name.StartsWith("DOTNET_", StringComparison.Ordinal)
                && !RequiredDotnetVariables.ContainsKey(name);
            var forbiddenPrefix =
                name.StartsWith(
                    "COREHOST_",
                    StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(
                    "COMPlus_",
                    StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(
                    "NUGET_",
                    StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(
                    "MSBuild",
                    StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(
                    "LC_",
                    StringComparison.OrdinalIgnoreCase);
            var shellFunction =
                name.StartsWith(
                    "BASH_FUNC_",
                    StringComparison.Ordinal)
                || value.StartsWith("() {", StringComparison.Ordinal);
            if (unexpectedDotnet
                || forbiddenPrefix
                || ForbiddenExactVariables.Contains(name)
                || shellFunction)
            {
                throw new SigningFailureException(
                    "Linux KeyLocker rejects inherited runtime hooks, "
                    + "dependency stores, loaders, proxies, plugins, locale "
                    + "overrides, and shell functions.");
            }
        }
    }
}

internal static class RuntimeHostPolicy
{
    public const string ApprovedDotnetRoot = "/usr/lib/dotnet";
    public const string ApprovedDotnetPath = "/usr/lib/dotnet/dotnet";
    public const string ApprovedDotnetSha256 =
        "a2e03e682b5ba32303077bc5ed95ca3dd6b57b6d55d09491b67444644e211940";
    public const string ApprovedDotnetTreeSha256 =
        "ba27f662b28bfe7b938b8c862c41e07739db8182a42481a6a0cc5b385ec5f2be";
    private const int MaximumTreeEntries = 100_000;

    public static void RequireApprovedDotnetTreeSha256(string actual)
    {
        if (!Hashing.FixedHexEquals(
                actual,
                ApprovedDotnetTreeSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker .NET installation differs from the "
                + "approved canonical tree.");
        }
    }

    public static async Task<string> ComputeCanonicalDotnetTreeSha256Async(
        string dotnetRoot,
        TimeSpan timeout)
    {
        RequireExactRoot(dotnetRoot);
        return await CanonicalTreeHasher.ComputeSha256Async(
            "/usr/lib",
            "dotnet",
            timeout,
            ".NET");
    }

    public static void RequireDotnetInstallation(
        string dotnetRoot,
        string dotnetPath,
        string expectedDotnetSha256)
    {
        RequireExactRoot(dotnetRoot);
        if (!string.Equals(
                dotnetPath,
                ApprovedDotnetPath,
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Linux KeyLocker requires the fixed direct .NET host.");
        }
        var resolvedDotnet = SystemToolPolicy.RequireRootOwnedExecutable(
            dotnetPath,
            "Linux KeyLocker direct .NET host");
        if (!Hashing.FixedHexEquals(
                expectedDotnetSha256,
                ApprovedDotnetSha256)
            || !Hashing.FixedHexEquals(
                Hashing.FileSha256(resolvedDotnet),
                ApprovedDotnetSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker direct .NET host differs from its approved "
                + "SHA-256.");
        }
        RequireRootOwnedTree(dotnetRoot);
    }

    public static void RequireCurrentProcess(
        string expectedSignerAssemblyPath)
    {
        if (!string.Equals(
                Environment.ProcessPath,
                ApprovedDotnetPath,
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer was not launched by the fixed direct "
                + ".NET host.");
        }

        byte[] commandLineBytes;
        try
        {
            commandLineBytes = File.ReadAllBytes("/proc/self/cmdline");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new SigningFailureException(
                "Linux KeyLocker could not attest its direct host command "
                + "line.");
        }
        if (commandLineBytes.Length is < 3 or > 1_048_576
            || commandLineBytes[^1] != 0)
        {
            throw new SigningFailureException(
                "Linux KeyLocker direct host command line is malformed.");
        }
        var arguments = Encoding.UTF8.GetString(commandLineBytes)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (arguments.Length < 4
            || !string.Equals(
                arguments[0],
                ApprovedDotnetPath,
                StringComparison.Ordinal)
            || !string.Equals(
                arguments[1],
                expectedSignerAssemblyPath,
                StringComparison.Ordinal)
            || (arguments.Length - 2) % 2 != 0)
        {
            throw new SigningFailureException(
                "Linux KeyLocker direct host command line does not match the "
                + "sealed signer contract.");
        }
        for (var index = 2; index < arguments.Length; index += 2)
        {
            if (!string.Equals(
                    arguments[index],
                    "--artifact",
                    StringComparison.Ordinal))
            {
                throw new SigningFailureException(
                    "Linux KeyLocker direct host accepts only repeated "
                    + "artifact arguments.");
            }
        }
    }

    public static void RequireRootOwnedTree(string dotnetRoot)
    {
        RequireExactRoot(dotnetRoot);
        var pending = new Queue<string>();
        pending.Enqueue(dotnetRoot);
        var entryCount = 0;
        while (pending.Count != 0)
        {
            var current = pending.Dequeue();
            foreach (var path in Directory.EnumerateFileSystemEntries(current))
            {
                entryCount++;
                if (entryCount > MaximumTreeEntries)
                {
                    throw new SigningFailureException(
                        "Linux KeyLocker .NET tree exceeds its governed entry "
                        + "limit.");
                }
                var identity = LinuxFileSystem.ReadIdentity(path);
                if (identity.IsSymbolicLink)
                {
                    RequireSymlinkTargetInsideRoot(path, dotnetRoot);
                    continue;
                }
                const ushort groupOrOtherWrite = 0x12;
                if (identity.UserId != 0
                    || (identity.Mode & groupOrOtherWrite) != 0
                    || (!identity.IsDirectory && !identity.IsRegularFile))
                {
                    throw new SigningFailureException(
                        "Linux KeyLocker .NET tree must contain only "
                        + "root-owned, non-writable files and directories or "
                        + "internal symbolic links.");
                }
                if (identity.IsDirectory)
                {
                    pending.Enqueue(path);
                }
            }
        }
    }

    private static void RequireExactRoot(string dotnetRoot)
    {
        if (!string.Equals(
                dotnetRoot,
                ApprovedDotnetRoot,
                StringComparison.Ordinal)
            || !string.Equals(
                dotnetRoot,
                Path.GetFullPath(dotnetRoot),
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Linux KeyLocker requires the fixed approved .NET root.");
        }
        GovernedPath.RequireNoSymbolicLinkComponents(
            dotnetRoot,
            "Linux KeyLocker .NET root");
        var identity = LinuxFileSystem.ReadIdentity(dotnetRoot);
        const ushort groupOrOtherWrite = 0x12;
        if (!identity.IsDirectory
            || identity.UserId != 0
            || (identity.Mode & groupOrOtherWrite) != 0)
        {
            throw new SigningFailureException(
                "Linux KeyLocker .NET root must be a root-owned non-writable "
                + "directory.");
        }
    }

    private static void RequireSymlinkTargetInsideRoot(
        string path,
        string root)
    {
        try
        {
            FileSystemInfo link = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            var target = link.ResolveLinkTarget(returnFinalTarget: true)
                ?? throw new SigningFailureException(
                    "Linux KeyLocker .NET tree contains an unresolved "
                    + "symbolic link.");
            var targetPath = Path.GetFullPath(target.FullName);
            var rootPrefix =
                Path.TrimEndingDirectorySeparator(root)
                + Path.DirectorySeparatorChar;
            if (!string.Equals(
                    targetPath,
                    root,
                    StringComparison.Ordinal)
                && !targetPath.StartsWith(
                    rootPrefix,
                    StringComparison.Ordinal))
            {
                throw new SigningFailureException(
                    "Linux KeyLocker .NET tree contains a symbolic link that "
                    + "escapes the approved root.");
            }
        }
        catch (SigningFailureException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            throw new SigningFailureException(
                "Linux KeyLocker .NET tree contains an invalid symbolic link.");
        }
    }
}

internal static class SignerOutputPolicy
{
    public const string AssemblyFileName = "Chummer.KeyLockerSigner.dll";
    public const string RuntimeConfigFileName =
        "Chummer.KeyLockerSigner.runtimeconfig.json";
    public const string DepsFileName = "Chummer.KeyLockerSigner.deps.json";
    private const ushort PrivateRuntimeParentMode = 0x1c0;
    private const ushort SealedDirectoryMode = 0x140;
    private const ushort SealedFileMode = 0x100;
    private const int MaximumOutputEntries = 4096;

    public static void RequireTreeSha256(
        string expectedSha256,
        string actualSha256)
    {
        if (!Hashing.FixedHexEquals(expectedSha256, actualSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker sealed signer output tree changed after "
                + "preflight.");
        }
    }

    public static SignerOutputEvidence RequireSealedOutput(
        string signerAssemblyPath,
        string expectedAssemblySha256,
        string expectedRuntimeConfigSha256,
        string expectedDepsSha256)
    {
        var assemblyPath = GovernedPath.ResolveRegularFile(
            signerAssemblyPath,
            "Linux KeyLocker signer assembly",
            [".dll"],
            AssemblyFileName,
            executable: false);
        var outputDirectory = Path.GetDirectoryName(assemblyPath)
            ?? throw new SigningFailureException(
                "Linux KeyLocker signer output directory is absent.");
        RequireSealedTree(outputDirectory);
        var runtimeConfigPath = GovernedPath.ResolveRegularFile(
            Path.Combine(outputDirectory, RuntimeConfigFileName),
            "Linux KeyLocker signer runtime configuration",
            [".json"],
            RuntimeConfigFileName,
            executable: false);
        var depsPath = GovernedPath.ResolveRegularFile(
            Path.Combine(outputDirectory, DepsFileName),
            "Linux KeyLocker signer dependency manifest",
            [".json"],
            DepsFileName,
            executable: false);
        var sdkPin = SdkPinPolicy.Require(
            Path.Combine(outputDirectory, SdkPinPolicy.FileName));

        var assemblySha256 = Hashing.FileSha256(assemblyPath);
        var runtimeConfigSha256 = Hashing.FileSha256(runtimeConfigPath);
        var depsSha256 = Hashing.FileSha256(depsPath);
        if (!Hashing.FixedHexEquals(
                assemblySha256,
                expectedAssemblySha256)
            || !Hashing.FixedHexEquals(
                runtimeConfigSha256,
                expectedRuntimeConfigSha256)
            || !Hashing.FixedHexEquals(
                depsSha256,
                expectedDepsSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker sealed signer DLL, runtime configuration, or "
                + "dependency manifest differs from its supplied SHA-256.");
        }
        RequireSealedTree(outputDirectory);
        return new SignerOutputEvidence(
            outputDirectory,
            assemblyPath,
            runtimeConfigPath,
            depsPath,
            sdkPin,
            assemblySha256,
            runtimeConfigSha256,
            depsSha256);
    }

    public static void RequireSealedTree(string outputDirectory)
    {
        if (!Path.IsPathFullyQualified(outputDirectory)
            || !string.Equals(
                outputDirectory,
                Path.GetFullPath(outputDirectory),
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer output requires one normalized "
                + "absolute directory.");
        }
        GovernedPath.RequireNoSymbolicLinkComponents(
            outputDirectory,
            "Linux KeyLocker signer output");
        var parent = Path.GetDirectoryName(outputDirectory)
            ?? throw new SigningFailureException(
                "Linux KeyLocker signer output parent is absent.");
        var parentIdentity = LinuxFileSystem.ReadIdentity(parent);
        if (!parentIdentity.IsDirectory
            || parentIdentity.UserId != LinuxFileSystem.EffectiveUserId
            || (parentIdentity.Mode & 0x1ff) != PrivateRuntimeParentMode)
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer runtime parent must be a caller-owned "
                + "private 0700 directory.");
        }

        var pending = new Queue<string>();
        pending.Enqueue(outputDirectory);
        var fileCount = 0;
        var entryCount = 0;
        while (pending.Count != 0)
        {
            var current = pending.Dequeue();
            var directoryIdentity = LinuxFileSystem.ReadIdentity(current);
            if (!directoryIdentity.IsDirectory
                || directoryIdentity.IsSymbolicLink
                || directoryIdentity.UserId
                    != LinuxFileSystem.EffectiveUserId
                || (directoryIdentity.Mode & 0x1ff) != SealedDirectoryMode)
            {
                throw new SigningFailureException(
                    "Linux KeyLocker signer output directories must be "
                    + "caller-owned non-links with exact mode 0500.");
            }

            foreach (var path in Directory.EnumerateFileSystemEntries(current))
            {
                entryCount++;
                if (entryCount > MaximumOutputEntries)
                {
                    throw new SigningFailureException(
                        "Linux KeyLocker signer output exceeds its governed "
                        + "entry limit.");
                }
                var identity = LinuxFileSystem.ReadIdentity(path);
                if (identity.IsDirectory)
                {
                    pending.Enqueue(path);
                    continue;
                }
                if (!identity.IsRegularFile
                    || identity.IsSymbolicLink
                    || identity.UserId
                        != LinuxFileSystem.EffectiveUserId
                    || identity.LinkCount != 1
                    || (identity.Mode & 0x1ff) != SealedFileMode)
                {
                    throw new SigningFailureException(
                        "Linux KeyLocker signer output entries must be "
                        + "caller-owned single-link regular files with exact "
                        + "mode 0400.");
                }
                fileCount++;
            }
        }
        if (fileCount < 3)
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer output is incomplete.");
        }
    }
}

internal sealed record SignerOutputEvidence(
    string OutputDirectory,
    string AssemblyPath,
    string RuntimeConfigPath,
    string DepsPath,
    SdkPinEvidence SdkPin,
    string AssemblySha256,
    string RuntimeConfigSha256,
    string DepsSha256);

internal sealed record RuntimeHostEvidence(
    string Invocation,
    string DotnetRootDirectoryName,
    string DotnetFileName,
    string DotnetSha256,
    string DotnetTreeSha256,
    string DotnetTreeCanonicalization,
    string SignerAssemblyFileName,
    string SignerAssemblySha256,
    string SignerRuntimeConfigFileName,
    string SignerRuntimeConfigSha256,
    string SignerDepsFileName,
    string SignerDepsSha256,
    SdkPinEvidence SignerSdkPin,
    string SignerOutputTreeSha256,
    string SignerOutputTreeCanonicalization,
    string SignerOutputSeal);

internal sealed class RuntimeHostConfiguration
{
    private RuntimeHostConfiguration(
        string dotnetRoot,
        string dotnetPath,
        string dotnetSha256,
        string dotnetTreeSha256,
        SignerOutputEvidence signerOutput,
        string signerOutputTreeSha256)
    {
        DotnetRoot = dotnetRoot;
        DotnetPath = dotnetPath;
        DotnetSha256 = dotnetSha256;
        DotnetTreeSha256 = dotnetTreeSha256;
        SignerOutput = signerOutput;
        SignerOutputTreeSha256 = signerOutputTreeSha256;
        Evidence = new RuntimeHostEvidence(
            "direct_pinned_dotnet_dll",
            Path.GetFileName(dotnetRoot),
            Path.GetFileName(dotnetPath),
            dotnetSha256,
            dotnetTreeSha256,
            CanonicalTreeHasher.Canonicalization,
            Path.GetFileName(signerOutput.AssemblyPath),
            signerOutput.AssemblySha256,
            Path.GetFileName(signerOutput.RuntimeConfigPath),
            signerOutput.RuntimeConfigSha256,
            Path.GetFileName(signerOutput.DepsPath),
            signerOutput.DepsSha256,
            signerOutput.SdkPin,
            signerOutputTreeSha256,
            CanonicalTreeHasher.Canonicalization,
            "current_euid_0700_parent_0500_directories_0400_single_link_files");
    }

    public string DotnetRoot { get; }
    public string DotnetPath { get; }
    public string DotnetSha256 { get; }
    public string DotnetTreeSha256 { get; }
    public SignerOutputEvidence SignerOutput { get; }
    public string SignerOutputTreeSha256 { get; }
    public RuntimeHostEvidence Evidence { get; }

    public static RuntimeHostConfiguration LoadFromEnvironment()
    {
        var dotnetRoot = SigningConfiguration.ExactFixedText(
            "CHUMMER_KEYLOCKER_DOTNET_ROOT",
            RuntimeHostPolicy.ApprovedDotnetRoot);
        var dotnetPath = SigningConfiguration.ExactFixedText(
            "CHUMMER_KEYLOCKER_DOTNET_BIN",
            RuntimeHostPolicy.ApprovedDotnetPath);
        var dotnetSha256 = SigningConfiguration.ExactLowerSha256(
            "CHUMMER_KEYLOCKER_DOTNET_BIN_SHA256");
        if (!Hashing.FixedHexEquals(
                dotnetSha256,
                RuntimeHostPolicy.ApprovedDotnetSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker .NET binary pin is not the approved "
                + "canonical host digest.");
        }
        var dotnetTreeSha256 = SigningConfiguration.ExactLowerSha256(
            "CHUMMER_KEYLOCKER_DOTNET_TREE_SHA256");
        if (!Hashing.FixedHexEquals(
                dotnetTreeSha256,
                RuntimeHostPolicy.ApprovedDotnetTreeSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker .NET tree pin is not the approved canonical "
                + "host tree digest.");
        }
        RuntimeHostPolicy.RequireDotnetInstallation(
            dotnetRoot,
            dotnetPath,
            dotnetSha256);

        var signerAssemblyPath = SigningConfiguration.ExactText(
            "CHUMMER_KEYLOCKER_SIGNER_DLL",
            required: true);
        var loadedAssemblyPath =
            Path.GetFullPath(typeof(AuthenticodeVerifier).Assembly.Location);
        if (!string.Equals(
                signerAssemblyPath,
                loadedAssemblyPath,
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer DLL path differs from the loaded "
                + "sealed verifier assembly.");
        }
        var signerAssemblySha256 = SigningConfiguration.ExactLowerSha256(
            "CHUMMER_KEYLOCKER_SIGNER_DLL_SHA256");
        var signerRuntimeConfigSha256 =
            SigningConfiguration.ExactLowerSha256(
                "CHUMMER_KEYLOCKER_SIGNER_RUNTIME_CONFIG_SHA256");
        var signerDepsSha256 = SigningConfiguration.ExactLowerSha256(
            "CHUMMER_KEYLOCKER_SIGNER_DEPS_SHA256");
        var signerOutputTreeSha256 =
            SigningConfiguration.ExactLowerSha256(
                "CHUMMER_KEYLOCKER_SIGNER_OUTPUT_TREE_SHA256");
        var signerOutput = SignerOutputPolicy.RequireSealedOutput(
            signerAssemblyPath,
            signerAssemblySha256,
            signerRuntimeConfigSha256,
            signerDepsSha256);
        RuntimeHostPolicy.RequireCurrentProcess(signerOutput.AssemblyPath);
        return new RuntimeHostConfiguration(
            dotnetRoot,
            dotnetPath,
            dotnetSha256,
            dotnetTreeSha256,
            signerOutput,
            signerOutputTreeSha256);
    }

    public async Task VerifyAsync(TimeSpan timeout)
    {
        RuntimeHostPolicy.RequireCurrentProcess(SignerOutput.AssemblyPath);
        RuntimeHostPolicy.RequireDotnetInstallation(
            DotnetRoot,
            DotnetPath,
            DotnetSha256);
        SignerOutputPolicy.RequireSealedOutput(
            SignerOutput.AssemblyPath,
            SignerOutput.AssemblySha256,
            SignerOutput.RuntimeConfigSha256,
            SignerOutput.DepsSha256);

        var dotnetTreeTask =
            RuntimeHostPolicy.ComputeCanonicalDotnetTreeSha256Async(
                DotnetRoot,
                timeout);
        var outputDirectory = SignerOutput.OutputDirectory;
        var outputTreeTask = CanonicalTreeHasher.ComputeSha256Async(
            Path.GetDirectoryName(outputDirectory)
                ?? throw new SigningFailureException(
                    "Linux KeyLocker signer output parent is absent."),
            Path.GetFileName(outputDirectory),
            timeout,
            "signer output");
        await Task.WhenAll(dotnetTreeTask, outputTreeTask);
        var actualDotnetTreeSha256 = await dotnetTreeTask;
        var actualOutputTreeSha256 = await outputTreeTask;
        RuntimeHostPolicy.RequireApprovedDotnetTreeSha256(
            actualDotnetTreeSha256);
        if (!Hashing.FixedHexEquals(
                actualDotnetTreeSha256,
                DotnetTreeSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker .NET tree changed after preflight.");
        }
        SignerOutputPolicy.RequireTreeSha256(
            SignerOutputTreeSha256,
            actualOutputTreeSha256);

        RuntimeHostPolicy.RequireCurrentProcess(SignerOutput.AssemblyPath);
        RuntimeHostPolicy.RequireDotnetInstallation(
            DotnetRoot,
            DotnetPath,
            DotnetSha256);
        SignerOutputPolicy.RequireSealedOutput(
            SignerOutput.AssemblyPath,
            SignerOutput.AssemblySha256,
            SignerOutput.RuntimeConfigSha256,
            SignerOutput.DepsSha256);
    }
}

internal static class SystemToolPolicy
{
    public static string RequireRootOwnedExecutable(
        string path,
        string label)
    {
        var resolved = GovernedPath.ResolveRegularFile(
            path,
            label,
            [],
            Path.GetFileName(path),
            executable: true);
        var identity = LinuxFileSystem.ReadIdentity(resolved);
        const ushort groupOrOtherWrite = 0x12;
        if (!identity.IsRegularFile
            || identity.UserId != 0
            || (identity.Mode & groupOrOtherWrite) != 0)
        {
            throw new SigningFailureException(
                $"{label} must be a root-owned non-writable regular file.");
        }
        return resolved;
    }
}

internal sealed class Redactor
{
    private static readonly Regex NamedSecret = new(
        @"(?im)\b(SM_[A-Z0-9_]+|"
        + @"CHUMMER_WINDOWS_JSIGN_DIGICERTONE_STOREPASS|"
        + @"CHUMMER_WINDOWS_SIGN_PFX_BASE64|"
        + @"CHUMMER_WINDOWS_SIGN_PFX_PASSWORD|"
        + @"CHUMMER_WINDOWS_SIGN_CERT_PASSWORD)\s*[:=]\s*[^\r\n\s]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex AuthorizationSecret = new(
        @"(?im)\b(authorization|api[-_ ]?key|"
        + @"client[-_ ]?certificate[-_ ]?password)\s*[:=]\s*"
        + @"(?:bearer\s+)?[^\r\n\s]+",
        RegexOptions.CultureInvariant);
    private readonly string[] _exactSecrets;

    public Redactor(IEnumerable<string> exactSecrets)
    {
        _exactSecrets = exactSecrets
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToArray();
    }

    public static Redactor ForTransientCredential(string credential)
    {
        return new Redactor(
            new[] { credential }.Concat(credential.Split('|')));
    }

    public string Sanitize(string value)
    {
        var sanitized = value ?? string.Empty;
        foreach (var secret in _exactSecrets)
        {
            sanitized = sanitized.Replace(
                secret,
                "[REDACTED]",
                StringComparison.Ordinal);
        }
        sanitized = NamedSecret.Replace(
            sanitized,
            "$1=[REDACTED]");
        return AuthorizationSecret.Replace(
            sanitized,
            "$1=[REDACTED]");
    }

    public static string StaticSanitize(string value)
    {
        var sanitized = NamedSecret.Replace(
            value ?? string.Empty,
            "$1=[REDACTED]");
        return AuthorizationSecret.Replace(
            sanitized,
            "$1=[REDACTED]");
    }
}

internal static class GovernedProcess
{
    private const int MaximumCapturedOutputBytes = 65_536;
    private const string SessionLauncherPath = "/usr/bin/setsid";
    private static readonly TimeSpan TerminationTimeout =
        TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PipeDrainTimeout =
        TimeSpan.FromSeconds(5);

    public static async Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string provider,
        string operation,
        string? artifactPath,
        IReadOnlyDictionary<string, string> childEnvironment,
        Redactor redactor,
        TimeSpan timeout,
        bool credentialed)
    {
        SystemToolPolicy.RequireRootOwnedExecutable(
            SessionLauncherPath,
            "Linux KeyLocker process-group launcher");
        var startInfo = new ProcessStartInfo
        {
            FileName = SessionLauncherPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(executable);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.Environment.Clear();
        foreach (var pair in childEnvironment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }
        startInfo.Environment.Remove("BASH_ENV");
        startInfo.Environment.Remove("ENV");

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new SigningFailureException(
                $"Fixed {provider} process did not start.");
        }

        using var ioCancellation = new CancellationTokenSource();
        var retainedBytesPerStream = credentialed
            ? 0
            : MaximumCapturedOutputBytes / 2;
        var stdoutTask = ReadBoundedAsync(
            process.StandardOutput.BaseStream,
            retainedBytesPerStream,
            ioCancellation.Token);
        var stderrTask = ReadBoundedAsync(
            process.StandardError.BaseStream,
            retainedBytesPerStream,
            ioCancellation.Token);
        var timedOut = false;
        var processExited = false;
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            processExited = true;
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            try
            {
                LinuxProcessGroup.Kill(process.Id);
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may have exited at the timeout boundary.
            }
            using var termination =
                new CancellationTokenSource(TerminationTimeout);
            try
            {
                await process.WaitForExitAsync(termination.Token);
                processExited = true;
            }
            catch (OperationCanceledException)
            {
                // Fail closed without waiting forever on an unkillable child.
            }
        }

        var pipeDrainTimedOut = false;
        BoundedBytes stdout;
        BoundedBytes stderr;
        try
        {
            var output = await Task.WhenAll(stdoutTask, stderrTask)
                .WaitAsync(PipeDrainTimeout);
            stdout = output[0];
            stderr = output[1];
        }
        catch (TimeoutException)
        {
            pipeDrainTimedOut = true;
            LinuxProcessGroup.Kill(process.Id);
            ioCancellation.Cancel();
            process.StandardOutput.Dispose();
            process.StandardError.Dispose();
            try
            {
                var output = await Task.WhenAll(stdoutTask, stderrTask)
                    .WaitAsync(TimeSpan.FromSeconds(2));
                stdout = output[0];
                stderr = output[1];
            }
            catch
            {
                stdout = new BoundedBytes([], true);
                stderr = new BoundedBytes([], true);
            }
        }
        var sanitized = credentialed
            ? string.Empty
            : redactor.Sanitize(
                Encoding.UTF8.GetString(stdout.Retained)
                + "\n"
                + Encoding.UTF8.GetString(stderr.Retained))
                .TrimEnd('\r', '\n');
        var exitCode = timedOut || !processExited
            ? -2
            : process.ExitCode;
        var outputSha = credentialed
            ? null
            : Hashing.BytesSha256(Encoding.UTF8.GetBytes(sanitized));
        var artifactSha = artifactPath is not null
            && File.Exists(artifactPath)
            ? Hashing.FileSha256(artifactPath)
            : null;
        var artifactName = artifactPath is null
            ? null
            : Path.GetFileName(artifactPath);
        var truncated = stdout.Truncated || stderr.Truncated;
        var transactionMaterial = string.Join(
            '\n',
            operation,
            artifactName ?? string.Empty,
            exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            outputSha ?? "suppressed",
            credentialed
                ? "suppressed"
                : truncated ? "truncated" : "complete");

        return new ProcessResult(
            new ProcessTransaction(
                provider,
                operation,
                artifactName,
                artifactSha,
                exitCode,
                outputSha,
                credentialed
                    ? null
                    : Encoding.UTF8.GetByteCount(sanitized),
                credentialed
                    ? null
                    : truncated ? MaximumCapturedOutputBytes : null,
                Hashing.BytesSha256(
                    Encoding.UTF8.GetBytes(transactionMaterial)),
                timedOut,
                pipeDrainTimedOut,
                credentialed
                    ? "suppressed_unrecorded"
                    : "bounded_hash_only",
                credentialed
                    ? "credentialed_output_never_retained_or_hashed"
                    : "exact_values_assignments_and_authorization_fields_v2"),
            sanitized);
    }

    private static async Task<BoundedBytes> ReadBoundedAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var retained = new MemoryStream(maximumBytes);
        var buffer = new byte[4096];
        var total = 0L;
        var cancelled = false;
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }
                total += read;
                var remaining = maximumBytes - (int)retained.Length;
                if (remaining > 0)
                {
                    retained.Write(buffer, 0, Math.Min(remaining, read));
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (ObjectDisposedException)
        {
            cancelled = true;
        }
        return new BoundedBytes(
            retained.ToArray(),
            total > maximumBytes || cancelled);
    }

    private sealed record BoundedBytes(byte[] Retained, bool Truncated);
}

internal static class LinuxProcessGroup
{
    private const int SignalKill = 9;

    public static void Kill(int processGroupId)
    {
        if (processGroupId <= 1)
        {
            return;
        }
        NativeMethods.Kill(-processGroupId, SignalKill);
    }

    private static class NativeMethods
    {
        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        internal static extern int Kill(int processId, int signal);
    }
}

internal sealed record ProcessResult(
    ProcessTransaction Transaction,
    string SanitizedOutput);

internal sealed record ProcessTransaction(
    string Provider,
    string Operation,
    string? ArtifactFileName,
    string? ArtifactSha256AfterOperation,
    int ExitCode,
    string? SanitizedOutputSha256,
    int? SanitizedOutputLengthBytes,
    int? OutputTruncatedAtBytes,
    string TransactionSha256,
    bool TimedOut,
    bool PipeDrainTimedOut,
    string OutputRetention,
    string RedactionPolicy);

internal static class AuthenticodeVerifier
{
    public const string CodeSigningEkuOid = "1.3.6.1.5.5.7.3.3";
    public const string TimestampingEkuOid = "1.3.6.1.5.5.7.3.8";
    private const string Rfc3161AttributeOid =
        "1.3.6.1.4.1.311.3.3.1";
    private const string GenericRfc3161AttributeOid =
        "1.2.840.113549.1.9.16.2.14";
    private const string CmsCounterSignatureAttributeOid =
        "1.2.840.113549.1.9.6";
    private const string Rfc3161ContentTypeOid =
        "1.2.840.113549.1.9.16.1.4";
    private const string AuthenticodeContentTypeOid =
        "1.3.6.1.4.1.311.2.1.4";
    private const string SpcPeImageDataOid =
        "1.3.6.1.4.1.311.2.1.15";
    private const string SpcStatementTypeAttributeOid =
        "1.3.6.1.4.1.311.2.1.11";
    private const string SpcIndividualCodeSigningOid =
        "1.3.6.1.4.1.311.2.1.21";
    private const string CmsContentTypeAttributeOid =
        "1.2.840.113549.1.9.3";
    private const string CmsMessageDigestAttributeOid =
        "1.2.840.113549.1.9.4";
    private const string NestedSignatureOid =
        "1.3.6.1.4.1.311.2.4.1";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    public static void RequireUnsignedPe(string path)
    {
        var layout = PeAuthenticodeLayout.Read(path, requireSignature: false);
        if (layout.CertificateOffset != 0 || layout.CertificateSize != 0)
        {
            throw new SigningFailureException(
                "Linux KeyLocker refuses to add or replace a pre-existing "
                + "Authenticode signature.");
        }
    }

    public static ArtifactSignatureEvidence Verify(
        string path,
        X509Certificate2 expectedSigner,
        X509Certificate2Collection? customTrustRoots,
        bool onlineRevocation)
    {
        try
        {
            return VerifyCore(
                path,
                expectedSigner,
                customTrustRoots,
                onlineRevocation);
        }
        catch (SigningFailureException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is CryptographicException
                or AsnContentException
                or IOException
                or UnauthorizedAccessException
                or ArgumentException
                or OverflowException)
        {
            throw new SigningFailureException(
                "Independent Authenticode/RFC3161 cryptographic or structural "
                + "verification failed.");
        }
    }

    private static ArtifactSignatureEvidence VerifyCore(
        string path,
        X509Certificate2 expectedSigner,
        X509Certificate2Collection? customTrustRoots,
        bool onlineRevocation)
    {
        var layout = PeAuthenticodeLayout.Read(path, requireSignature: true);
        var cms = new SignedCms();
        cms.Decode(layout.CmsBytes);
        if (cms.ContentInfo.ContentType.Value != AuthenticodeContentTypeOid
            || cms.SignerInfos.Count != 1)
        {
            throw new SigningFailureException(
                "Authenticode PKCS#7 must contain the expected content type "
                + "and exactly one signer.");
        }
        cms.CheckSignature(verifySignatureOnly: true);
        var signerInfo = cms.SignerInfos[0];
        if (signerInfo.DigestAlgorithm.Value != Sha256Oid
            || signerInfo.Certificate is null)
        {
            throw new SigningFailureException(
                "Authenticode signer must use SHA-256 and include its "
                + "certificate.");
        }
        RequirePrimarySignedAttributes(
            signerInfo,
            cms.ContentInfo.Content);

        var indirectDigest = ParseAuthenticodeContentDigest(
            cms.ContentInfo.Content);
        if (indirectDigest.AlgorithmOid != Sha256Oid)
        {
            throw new SigningFailureException(
                "Authenticode indirect-data digest must use SHA-256.");
        }
        var computedImageDigest =
            layout.ComputeAuthenticodeImageSha256();
        if (!Hashing.FixedHexEquals(
                indirectDigest.DigestHex,
                computedImageDigest))
        {
            throw new SigningFailureException(
                "Authenticode SHA-256 digest does not match the signed PE "
                + "image.");
        }

        using var signerCertificate =
            X509CertificateLoader.LoadCertificate(
                signerInfo.Certificate.RawData);
        CertificatePolicy.RequireEku(
            signerCertificate,
            CodeSigningEkuOid,
            "Authenticode signer certificate");
        CertificatePolicy.RequireLeafSigningPosture(
            signerCertificate,
            dedicatedTimestampAuthority: false,
            "Authenticode signer certificate");
        var signerEvidence = CertificateEvidence.From(signerCertificate);
        var expectedEvidence = CertificateEvidence.From(expectedSigner);
        if (!Hashing.FixedHexEquals(
                signerEvidence.CertificateSha256,
                expectedEvidence.CertificateSha256)
            || !Hashing.FixedHexEquals(
                signerEvidence.SpkiSha256,
                expectedEvidence.SpkiSha256))
        {
            throw new SigningFailureException(
                "The signed artifact identity differs from the configured "
                + "KeyLocker public certificate.");
        }

        var timestamp = ParseRequiredRfc3161(signerInfo);
        using var timestampCertificate = X509CertificateLoader.LoadCertificate(
            timestamp.Signer.Certificate?.RawData
            ?? throw new SigningFailureException(
                "RFC3161 timestamp signer certificate is absent."));
        CertificatePolicy.RequireEku(
            timestampCertificate,
            TimestampingEkuOid,
            "RFC3161 timestamp certificate");
        CertificatePolicy.RequireLeafSigningPosture(
            timestampCertificate,
            dedicatedTimestampAuthority: true,
            "RFC3161 timestamp certificate");
        var now = DateTimeOffset.UtcNow;
        if (timestamp.GeneratedAt > now.AddMinutes(5))
        {
            throw new SigningFailureException(
                "RFC3161 timestamp is in the future.");
        }
        RequireWithinValidity(
            signerCertificate,
            timestamp.GeneratedAt,
            "Authenticode signer");
        RequireWithinValidity(
            timestampCertificate,
            timestamp.GeneratedAt,
            "RFC3161 timestamp signer");

        var signerChain = CertificatePolicy.BuildTrustedChain(
            signerCertificate,
            cms.Certificates,
            timestamp.GeneratedAt,
            CodeSigningEkuOid,
            "Authenticode signer",
            customTrustRoots,
            onlineRevocation);
        var timestampChain = CertificatePolicy.BuildTrustedChain(
            timestampCertificate,
            timestamp.Cms.Certificates,
            timestamp.GeneratedAt,
            TimestampingEkuOid,
            "RFC3161 timestamp signer",
            customTrustRoots,
            onlineRevocation);

        return new ArtifactSignatureEvidence(
            Path.GetFileName(path),
            layout.ArtifactSha256,
            computedImageDigest,
            "sha256",
            "passed",
            signerEvidence,
            signerChain,
            new TimestampEvidence(
                "verified",
                "rfc3161",
                "sha256",
                timestamp.GeneratedAt.UtcDateTime.ToString(
                    "yyyy-MM-ddTHH:mm:ss.fffffffZ",
                    System.Globalization.CultureInfo.InvariantCulture),
                timestamp.MessageImprintSha256,
                Hashing.BytesSha256(timestampCertificate.RawData),
                timestampCertificate.Subject,
                timestampCertificate.Issuer,
                timestampCertificate.SerialNumber,
                timestampChain),
            new VerifierEvidence(
                "scripts/Chummer.KeyLockerSigner/Program.cs/"
                + "pe_cms_rfc3161_v2",
                true,
                false));
    }

    private static AuthenticodeContentDigest ParseAuthenticodeContentDigest(
        byte[] content)
    {
        var reader = new AsnReader(content, AsnEncodingRules.BER);
        var sequence = reader.ReadSequence();
        var data = sequence.ReadSequence();
        if (data.ReadObjectIdentifier() != SpcPeImageDataOid
            || !data.HasData)
        {
            throw new SigningFailureException(
                "Authenticode indirect data must identify one PE image.");
        }
        var peImageData = data.ReadSequence();
        if (HasUniversalTag(peImageData, UniversalTagNumber.BitString))
        {
            var flags = peImageData.ReadBitString(out var unusedBits);
            if (unusedBits != 0 || flags.Any(value => value != 0))
            {
                throw new SigningFailureException(
                    "Authenticode PE image flags must be empty.");
            }
        }
        if (peImageData.HasData)
        {
            var file = peImageData.ReadSequence(ContextTag(0, constructed: true));
            if (!file.HasData
                || file.PeekTag().TagClass != TagClass.ContextSpecific
                || file.PeekTag().TagValue != 2
                || !file.PeekTag().IsConstructed)
            {
                throw new SigningFailureException(
                    "Authenticode PE image file link has an unsupported form.");
            }
            var linkReader = file.ReadSequence(
                ContextTag(2, constructed: true));
            if (!linkReader.HasData
                || linkReader.PeekTag().TagClass != TagClass.ContextSpecific
                || linkReader.PeekTag().TagValue is < 0 or > 1)
            {
                throw new SigningFailureException(
                    "Authenticode PE image file string has an unsupported "
                    + "form.");
            }
            linkReader.ReadEncodedValue();
            if (linkReader.HasData || file.HasData)
            {
                throw new SigningFailureException(
                    "Authenticode PE image file link has trailing fields.");
            }
        }
        if (peImageData.HasData || data.HasData)
        {
            throw new SigningFailureException(
                "Authenticode PE image data contains trailing fields.");
        }
        var digestInfo = sequence.ReadSequence();
        var algorithm = digestInfo.ReadSequence();
        var algorithmOid = algorithm.ReadObjectIdentifier();
        if (!HasUniversalTag(algorithm, UniversalTagNumber.Null))
        {
            throw new SigningFailureException(
                "Authenticode SHA-256 algorithm identifier must contain "
                + "one NULL parameter.");
        }
        algorithm.ReadNull();
        if (algorithm.HasData)
        {
            throw new SigningFailureException(
                "Authenticode digest algorithm identifier is malformed.");
        }
        var digest = digestInfo.ReadOctetString();
        if (digest.Length != SHA256.HashSizeInBytes
            || digestInfo.HasData
            || sequence.HasData
            || reader.HasData)
        {
            throw new SigningFailureException(
                "Authenticode indirect-data content contains trailing fields.");
        }
        return new AuthenticodeContentDigest(
            algorithmOid,
            Convert.ToHexString(digest).ToLowerInvariant());
    }

    private static void RequirePrimarySignedAttributes(
        SignerInfo signer,
        byte[] content)
    {
        var attributes = signer.SignedAttributes
            .Cast<CryptographicAttributeObject>()
            .ToArray();
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            CmsContentTypeAttributeOid,
            CmsMessageDigestAttributeOid,
            SpcStatementTypeAttributeOid,
        };
        if (attributes.Length != allowed.Count
            || attributes.Any(
                attribute => attribute.Oid.Value is null
                    || !allowed.Contains(attribute.Oid.Value))
            || attributes.Select(attribute => attribute.Oid.Value)
                .Distinct(StringComparer.Ordinal)
                .Count() != allowed.Count)
        {
            throw new SigningFailureException(
                "Authenticode primary signer attributes differ from the "
                + "fixed Jsign PE-signing profile.");
        }

        RequireCmsContentBinding(
            signer,
            content,
            AuthenticodeContentTypeOid,
            hashAsn1ContentOctets: true);
        var statement = attributes.Single(
            attribute =>
                attribute.Oid.Value == SpcStatementTypeAttributeOid);
        if (statement.Values.Count != 1)
        {
            throw new SigningFailureException(
                "Authenticode statement type must contain one value.");
        }
        var statementReader = new AsnReader(
            statement.Values[0].RawData,
            AsnEncodingRules.DER);
        var statementSequence = statementReader.ReadSequence();
        if (statementSequence.ReadObjectIdentifier()
                != SpcIndividualCodeSigningOid
            || statementSequence.HasData
            || statementReader.HasData)
        {
            throw new SigningFailureException(
                "Authenticode statement type is not the fixed individual "
                + "code-signing profile.");
        }
    }

    private static void RequireCmsContentBinding(
        SignerInfo signer,
        byte[] content,
        string expectedContentTypeOid,
        bool hashAsn1ContentOctets)
    {
        var attributes = signer.SignedAttributes
            .Cast<CryptographicAttributeObject>()
            .ToArray();
        var contentTypeAttributes = attributes
            .Where(
                attribute =>
                    attribute.Oid.Value == CmsContentTypeAttributeOid)
            .ToArray();
        var messageDigestAttributes = attributes
            .Where(
                attribute =>
                    attribute.Oid.Value == CmsMessageDigestAttributeOid)
            .ToArray();
        if (contentTypeAttributes.Length != 1
            || contentTypeAttributes[0].Values.Count != 1
            || messageDigestAttributes.Length != 1
            || messageDigestAttributes[0].Values.Count != 1)
        {
            throw new SigningFailureException(
                "CMS signer must contain exactly one content-type and one "
                + "message-digest attribute.");
        }

        var contentTypeReader = new AsnReader(
            contentTypeAttributes[0].Values[0].RawData,
            AsnEncodingRules.DER);
        if (contentTypeReader.ReadObjectIdentifier()
                != expectedContentTypeOid
            || contentTypeReader.HasData)
        {
            throw new SigningFailureException(
                "CMS signed content-type attribute does not match the "
                + "encapsulated content.");
        }

        var digestReader = new AsnReader(
            messageDigestAttributes[0].Values[0].RawData,
            AsnEncodingRules.DER);
        var digest = digestReader.ReadOctetString();
        var digestInput = hashAsn1ContentOctets
            ? ExtractDefiniteAsn1ContentOctets(content)
            : content;
        var expectedDigest = SHA256.HashData(digestInput);
        if (digestReader.HasData
            || !CryptographicOperations.FixedTimeEquals(
                digest,
                expectedDigest))
        {
            throw new SigningFailureException(
                "CMS signed message-digest attribute does not bind the "
                + "encapsulated content.");
        }
    }

    private static ReadOnlySpan<byte> ExtractDefiniteAsn1ContentOctets(
        byte[] encoded)
    {
        if (encoded.Length < 2)
        {
            throw new SigningFailureException(
                "Authenticode encapsulated ASN.1 content is truncated.");
        }
        var firstLengthByte = encoded[1];
        var headerLength = 2;
        var contentLength = 0;
        if ((firstLengthByte & 0x80) == 0)
        {
            contentLength = firstLengthByte;
        }
        else
        {
            var lengthByteCount = firstLengthByte & 0x7f;
            if (lengthByteCount is 0 or > 4
                || encoded.Length < 2 + lengthByteCount
                || encoded[2] == 0)
            {
                throw new SigningFailureException(
                    "Authenticode encapsulated ASN.1 content length is not "
                    + "one canonical definite length.");
            }
            headerLength += lengthByteCount;
            for (var index = 0; index < lengthByteCount; index++)
            {
                contentLength = checked(
                    (contentLength << 8) | encoded[2 + index]);
            }
            if (contentLength < 128)
            {
                throw new SigningFailureException(
                    "Authenticode encapsulated ASN.1 content uses a "
                    + "non-minimal length.");
            }
        }
        if (headerLength + contentLength != encoded.Length)
        {
            throw new SigningFailureException(
                "Authenticode encapsulated ASN.1 content length is "
                + "inconsistent.");
        }
        return encoded.AsSpan(headerLength, contentLength);
    }

    private static Rfc3161Timestamp ParseRequiredRfc3161(
        SignerInfo authenticodeSigner)
    {
        var attributes = authenticodeSigner.UnsignedAttributes
            .Cast<CryptographicAttributeObject>()
            .ToArray();
        if (attributes.Length != 1
            || attributes[0].Oid.Value != Rfc3161AttributeOid
            || attributes[0].Values.Count != 1)
        {
            var conflictingTimestamp = attributes.Any(
                attribute => attribute.Oid.Value
                    is GenericRfc3161AttributeOid
                    or CmsCounterSignatureAttributeOid
                    or NestedSignatureOid);
            throw new SigningFailureException(
                conflictingTimestamp
                    ? "Authenticode signer contains an unsupported, legacy, "
                        + "nested, or additional timestamp/signature attribute."
                    : "Authenticode signer must contain exactly one Microsoft "
                        + "Authenticode RFC3161 timestamp token.");
        }

        var timestampCms = new SignedCms();
        timestampCms.Decode(attributes[0].Values[0].RawData);
        if (timestampCms.ContentInfo.ContentType.Value
                != Rfc3161ContentTypeOid
            || timestampCms.SignerInfos.Count != 1)
        {
            throw new SigningFailureException(
                "RFC3161 timestamp token has an invalid content type or "
                + "signer count.");
        }
        timestampCms.CheckSignature(verifySignatureOnly: true);
        var timestampSigner = timestampCms.SignerInfos[0];
        if (timestampSigner.DigestAlgorithm.Value != Sha256Oid)
        {
            throw new SigningFailureException(
                "RFC3161 token signer must use SHA-256.");
        }
        if (timestampSigner.UnsignedAttributes.Count != 0)
        {
            throw new SigningFailureException(
                "RFC3161 token signer must not contain nested unsigned "
                + "attributes.");
        }
        RequireCmsContentBinding(
            timestampSigner,
            timestampCms.ContentInfo.Content,
            Rfc3161ContentTypeOid,
            hashAsn1ContentOctets: false);

        var reader = new AsnReader(
            timestampCms.ContentInfo.Content,
            AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        if (sequence.ReadInteger() != 1)
        {
            throw new SigningFailureException(
                "RFC3161 TSTInfo version must be 1.");
        }
        var policyOid = sequence.ReadObjectIdentifier();
        if (string.IsNullOrWhiteSpace(policyOid))
        {
            throw new SigningFailureException(
                "RFC3161 TSTInfo policy OID is absent.");
        }
        var messageImprint = sequence.ReadSequence();
        var algorithm = messageImprint.ReadSequence();
        var algorithmOid = algorithm.ReadObjectIdentifier();
        if (algorithm.HasData)
        {
            if (!HasUniversalTag(algorithm, UniversalTagNumber.Null))
            {
                throw new SigningFailureException(
                    "RFC3161 SHA-256 algorithm identifier has an unsupported "
                    + "parameter.");
            }
            algorithm.ReadNull();
        }
        if (algorithm.HasData)
        {
            throw new SigningFailureException(
                "RFC3161 message-imprint algorithm has trailing fields.");
        }
        var imprint = messageImprint.ReadOctetString();
        if (messageImprint.HasData)
        {
            throw new SigningFailureException(
                "RFC3161 message imprint has trailing fields.");
        }
        if (sequence.ReadInteger() <= 0)
        {
            throw new SigningFailureException(
                "RFC3161 TSTInfo serial number must be positive.");
        }
        var generatedAt = sequence.ReadGeneralizedTime();
        if (algorithmOid != Sha256Oid)
        {
            throw new SigningFailureException(
                "RFC3161 message imprint must use SHA-256.");
        }
        if (imprint.Length != SHA256.HashSizeInBytes)
        {
            throw new SigningFailureException(
                "RFC3161 SHA-256 message imprint has an invalid length.");
        }
        ParseOptionalTstInfoFields(sequence);
        var expectedImprint = SHA256.HashData(
            authenticodeSigner.GetSignature());
        if (!CryptographicOperations.FixedTimeEquals(
                imprint,
                expectedImprint))
        {
            throw new SigningFailureException(
                "RFC3161 message imprint does not bind the Authenticode "
                + "signer signature.");
        }
        if (sequence.HasData || reader.HasData)
        {
            throw new SigningFailureException(
                "RFC3161 timestamp content contains trailing data.");
        }
        return new Rfc3161Timestamp(
            timestampCms,
            timestampSigner,
            generatedAt,
            Convert.ToHexString(imprint).ToLowerInvariant());
    }

    private static void ParseOptionalTstInfoFields(AsnReader sequence)
    {
        if (HasUniversalTag(sequence, UniversalTagNumber.Sequence))
        {
            var accuracy = sequence.ReadSequence();
            if (HasUniversalTag(accuracy, UniversalTagNumber.Integer)
                && accuracy.ReadInteger() <= 0)
            {
                throw new SigningFailureException(
                    "RFC3161 accuracy seconds must be positive.");
            }
            if (HasContextTag(accuracy, 0))
            {
                var milliseconds =
                    accuracy.ReadInteger(ContextTag(0, constructed: false));
                if (milliseconds < 1 || milliseconds > 999)
                {
                    throw new SigningFailureException(
                        "RFC3161 accuracy milliseconds are out of range.");
                }
            }
            if (HasContextTag(accuracy, 1))
            {
                var microseconds =
                    accuracy.ReadInteger(ContextTag(1, constructed: false));
                if (microseconds < 1 || microseconds > 999)
                {
                    throw new SigningFailureException(
                        "RFC3161 accuracy microseconds are out of range.");
                }
            }
            if (accuracy.HasData)
            {
                throw new SigningFailureException(
                    "RFC3161 accuracy contains trailing or reordered fields.");
            }
        }
        if (HasUniversalTag(sequence, UniversalTagNumber.Boolean)
            && !sequence.ReadBoolean())
        {
            throw new SigningFailureException(
                "RFC3161 default ordering=false must be omitted in DER.");
        }
        if (HasUniversalTag(sequence, UniversalTagNumber.Integer)
            && sequence.ReadInteger() < 0)
        {
            throw new SigningFailureException(
                "RFC3161 nonce must not be negative.");
        }
        if (HasContextTag(sequence, 0))
        {
            var tsa = sequence.ReadSequence(ContextTag(0, constructed: true));
            if (!tsa.HasData
                || tsa.PeekTag().TagClass != TagClass.ContextSpecific
                || tsa.PeekTag().TagValue is < 0 or > 8)
            {
                throw new SigningFailureException(
                    "RFC3161 TSA GeneralName is malformed.");
            }
            tsa.ReadEncodedValue();
            if (tsa.HasData)
            {
                throw new SigningFailureException(
                    "RFC3161 TSA field contains multiple GeneralNames.");
            }
        }
        if (HasContextTag(sequence, 1))
        {
            var extensions = sequence.ReadSequence(
                ContextTag(1, constructed: true));
            var count = 0;
            while (extensions.HasData)
            {
                count++;
                var extension = extensions.ReadSequence();
                extension.ReadObjectIdentifier();
                if (HasUniversalTag(
                        extension,
                        UniversalTagNumber.Boolean))
                {
                    extension.ReadBoolean();
                }
                extension.ReadOctetString();
                if (extension.HasData)
                {
                    throw new SigningFailureException(
                        "RFC3161 extension contains trailing fields.");
                }
            }
            if (count == 0)
            {
                throw new SigningFailureException(
                    "RFC3161 extensions field must not be empty.");
            }
        }
    }

    private static bool HasUniversalTag(
        AsnReader reader,
        UniversalTagNumber tag)
    {
        return reader.HasData
            && reader.PeekTag().TagClass == TagClass.Universal
            && reader.PeekTag().TagValue == (int)tag;
    }

    private static bool HasContextTag(AsnReader reader, int tag)
    {
        return reader.HasData
            && reader.PeekTag().TagClass == TagClass.ContextSpecific
            && reader.PeekTag().TagValue == tag;
    }

    private static Asn1Tag ContextTag(int tag, bool constructed)
    {
        return new Asn1Tag(
            TagClass.ContextSpecific,
            tag,
            constructed);
    }

    private static void RequireWithinValidity(
        X509Certificate2 certificate,
        DateTimeOffset timestamp,
        string label)
    {
        if (timestamp < certificate.NotBefore.ToUniversalTime()
            || timestamp > certificate.NotAfter.ToUniversalTime())
        {
            throw new SigningFailureException(
                $"RFC3161 timestamp is outside the {label} certificate "
                + "validity interval.");
        }
    }

    private sealed record AuthenticodeContentDigest(
        string AlgorithmOid,
        string DigestHex);

    private sealed record Rfc3161Timestamp(
        SignedCms Cms,
        SignerInfo Signer,
        DateTimeOffset GeneratedAt,
        string MessageImprintSha256);
}

internal sealed class PeAuthenticodeLayout
{
    private PeAuthenticodeLayout(
        byte[] bytes,
        int checksumOffset,
        int securityEntryOffset,
        int certificateOffset,
        int certificateSize,
        byte[] cmsBytes)
    {
        Bytes = bytes;
        ChecksumOffset = checksumOffset;
        SecurityEntryOffset = securityEntryOffset;
        CertificateOffset = certificateOffset;
        CertificateSize = certificateSize;
        CmsBytes = cmsBytes;
        ArtifactSha256 = Hashing.BytesSha256(bytes);
    }

    public byte[] Bytes { get; }
    public int ChecksumOffset { get; }
    public int SecurityEntryOffset { get; }
    public int CertificateOffset { get; }
    public int CertificateSize { get; }
    public byte[] CmsBytes { get; }
    public string ArtifactSha256 { get; }

    public static PeAuthenticodeLayout Read(
        string path,
        bool requireSignature)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 256 || bytes[0] != 0x4d || bytes[1] != 0x5a)
        {
            throw new SigningFailureException(
                "Linux KeyLocker accepts only structurally valid PE images.");
        }
        var peOffset = ReadInt32(bytes, 0x3c);
        if (peOffset < 0x40 || peOffset > bytes.Length - 24)
        {
            throw new SigningFailureException(
                "PE header offset is outside the artifact.");
        }
        if (bytes[peOffset] != 0x50
            || bytes[peOffset + 1] != 0x45
            || bytes[peOffset + 2] != 0
            || bytes[peOffset + 3] != 0)
        {
            throw new SigningFailureException("PE signature is invalid.");
        }

        var numberOfSections = ReadUInt16(bytes, peOffset + 6);
        if (numberOfSections is 0 or > 96)
        {
            throw new SigningFailureException(
                "PE section count is outside the governed range.");
        }
        var optionalOffset = checked(peOffset + 24);
        var optionalSize = ReadUInt16(bytes, peOffset + 20);
        if (optionalOffset > bytes.Length - optionalSize)
        {
            throw new SigningFailureException(
                "PE optional header is truncated.");
        }
        var magic = ReadUInt16(bytes, optionalOffset);
        var (numberOfRvaAndSizesOffset, dataDirectoryOffset) = magic switch
        {
            0x10b => (
                checked(optionalOffset + 92),
                checked(optionalOffset + 96)),
            0x20b => (
                checked(optionalOffset + 108),
                checked(optionalOffset + 112)),
            _ => throw new SigningFailureException(
                "PE optional-header magic is unsupported."),
        };
        var checksumOffset = checked(optionalOffset + 64);
        var securityEntryOffset = checked(dataDirectoryOffset + (4 * 8));
        var optionalEnd = checked(optionalOffset + optionalSize);
        if (checksumOffset > optionalEnd - 4
            || numberOfRvaAndSizesOffset > optionalEnd - 4
            || securityEntryOffset > optionalEnd - 8
            || securityEntryOffset > bytes.Length - 8)
        {
            throw new SigningFailureException(
                "PE Authenticode header fields are absent.");
        }
        if (ReadUInt32(bytes, numberOfRvaAndSizesOffset) < 5)
        {
            throw new SigningFailureException(
                "PE optional header exposes fewer than five data "
                + "directories.");
        }

        var sectionTableOffset = optionalEnd;
        var sectionTableSize = checked(numberOfSections * 40);
        var sectionTableEnd = checked(sectionTableOffset + sectionTableSize);
        var sizeOfHeaders = ReadUInt32(bytes, optionalOffset + 60);
        if (sectionTableEnd > bytes.Length
            || sizeOfHeaders < sectionTableEnd
            || sizeOfHeaders > bytes.Length)
        {
            throw new SigningFailureException(
                "PE section table or SizeOfHeaders is out of bounds.");
        }
        var sectionRanges = new List<(ulong Start, ulong End)>();
        for (var index = 0; index < numberOfSections; index++)
        {
            var sectionOffset = checked(sectionTableOffset + (index * 40));
            var sizeOfRawData = ReadUInt32(bytes, sectionOffset + 16);
            var pointerToRawData = ReadUInt32(bytes, sectionOffset + 20);
            if (sizeOfRawData == 0)
            {
                continue;
            }
            var rawEnd = (ulong)pointerToRawData + sizeOfRawData;
            if (pointerToRawData < sizeOfHeaders
                || rawEnd > (ulong)bytes.Length)
            {
                throw new SigningFailureException(
                    "PE loaded section raw-data range is out of bounds or "
                    + "overlaps the headers.");
            }
            sectionRanges.Add((pointerToRawData, rawEnd));
        }
        var orderedSections = sectionRanges
            .OrderBy(range => range.Start)
            .ToArray();
        for (var index = 1; index < orderedSections.Length; index++)
        {
            if (orderedSections[index].Start
                < orderedSections[index - 1].End)
            {
                throw new SigningFailureException(
                    "PE loaded section raw-data ranges overlap.");
            }
        }

        var certificateOffsetRaw =
            ReadUInt32(bytes, securityEntryOffset);
        var certificateSizeRaw =
            ReadUInt32(bytes, securityEntryOffset + 4);
        if ((certificateOffsetRaw == 0) != (certificateSizeRaw == 0))
        {
            throw new SigningFailureException(
                "PE certificate-table address and size must both be zero or "
                + "both be present.");
        }
        if (!requireSignature)
        {
            return new PeAuthenticodeLayout(
                bytes,
                checksumOffset,
                securityEntryOffset,
                checked((int)certificateOffsetRaw),
                checked((int)certificateSizeRaw),
                []);
        }
        if (certificateOffsetRaw == 0
            || certificateSizeRaw < 16
            || certificateOffsetRaw % 8 != 0
            || (ulong)certificateOffsetRaw + certificateSizeRaw
                != (ulong)bytes.Length
            || certificateOffsetRaw < sizeOfHeaders
            || sectionRanges.Any(range =>
                range.Start < (ulong)certificateOffsetRaw + certificateSizeRaw
                && range.End > certificateOffsetRaw))
        {
            throw new SigningFailureException(
                "PE Authenticode certificate table is absent, misaligned, "
                + "truncated, non-terminal, or overlaps mapped PE content.");
        }

        var certificateOffset = checked((int)certificateOffsetRaw);
        var certificateSize = checked((int)certificateSizeRaw);
        var declaredSize = ReadUInt32(bytes, certificateOffset);
        var revision = ReadUInt16(bytes, certificateOffset + 4);
        var certificateType = ReadUInt16(bytes, certificateOffset + 6);
        var availableCmsAndPadding = checked(certificateSize - 8);
        var cmsLength = ReadDerSequenceLength(
            bytes.AsSpan(
                certificateOffset + 8,
                availableCmsAndPadding));
        var unpaddedCertificateSize = checked(cmsLength + 8);
        var alignedCertificateSize =
            checked((unpaddedCertificateSize + 7) & ~7);
        if (declaredSize != unpaddedCertificateSize
                && declaredSize != certificateSizeRaw
            || alignedCertificateSize != certificateSize
            || revision != 0x0200
            || certificateType != 0x0002)
        {
            throw new SigningFailureException(
                "PE must contain exactly one aligned PKCS#7 Authenticode "
                + "WIN_CERTIFICATE.");
        }
        var cmsBytes = bytes.AsSpan(certificateOffset + 8, cmsLength).ToArray();
        var paddingStart = certificateOffset + unpaddedCertificateSize;
        if (bytes.AsSpan(paddingStart, bytes.Length - paddingStart)
            .ContainsAnyExcept((byte)0))
        {
            throw new SigningFailureException(
                "PE Authenticode certificate-table alignment padding must be "
                + "zero.");
        }
        return new PeAuthenticodeLayout(
            bytes,
            checksumOffset,
            securityEntryOffset,
            certificateOffset,
            certificateSize,
            cmsBytes);
    }

    private static int ReadDerSequenceLength(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2 || bytes[0] != 0x30)
        {
            throw new SigningFailureException(
                "PE Authenticode PKCS#7 is not one DER SEQUENCE.");
        }
        var headerLength = 2;
        var contentLength = 0;
        if ((bytes[1] & 0x80) == 0)
        {
            contentLength = bytes[1];
        }
        else
        {
            var lengthByteCount = bytes[1] & 0x7f;
            if (lengthByteCount is 0 or > 4
                || bytes.Length < 2 + lengthByteCount
                || bytes[2] == 0)
            {
                throw new SigningFailureException(
                    "PE Authenticode PKCS#7 has a non-canonical or "
                    + "indefinite length.");
            }
            headerLength += lengthByteCount;
            for (var index = 0; index < lengthByteCount; index++)
            {
                contentLength = checked(
                    (contentLength << 8) | bytes[2 + index]);
            }
            if (contentLength < 128)
            {
                throw new SigningFailureException(
                    "PE Authenticode PKCS#7 has a non-minimal length.");
            }
        }
        var encodedLength = checked(headerLength + contentLength);
        if (encodedLength > bytes.Length)
        {
            throw new SigningFailureException(
                "PE Authenticode PKCS#7 length exceeds its certificate "
                + "record.");
        }
        return encodedLength;
    }

    public string ComputeAuthenticodeImageSha256()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Bytes, 0, ChecksumOffset);
        var afterChecksum = checked(ChecksumOffset + 4);
        hash.AppendData(
            Bytes,
            afterChecksum,
            checked(SecurityEntryOffset - afterChecksum));
        var afterSecurityEntry = checked(SecurityEntryOffset + 8);
        hash.AppendData(
            Bytes,
            afterSecurityEntry,
            checked(CertificateOffset - afterSecurityEntry));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - sizeof(ushort))
        {
            throw new SigningFailureException("PE integer field is truncated.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - sizeof(uint))
        {
            throw new SigningFailureException("PE integer field is truncated.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(
            bytes.AsSpan(offset, sizeof(uint)));
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - sizeof(int))
        {
            throw new SigningFailureException("PE integer field is truncated.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(
            bytes.AsSpan(offset, sizeof(int)));
    }
}

internal static class CertificatePolicy
{
    public static void RequireEku(
        X509Certificate2 certificate,
        string requiredOid,
        string label)
    {
        var ekuExtensions = certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .ToArray();
        if (ekuExtensions.Length != 1
            || !ekuExtensions[0].EnhancedKeyUsages
                .Cast<Oid>()
                .Any(oid => oid.Value == requiredOid))
        {
            throw new SigningFailureException(
                $"{label} must contain exactly one EKU extension including "
                + $"{requiredOid}.");
        }
    }

    public static void RequireLeafSigningPosture(
        X509Certificate2 certificate,
        bool dedicatedTimestampAuthority,
        string label)
    {
        var basicConstraints = certificate.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .ToArray();
        if (basicConstraints.Length != 1
            || basicConstraints[0].CertificateAuthority)
        {
            throw new SigningFailureException(
                $"{label} must be an explicit non-CA leaf certificate.");
        }

        var keyUsage = certificate.Extensions
            .OfType<X509KeyUsageExtension>()
            .ToArray();
        if (keyUsage.Length != 1
            || (keyUsage[0].KeyUsages
                    & X509KeyUsageFlags.DigitalSignature) == 0
            || (keyUsage[0].KeyUsages
                    & (X509KeyUsageFlags.KeyCertSign
                        | X509KeyUsageFlags.CrlSign)) != 0)
        {
            throw new SigningFailureException(
                $"{label} must permit digital signatures and must not permit "
                + "CA certificate or CRL signing.");
        }

        if (dedicatedTimestampAuthority)
        {
            var eku = certificate.Extensions
                .OfType<X509EnhancedKeyUsageExtension>()
                .Single();
            if (!eku.Critical
                || eku.EnhancedKeyUsages.Count != 1
                || eku.EnhancedKeyUsages[0].Value
                    != AuthenticodeVerifier.TimestampingEkuOid)
            {
                throw new SigningFailureException(
                    $"{label} must have one critical, dedicated timestamping "
                    + "EKU as required by RFC3161.");
            }
        }
    }

    public static ChainEvidence BuildTrustedChain(
        X509Certificate2 certificate,
        X509Certificate2Collection extraCertificates,
        DateTimeOffset verificationTime,
        string applicationPolicyOid,
        string label,
        X509Certificate2Collection? customTrustRoots,
        bool onlineRevocation)
    {
        using var chain = new X509Chain();
        foreach (var extra in extraCertificates)
        {
            if (!Hashing.FixedHexEquals(
                    Hashing.BytesSha256(extra.RawData),
                    Hashing.BytesSha256(certificate.RawData)))
            {
                chain.ChainPolicy.ExtraStore.Add(extra);
            }
        }
        chain.ChainPolicy.ApplicationPolicy.Add(
            new Oid(applicationPolicyOid));
        chain.ChainPolicy.VerificationTime = verificationTime.UtcDateTime;
        chain.ChainPolicy.VerificationFlags =
            X509VerificationFlags.NoFlag;
        chain.ChainPolicy.RevocationFlag =
            X509RevocationFlag.EntireChain;
        chain.ChainPolicy.RevocationMode = onlineRevocation
            ? X509RevocationMode.Online
            : X509RevocationMode.NoCheck;
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(30);
        if (customTrustRoots is not null)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(customTrustRoots);
        }
        if (!chain.Build(certificate))
        {
            var status = string.Join(
                "; ",
                chain.ChainStatus.Select(item =>
                    $"{item.Status}:{item.StatusInformation.Trim()}"));
            throw new SigningFailureException(
                $"{label} certificate chain is not trusted: {status}");
        }
        if (chain.ChainElements.Count < 2)
        {
            throw new SigningFailureException(
                $"{label} certificate must terminate at a distinct external "
                + "trust anchor.");
        }
        var trustAnchor =
            chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
        var anchorBasicConstraints = trustAnchor.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .ToArray();
        if (anchorBasicConstraints.Length != 1
            || !anchorBasicConstraints[0].CertificateAuthority)
        {
            throw new SigningFailureException(
                $"{label} chain trust anchor must be an explicit CA "
                + "certificate.");
        }
        var trustAnchorSource = "system_trust_store";
        if (customTrustRoots is not null)
        {
            var anchorSha256 = Hashing.BytesSha256(trustAnchor.RawData);
            if (!customTrustRoots.Cast<X509Certificate2>().Any(
                    root => Hashing.FixedHexEquals(
                        anchorSha256,
                        Hashing.BytesSha256(root.RawData))))
            {
                throw new SigningFailureException(
                    $"{label} chain did not terminate at an externally "
                    + "provided test trust root.");
            }
            trustAnchorSource = "external_test_trust_store";
        }
        return new ChainEvidence(
            true,
            onlineRevocation ? "online" : "no_check_test_only",
            "entire_chain",
            "no_flag",
            verificationTime.UtcDateTime.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffffffZ",
                System.Globalization.CultureInfo.InvariantCulture),
            Hashing.BytesSha256(trustAnchor.RawData),
            trustAnchor.Subject,
            trustAnchorSource);
    }
}

internal static class Hashing
{
    public static string FileSha256(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string BytesSha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static bool FixedHexEquals(string left, string right)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(left),
                Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal sealed record CertificateEvidence(
    string CertificateSha256,
    string SpkiSha256,
    string ThumbprintSha1,
    string Subject,
    string Issuer,
    string SerialNumber,
    string NotBeforeUtc,
    string NotAfterUtc)
{
    public static CertificateEvidence From(X509Certificate2 certificate)
    {
        return new CertificateEvidence(
            Hashing.BytesSha256(certificate.RawData),
            Hashing.BytesSha256(ExportSubjectPublicKeyInfo(certificate)),
            certificate.Thumbprint.ToLowerInvariant(),
            certificate.Subject,
            certificate.Issuer,
            certificate.SerialNumber,
            certificate.NotBefore.ToUniversalTime().ToString(
                "yyyy-MM-ddTHH:mm:ss.fffffffZ",
                System.Globalization.CultureInfo.InvariantCulture),
            certificate.NotAfter.ToUniversalTime().ToString(
                "yyyy-MM-ddTHH:mm:ss.fffffffZ",
                System.Globalization.CultureInfo.InvariantCulture));
    }

    private static byte[] ExportSubjectPublicKeyInfo(
        X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is not null)
        {
            return rsa.ExportSubjectPublicKeyInfo();
        }
        using var ecdsa = certificate.GetECDsaPublicKey();
        if (ecdsa is not null)
        {
            return ecdsa.ExportSubjectPublicKeyInfo();
        }
        using var dsa = certificate.GetDSAPublicKey();
        if (dsa is not null)
        {
            return dsa.ExportSubjectPublicKeyInfo();
        }
        throw new SigningFailureException(
            "Signer certificate uses an unsupported public-key algorithm.");
    }
}

internal sealed record ChainEvidence(
    bool Trusted,
    string RevocationMode,
    string RevocationFlag,
    string VerificationFlags,
    string VerificationTimeUtc,
    string TrustAnchorCertificateSha256,
    string TrustAnchorSubject,
    string TrustAnchorSource);

internal sealed record TimestampEvidence(
    string Status,
    string Format,
    string DigestAlgorithm,
    string GeneratedAtUtc,
    string MessageImprintSha256,
    string CertificateSha256,
    string Subject,
    string Issuer,
    string SerialNumber,
    ChainEvidence Chain);

internal sealed record VerifierEvidence(
    string Implementation,
    bool ProviderIndependent,
    bool JsignOutputTrusted);

internal sealed record ArtifactSignatureEvidence(
    string ArtifactFileName,
    string ArtifactSha256,
    string AuthenticodeImageDigestSha256,
    string DigestAlgorithm,
    string CryptographicVerification,
    CertificateEvidence Signer,
    ChainEvidence SignerChain,
    TimestampEvidence Timestamp,
    VerifierEvidence Verifier);

internal sealed record SdkPinEvidence(
    string FileName,
    string Sha256,
    string Version,
    string RollForward,
    bool AllowPrerelease);

internal static class SdkPinPolicy
{
    public const string FileName = "global.json";
    public const string ApprovedSdkVersion = "10.0.110";
    public const string ApprovedRollForward = "disable";
    public const string ApprovedSha256 =
        "878939d8aec1375674ef0508026fc15101ac15f31807d97651c6f38b99feb5dd";

    public static SdkPinEvidence Require(string path)
    {
        var resolved = GovernedPath.ResolveRegularFile(
            path,
            "Linux KeyLocker signer SDK pin",
            [".json"],
            FileName,
            executable: false);
        var bytes = File.ReadAllBytes(resolved);
        var sha256 = Hashing.BytesSha256(bytes);
        if (!Hashing.FixedHexEquals(sha256, ApprovedSha256))
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer SDK pin differs from its exact "
                + "approved global.json identity.");
        }

        using var document = JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 4,
            });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer SDK pin root must be one object.");
        }
        var rootProperties =
            document.RootElement.EnumerateObject().ToArray();
        if (rootProperties.Length != 1
            || rootProperties[0].Name != "sdk"
            || rootProperties[0].Value.ValueKind != JsonValueKind.Object)
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer SDK pin must contain only one SDK "
                + "object.");
        }
        var sdkProperties =
            rootProperties[0].Value.EnumerateObject().ToArray();
        var expectedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "version",
            "rollForward",
            "allowPrerelease",
        };
        if (sdkProperties.Length != expectedNames.Count
            || sdkProperties.Any(
                property => !expectedNames.Contains(property.Name))
            || sdkProperties.Select(property => property.Name)
                .Distinct(StringComparer.Ordinal)
                .Count() != expectedNames.Count
            || rootProperties[0].Value.GetProperty("version").GetString()
                != ApprovedSdkVersion
            || rootProperties[0].Value.GetProperty("rollForward").GetString()
                != ApprovedRollForward
            || rootProperties[0].Value.GetProperty("allowPrerelease")
                .ValueKind != JsonValueKind.False)
        {
            throw new SigningFailureException(
                "Linux KeyLocker signer SDK pin must select exact stable SDK "
                + "10.0.110 with roll-forward disabled.");
        }
        return new SdkPinEvidence(
            FileName,
            sha256,
            ApprovedSdkVersion,
            ApprovedRollForward,
            false);
    }
}

internal sealed record VerifierBuildEvidence(
    string DotnetSdkVersion,
    string FrameworkDescription,
    string RuntimeIdentifier,
    string AssemblyFileName,
    string AssemblySha256,
    string PackageLockFileName,
    string PackageLockSha256,
    SdkPinEvidence SdkPin)
{
    public static VerifierBuildEvidence Current()
    {
        var assembly = typeof(AuthenticodeVerifier).Assembly;
        var sdkVersion = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(
                attribute =>
                    attribute.Key == "BuildDotnetSdkVersion")
            .Value;
        if (string.IsNullOrWhiteSpace(sdkVersion)
            || string.IsNullOrWhiteSpace(assembly.Location))
        {
            throw new SigningFailureException(
                "Verifier build identity metadata is absent.");
        }
        var assemblyPath = Path.GetFullPath(assembly.Location);
        var packageLockPath = Path.Combine(
            Path.GetDirectoryName(assemblyPath)
                ?? throw new SigningFailureException(
                    "Verifier assembly directory is absent."),
            "packages.lock.json");
        packageLockPath = GovernedPath.ResolveRegularFile(
            packageLockPath,
            "Linux KeyLocker verifier package lock",
            [".json"],
            "packages.lock.json",
            executable: false);
        var sdkPin = SdkPinPolicy.Require(
            Path.Combine(
                Path.GetDirectoryName(assemblyPath)
                    ?? throw new SigningFailureException(
                        "Verifier assembly directory is absent."),
                SdkPinPolicy.FileName));
        if (!string.Equals(
                sdkVersion,
                SdkPinPolicy.ApprovedSdkVersion,
                StringComparison.Ordinal))
        {
            throw new SigningFailureException(
                "Verifier build SDK metadata differs from the sealed exact "
                + "global.json pin.");
        }
        return new VerifierBuildEvidence(
            sdkVersion,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.RuntimeIdentifier,
            Path.GetFileName(assemblyPath),
            Hashing.FileSha256(assemblyPath),
            Path.GetFileName(packageLockPath),
            Hashing.FileSha256(packageLockPath),
            sdkPin);
    }
}

internal static class ReceiptWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void Write(
        SigningConfiguration configuration,
        IReadOnlyCollection<string> artifactPaths,
        string status,
        string reason,
        IReadOnlyCollection<ProcessTransaction> transactions,
        IReadOnlyCollection<ArtifactSignatureEvidence> signatureEvidence)
    {
        if (string.IsNullOrWhiteSpace(configuration.ReceiptPath))
        {
            return;
        }

        var receiptPath =
            ReceiptPathPolicy.Validate(configuration.ReceiptPath);
        var directory = Path.GetDirectoryName(receiptPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new SigningFailureException(
                "Signing receipt requires a parent directory.");
        }
        var verifierBuild = VerifierBuildEvidence.Current();
        if (!Hashing.FixedHexEquals(
                verifierBuild.AssemblySha256,
                configuration.RuntimeHost.Evidence.SignerAssemblySha256))
        {
            throw new SigningFailureException(
                "Verifier build identity differs from the sealed signer "
                + "runtime identity.");
        }
        var receipt = new
        {
            contractName = "chummer6-ui.desktop_artifact_signing",
            contractVersion = 2,
            generatedAt = DateTimeOffset.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ssZ",
                System.Globalization.CultureInfo.InvariantCulture),
            platform = "windows",
            app = configuration.AppKey,
            rid = configuration.Rid,
            releaseChannel = configuration.ReleaseChannel,
            releaseVersion = configuration.ReleaseVersion,
            signingStatus = status,
            notarizationStatus = (string?)null,
            reason,
            signingBackend = "digicert_keylocker_linux_jsign",
            digestAlgorithm = "sha256",
            timestamp = new
            {
                protocol = "rfc3161",
                url = "http://timestamp.digicert.com",
                digestAlgorithm = "sha256",
                status = status == "pass"
                    ? "verified"
                    : "not_verified",
            },
            signer = signatureEvidence.FirstOrDefault()?.Signer
                ?? configuration.PublicCertificateEvidence,
            signingTool = new
            {
                name = "jsign",
                version = "7.5",
                invocation = "pinned_java_jar",
                jsignJarFileName =
                    Path.GetFileName(configuration.JsignJarPath),
                jsignJarSha256 = configuration.JsignJarSha256,
                javaFileName = Path.GetFileName(configuration.JavaPath),
                javaSha256 = configuration.JavaSha256,
                javaHomeDirectoryName =
                    Path.GetFileName(configuration.JavaHome),
                javaTreeSha256 = configuration.JavaTreeSha256,
                javaTreeCanonicalization =
                    "gnu_tar_parent_and_root_name_v1",
                credentials = "environment_reference_only",
                environmentPolicy =
                    "clean_exact_allowlist_without_sm_or_startup_hooks",
                networkPolicy =
                    "fixed_digicert_origin_no_proxy_no_redirects",
                runtimeHost = configuration.RuntimeHost.Evidence,
                verifierBuild,
            },
            artifactSignatures = signatureEvidence,
            providerTransactions = transactions,
            artifacts = signatureEvidence
                .Select(evidence => new
                {
                    fileName = evidence.ArtifactFileName,
                    sha256 = evidence.ArtifactSha256,
                    kind = evidence.ArtifactFileName
                        .EndsWith("-installer.exe",
                            StringComparison.OrdinalIgnoreCase)
                        ? "installer"
                        : "portable",
                    signingStatus = status,
                })
                .ToArray(),
        };

        var bytes = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(receipt, JsonOptions) + "\n");
        string? temporaryPath = null;
        try
        {
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var candidate = Path.Combine(
                    directory,
                    $".{Path.GetFileName(receiptPath)}."
                    + $"{Path.GetRandomFileName()}.tmp");
                try
                {
                    using var stream = new FileStream(
                        candidate,
                        new FileStreamOptions
                        {
                            Mode = FileMode.CreateNew,
                            Access = FileAccess.Write,
                            Share = FileShare.None,
                            Options = FileOptions.WriteThrough,
                            UnixCreateMode =
                                UnixFileMode.UserRead
                                | UnixFileMode.UserWrite,
                        });
                    temporaryPath = candidate;
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                    break;
                }
                catch (IOException) when (!File.Exists(candidate))
                {
                    throw;
                }
                catch (IOException)
                {
                    // A random candidate collision is harmless; retry.
                }
            }
            if (temporaryPath is null)
            {
                throw new SigningFailureException(
                    "Could not allocate a private atomic receipt file.");
            }
            File.Move(temporaryPath, receiptPath, overwrite: true);
            temporaryPath = null;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Never replace the primary receipt error.
                }
            }
        }
    }
}

internal static class ReceiptPathPolicy
{
    public static string Validate(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }
        if (string.IsNullOrWhiteSpace(path)
            || !string.Equals(path, path.Trim(), StringComparison.Ordinal)
            || path.Any(char.IsControl)
            || !Path.IsPathFullyQualified(path)
            || !string.Equals(
                path,
                Path.GetFullPath(path),
                StringComparison.Ordinal)
            || !string.Equals(
                Path.GetExtension(path),
                ".json",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SigningFailureException(
                "Signing receipt path must be one normalized absolute JSON "
                + "path.");
        }
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new SigningFailureException(
                "Signing receipt requires an existing parent directory.");
        }
        RequireNoLinkDirectory(directory);
        var target = new FileInfo(path);
        target.Refresh();
        if (target.Exists
            && (target.LinkTarget is not null
                || (target.Attributes & FileAttributes.ReparsePoint) != 0))
        {
            throw new SigningFailureException(
                "Signing receipt target must not be a link or reparse point.");
        }
        return path;
    }

    private static void RequireNoLinkDirectory(string directory)
    {
        var root = Path.GetPathRoot(directory)
            ?? throw new SigningFailureException(
                "Signing receipt directory has no filesystem root.");
        var current = root;
        foreach (var segment in directory[root.Length..].Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var info = new DirectoryInfo(current);
            info.Refresh();
            if (!info.Exists
                || info.LinkTarget is not null
                || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new SigningFailureException(
                    "Signing receipt directory must not traverse a link or "
                    + "reparse point.");
            }
        }
    }
}
