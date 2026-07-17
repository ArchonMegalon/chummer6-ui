using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace Chummer.Hub.Web;

public static class HubDataProtection
{
    public const string KeysPathConfigurationKey = "CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH";
    public const string CertificatePathConfigurationKey = "CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH";
    public const string CertificatePasswordConfigurationKey = "CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PASSWORD";
    public const string PreviousCertificatePathConfigurationKey = "CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PATH";
    public const string PreviousCertificatePasswordConfigurationKey = "CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PASSWORD";

    private const string ApplicationName = "Chummer.Hub.Web";
    private const int MinimumRsaKeyBits = 3072;
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public static void Configure(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        string? configuredKeysPath = NormalizeOptional(configuration[KeysPathConfigurationKey]);
        string? configuredCertificatePath = NormalizeOptional(configuration[CertificatePathConfigurationKey]);
        string? configuredCertificatePassword =
            NormalizeSecret(configuration[CertificatePasswordConfigurationKey]);
        string? configuredPreviousCertificatePath =
            NormalizeOptional(configuration[PreviousCertificatePathConfigurationKey]);
        string? configuredPreviousCertificatePassword =
            NormalizeSecret(configuration[PreviousCertificatePasswordConfigurationKey]);

        if (configuredKeysPath is null
            && configuredCertificatePath is null
            && configuredCertificatePassword is null)
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    $"Production requires {KeysPathConfigurationKey} and {CertificatePathConfigurationKey}.");
            }

            RejectIncompletePreviousCertificateConfiguration(
                configuredPreviousCertificatePath,
                configuredPreviousCertificatePassword);
            return;
        }

        if (configuredKeysPath is null
            || configuredCertificatePath is null
            || configuredCertificatePassword is null)
        {
            throw new InvalidOperationException(
                $"{KeysPathConfigurationKey}, {CertificatePathConfigurationKey}, and {CertificatePasswordConfigurationKey} must be configured together.");
        }

        RejectIncompletePreviousCertificateConfiguration(
            configuredPreviousCertificatePath,
            configuredPreviousCertificatePassword);

        string keysPath = ResolveExternalPath(
            configuredKeysPath,
            KeysPathConfigurationKey,
            environment.ContentRootPath);
        ValidateDirectory(keysPath, environment.ContentRootPath);
        ValidatePersistedKeyRing(keysPath);

        HubDataProtectionCertificateSet certificates = HubDataProtectionCertificateSet.Load(
            configuration,
            environment.ContentRootPath,
            configuredCertificatePath,
            configuredPreviousCertificatePath);
        try
        {
            // Register through a factory so the container owns and disposes the
            // ephemeral private-key handles after the host has stopped.
            services.AddSingleton<HubDataProtectionCertificateSet>(_ => certificates);
            IDataProtectionBuilder dataProtection = services.AddDataProtection()
                .SetApplicationName(ApplicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .ProtectKeysWithCertificate(certificates.Current);
            dataProtection.UnprotectKeysWithAnyCertificate(certificates.All);
        }
        catch
        {
            certificates.Dispose();
            throw;
        }
    }

    public static void VerifyOperational(IServiceProvider services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration[KeysPathConfigurationKey]))
            return;

        // Resolve the factory registration before the first key operation so the
        // host owns the certificate set's lifetime even when startup later fails.
        _ = services.GetRequiredService<HubDataProtectionCertificateSet>();

        // Materialize every retained key before Protect is allowed to generate a
        // replacement default key. Without this gate, a host missing an older
        // rotation certificate can appear healthy by minting a new key while
        // silently losing the ability to unprotect existing cookies and payloads.
        IKeyManager keyManager = services.GetRequiredService<IKeyManager>();
        foreach (IKey key in keyManager.GetAllKeys())
        {
            _ = key.CreateEncryptor()
                ?? throw new InvalidOperationException(
                    "Hub Data Protection could not materialize a retained key.");
        }

        IDataProtector protector = services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("startup-readiness-v1");
        const string canary = "hub-data-protection-ready";
        string protectedCanary = protector.Protect(canary);
        if (!string.Equals(protector.Unprotect(protectedCanary), canary, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Hub Data Protection failed its startup round trip.");
        }

        string keysPath = Path.GetFullPath(
            configuration[KeysPathConfigurationKey]
            ?? throw new InvalidOperationException(
                $"{KeysPathConfigurationKey} was removed during startup."));
        ValidatePersistedKeyRing(keysPath, requireAtLeastOneKey: true);
    }

    private static void RejectIncompletePreviousCertificateConfiguration(
        string? previousCertificatePath,
        string? previousCertificatePassword)
    {
        if ((previousCertificatePath is null) != (previousCertificatePassword is null))
        {
            throw new InvalidOperationException(
                $"{PreviousCertificatePathConfigurationKey} and {PreviousCertificatePasswordConfigurationKey} must be configured together.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeSecret(string? value)
        => string.IsNullOrEmpty(value) ? null : value;

    private static string ResolveExternalPath(
        string configuredPath,
        string configurationKey,
        string contentRoot)
    {
        if (!Path.IsPathFullyQualified(configuredPath))
        {
            throw new InvalidOperationException($"{configurationKey} must be an absolute path.");
        }

        string fullPath = Path.GetFullPath(configuredPath);
        string normalizedContentRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRoot));
        string contentPrefix = normalizedContentRoot + Path.DirectorySeparatorChar;
        if (string.Equals(fullPath, normalizedContentRoot, StringComparison.Ordinal)
            || fullPath.StartsWith(contentPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{configurationKey} must be outside the application content root.");
        }

        return fullPath;
    }

    private static void ValidateDirectory(string keysPath, string contentRoot)
    {
        _ = ResolveExternalPath(keysPath, KeysPathConfigurationKey, contentRoot);

        if (!Directory.Exists(keysPath))
        {
            throw new InvalidOperationException(
                $"{KeysPathConfigurationKey} must reference an existing persistent directory.");
        }

        FileAttributes attributes = File.GetAttributes(keysPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                $"{KeysPathConfigurationKey} must not reference a symbolic link or reparse point.");
        }

        if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(keysPath) != PrivateDirectoryMode)
        {
            throw new InvalidOperationException(
                $"{KeysPathConfigurationKey} must have private user-only directory permissions.");
        }
    }

    private static void ValidatePersistedKeyRing(
        string keysPath,
        bool requireAtLeastOneKey = false)
    {
        string[] keyFiles = Directory
            .EnumerateFiles(keysPath, "key-*.xml", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (requireAtLeastOneKey && keyFiles.Length == 0)
        {
            throw new InvalidOperationException(
                "Hub Data Protection did not persist an encrypted key during its startup probe.");
        }

        foreach (string keyFile in keyFiles)
        {
            FileAttributes attributes = File.GetAttributes(keyFile);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                throw new InvalidOperationException(
                    "The Hub Data Protection key ring contains a non-regular or symbolic-link key entry.");
            }

            if (!OperatingSystem.IsWindows()
                && File.GetUnixFileMode(keyFile)
                    != (UnixFileMode.UserRead | UnixFileMode.UserWrite))
            {
                throw new InvalidOperationException(
                    "Hub Data Protection key files must use owner-only mode 0600.");
            }

            using FileStream stream = new(
                keyFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            using XmlReader reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    CloseInput = false
                });
            XDocument document = XDocument.Load(reader, LoadOptions.None);
            XElement[] encryptedSecrets = document
                .Descendants()
                .Where(element => element.Name.LocalName == "encryptedSecret")
                .ToArray();
            bool containsPlaintextMasterKey = document
                .Descendants()
                .Any(element => element.Name.LocalName == "masterKey");
            bool hasDecryptor = encryptedSecrets.Length == 1
                && encryptedSecrets[0].Attributes().Any(attribute =>
                    attribute.Name.LocalName == "decryptorType"
                    && !string.IsNullOrWhiteSpace(attribute.Value));
            bool hasCipherValue = encryptedSecrets.Length == 1
                && encryptedSecrets[0]
                    .Descendants()
                    .Any(element => element.Name.LocalName == "CipherValue"
                                    && !string.IsNullOrWhiteSpace(element.Value));
            if (containsPlaintextMasterKey
                || encryptedSecrets.Length != 1
                || !hasDecryptor
                || !hasCipherValue)
            {
                throw new InvalidOperationException(
                    "The Hub Data Protection key ring contains a key that is not certificate encrypted.");
            }
        }
    }

    private sealed class HubDataProtectionCertificateSet : IDisposable
    {
        private X509Certificate2[]? _all;

        private HubDataProtectionCertificateSet(X509Certificate2[] all)
        {
            _all = all;
        }

        internal X509Certificate2 Current
            => _all?[0]
                ?? throw new ObjectDisposedException(nameof(HubDataProtectionCertificateSet));

        internal X509Certificate2[] All
            => _all
                ?? throw new ObjectDisposedException(nameof(HubDataProtectionCertificateSet));

        internal static HubDataProtectionCertificateSet Load(
            IConfiguration configuration,
            string contentRoot,
            string currentPath,
            string? previousPath)
        {
            X509Certificate2? current = null;
            X509Certificate2? previous = null;
            try
            {
                current = LoadCertificate(
                    ResolveExternalPath(currentPath, CertificatePathConfigurationKey, contentRoot),
                    configuration[CertificatePasswordConfigurationKey],
                    CertificatePathConfigurationKey);
                ValidateCurrentValidity(current);

                if (previousPath is not null)
                {
                    previous = LoadCertificate(
                        ResolveExternalPath(
                            previousPath,
                            PreviousCertificatePathConfigurationKey,
                            contentRoot),
                        configuration[PreviousCertificatePasswordConfigurationKey],
                        PreviousCertificatePathConfigurationKey);
                    byte[] currentHash = current.GetCertHash(HashAlgorithmName.SHA256);
                    byte[] previousHash = previous.GetCertHash(HashAlgorithmName.SHA256);
                    bool duplicate;
                    try
                    {
                        duplicate = CryptographicOperations.FixedTimeEquals(currentHash, previousHash);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(currentHash);
                        CryptographicOperations.ZeroMemory(previousHash);
                    }
                    if (duplicate)
                    {
                        throw new InvalidOperationException(
                            $"{PreviousCertificatePathConfigurationKey} must identify a different certificate from {CertificatePathConfigurationKey}.");
                    }
                }

                X509Certificate2[] all = previous is null
                    ? [current]
                    : [current, previous];
                current = null;
                previous = null;
                return new HubDataProtectionCertificateSet(all);
            }
            finally
            {
                current?.Dispose();
                previous?.Dispose();
            }
        }

        public void Dispose()
        {
            X509Certificate2[]? all = Interlocked.Exchange(ref _all, null);
            if (all is null)
                return;

            foreach (X509Certificate2 certificate in all)
                certificate.Dispose();
        }

        private static X509Certificate2 LoadCertificate(
            string certificatePath,
            string? certificatePassword,
            string configurationKey)
        {
            using HubPinnedCertificateFile pinnedCertificate =
                HubPinnedCertificateFile.Open(certificatePath, configurationKey);
            byte[] pkcs12Bytes = pinnedCertificate.ReadStableBytes();
            X509Certificate2 certificate;
            try
            {
                certificate = X509CertificateLoader.LoadPkcs12(
                    pkcs12Bytes,
                    certificatePassword,
                    X509KeyStorageFlags.EphemeralKeySet);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"{configurationKey} could not be loaded as a PKCS#12 certificate.",
                    ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(pkcs12Bytes);
            }

            try
            {
                if (!certificate.HasPrivateKey)
                {
                    throw new InvalidOperationException(
                        $"{configurationKey} must include an accessible RSA private key.");
                }

                X509KeyUsageExtension? keyUsage = certificate.Extensions
                    .OfType<X509KeyUsageExtension>()
                    .SingleOrDefault();
                if (keyUsage is not null
                    && (keyUsage.KeyUsages & X509KeyUsageFlags.KeyEncipherment) == 0)
                {
                    throw new InvalidOperationException(
                        $"{configurationKey} key usage must allow key encipherment.");
                }

                ValidateRsaCapability(certificate, configurationKey);
                return certificate;
            }
            catch
            {
                certificate.Dispose();
                throw;
            }
        }

        private static void ValidateRsaCapability(
            X509Certificate2 certificate,
            string configurationKey)
        {
            RSA? rsa;
            try
            {
                rsa = certificate.GetRSAPrivateKey();
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"{configurationKey} must include an accessible RSA private key.",
                    ex);
            }

            if (rsa is null)
            {
                throw new InvalidOperationException(
                    $"{configurationKey} must include an accessible RSA private key.");
            }

            using (rsa)
            {
                if (rsa.KeySize < MinimumRsaKeyBits)
                {
                    throw new InvalidOperationException(
                        $"{configurationKey} RSA keys must be at least {MinimumRsaKeyBits} bits.");
                }

                byte[] plaintext = RandomNumberGenerator.GetBytes(32);
                byte[]? encrypted = null;
                byte[]? decrypted = null;
                try
                {
                    encrypted = rsa.Encrypt(plaintext, RSAEncryptionPadding.Pkcs1);
                    decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);
                    if (!CryptographicOperations.FixedTimeEquals(plaintext, decrypted))
                    {
                        throw new InvalidOperationException(
                            $"{configurationKey} RSA private-key capability probe did not round-trip.");
                    }
                }
                catch (CryptographicException ex)
                {
                    throw new InvalidOperationException(
                        $"{configurationKey} RSA private-key capability probe failed.",
                        ex);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plaintext);
                    if (encrypted is not null)
                        CryptographicOperations.ZeroMemory(encrypted);
                    if (decrypted is not null)
                        CryptographicOperations.ZeroMemory(decrypted);
                }
            }
        }

        private static void ValidateCurrentValidity(X509Certificate2 certificate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now < certificate.NotBefore.ToUniversalTime()
                || now >= certificate.NotAfter.ToUniversalTime())
            {
                throw new InvalidOperationException(
                    $"{CertificatePathConfigurationKey} must be currently valid.");
            }
        }
    }
}
