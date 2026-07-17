#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Chummer.Hub.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
[SupportedOSPlatform("linux")]
public sealed class HubDataProtectionTests
{
    private const string CertificatePassword = "hub-data-protection-test-password";
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [TestMethod]
    public void Production_requires_complete_current_and_previous_configuration()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();

        Dictionary<string, string?>[] incompleteConfigurations =
        [
            new(),
            new()
            {
                [HubDataProtection.KeysPathConfigurationKey] = fixture.KeysPath
            },
            new()
            {
                [HubDataProtection.KeysPathConfigurationKey] = fixture.KeysPath,
                [HubDataProtection.CertificatePathConfigurationKey] = fixture.CurrentCertificatePath
            },
            new()
            {
                [HubDataProtection.CertificatePathConfigurationKey] = fixture.CurrentCertificatePath,
                [HubDataProtection.CertificatePasswordConfigurationKey] = CertificatePassword
            },
            new()
            {
                [HubDataProtection.KeysPathConfigurationKey] = fixture.KeysPath,
                [HubDataProtection.CertificatePathConfigurationKey] = fixture.CurrentCertificatePath,
                [HubDataProtection.CertificatePasswordConfigurationKey] = CertificatePassword,
                [HubDataProtection.PreviousCertificatePathConfigurationKey] = fixture.PreviousCertificatePath
            },
            new()
            {
                [HubDataProtection.KeysPathConfigurationKey] = fixture.KeysPath,
                [HubDataProtection.CertificatePathConfigurationKey] = fixture.CurrentCertificatePath,
                [HubDataProtection.CertificatePasswordConfigurationKey] = CertificatePassword,
                [HubDataProtection.PreviousCertificatePasswordConfigurationKey] = CertificatePassword
            }
        ];

        foreach (Dictionary<string, string?> values in incompleteConfigurations)
        {
            IConfiguration configuration = CreateConfiguration(values);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                HubDataProtection.Configure(
                    new ServiceCollection(),
                    configuration,
                    fixture.Environment));
        }
    }

    [TestMethod]
    public void Rsa3072_password_protected_certificate_persists_only_encrypted_private_key_files()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        WriteRsaCertificate(
            fixture.CurrentCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Chummer Hub Data Protection Tests");
        IConfiguration configuration = fixture.CreateCurrentConfiguration(
            fixture.CurrentCertificatePath,
            CertificatePassword);

        using ServiceProvider provider = CreateProvider(fixture, configuration);
        HubDataProtection.VerifyOperational(provider, configuration);

        string[] allXmlFiles = Directory.GetFiles(
            fixture.KeysPath,
            "*.xml",
            SearchOption.TopDirectoryOnly);
        Assert.IsTrue(allXmlFiles.Length > 0, "The startup probe must persist at least one key.");
        Assert.IsTrue(allXmlFiles.All(path =>
            Path.GetFileName(path).StartsWith("key-", StringComparison.Ordinal)
            && Path.GetFileName(path).EndsWith(".xml", StringComparison.Ordinal)));

        foreach (string keyFile in allXmlFiles)
        {
            Assert.AreEqual(PrivateFileMode, File.GetUnixFileMode(keyFile));
            XDocument document = XDocument.Load(keyFile, LoadOptions.None);
            XElement[] encryptedSecrets = document
                .Descendants()
                .Where(element => element.Name.LocalName == "encryptedSecret")
                .ToArray();
            Assert.HasCount(1, encryptedSecrets);
            Assert.IsFalse(document.Descendants().Any(element =>
                element.Name.LocalName == "masterKey"));
            Assert.IsTrue(encryptedSecrets[0].Descendants().Any(element =>
                element.Name.LocalName == "CipherValue"
                && !string.IsNullOrWhiteSpace(element.Value)));
        }
    }

    [TestMethod]
    public void Weak_rsa_and_non_rsa_certificates_are_rejected()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        string weakRsaPath = fixture.CertificatePath("weak-rsa.p12");
        string ecdsaPath = fixture.CertificatePath("ecdsa.p12");
        WriteRsaCertificate(
            weakRsaPath,
            keySize: 2048,
            password: CertificatePassword,
            subjectName: "CN=Weak Chummer Hub Data Protection Test");
        WriteEcdsaCertificate(ecdsaPath, CertificatePassword);

        foreach (string certificatePath in new[] { weakRsaPath, ecdsaPath })
        {
            IConfiguration configuration = fixture.CreateCurrentConfiguration(
                certificatePath,
                CertificatePassword);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                HubDataProtection.Configure(
                    new ServiceCollection(),
                    configuration,
                    fixture.Environment));
        }
    }

    [TestMethod]
    public void No_mac_plaintext_openssl_pkcs12_is_rejected_before_key_ring_mutation()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        string unprotectedPath = fixture.CertificatePath("openssl-nomac-unprotected.p12");
        WriteOpenSslUnprotectedPkcs12(fixture, unprotectedPath, omitMac: true);
        IConfiguration configuration = fixture.CreateCurrentConfiguration(
            unprotectedPath,
            CertificatePassword);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                configuration,
                fixture.Environment));

        StringAssert.Contains(exception.Message, "password MAC integrity");
        Assert.HasCount(0, Directory.GetFileSystemEntries(fixture.KeysPath));
    }

    [TestMethod]
    public void Mac_protected_openssl_pkcs12_with_plaintext_key_bag_is_rejected()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        string unprotectedPath = fixture.CertificatePath("openssl-mac-plaintext-key.p12");
        WriteOpenSslUnprotectedPkcs12(fixture, unprotectedPath, omitMac: false);
        IConfiguration configuration = fixture.CreateCurrentConfiguration(
            unprotectedPath,
            CertificatePassword);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                configuration,
                fixture.Environment));

        StringAssert.Contains(exception.Message, "unprotected plaintext private-key bag");
        Assert.HasCount(0, Directory.GetFileSystemEntries(fixture.KeysPath));
    }

    [TestMethod]
    public void Duplicate_current_and_previous_certificates_are_rejected()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        WriteRsaCertificate(
            fixture.CurrentCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Duplicate Chummer Hub Data Protection Test");
        File.Copy(fixture.CurrentCertificatePath, fixture.PreviousCertificatePath);
        File.SetUnixFileMode(fixture.PreviousCertificatePath, PrivateFileMode);
        IConfiguration configuration = fixture.CreateRotationConfiguration(
            fixture.CurrentCertificatePath,
            CertificatePassword,
            fixture.PreviousCertificatePath,
            CertificatePassword);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                configuration,
                fixture.Environment));
    }

    [TestMethod]
    public void Previous_certificate_unlocks_a_ring_that_current_certificate_alone_cannot_read()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        WriteRsaCertificate(
            fixture.CurrentCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Chummer Hub Data Protection A");
        WriteRsaCertificate(
            fixture.PreviousCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Chummer Hub Data Protection B");

        IConfiguration aConfiguration = fixture.CreateCurrentConfiguration(
            fixture.CurrentCertificatePath,
            CertificatePassword);
        string protectedByA;
        using (ServiceProvider aProvider = CreateProvider(fixture, aConfiguration))
        {
            HubDataProtection.VerifyOperational(aProvider, aConfiguration);
            protectedByA = aProvider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("hub-certificate-rotation-v1")
                .Protect("ring-a-payload");
        }

        IConfiguration bOnlyConfiguration = fixture.CreateCurrentConfiguration(
            fixture.PreviousCertificatePath,
            CertificatePassword);
        using (ServiceProvider bOnlyProvider = CreateProvider(fixture, bOnlyConfiguration))
        {
            IDataProtector bOnly = bOnlyProvider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("hub-certificate-rotation-v1");
            try
            {
                // A host may fail while warming an unreadable existing ring or
                // may create a new B-wrapped default key. Either way, B alone
                // must never recover payloads protected by the A-wrapped key.
                HubDataProtection.VerifyOperational(bOnlyProvider, bOnlyConfiguration);
                Assert.ThrowsExactly<CryptographicException>(() =>
                    bOnly.Unprotect(protectedByA));
            }
            catch (CryptographicException)
            {
                // Startup rejection is the stronger fail-closed outcome.
            }
        }

        IConfiguration bWithAConfiguration = fixture.CreateRotationConfiguration(
            fixture.PreviousCertificatePath,
            CertificatePassword,
            fixture.CurrentCertificatePath,
            CertificatePassword);
        using ServiceProvider bWithAProvider = CreateProvider(fixture, bWithAConfiguration);
        HubDataProtection.VerifyOperational(bWithAProvider, bWithAConfiguration);
        IDataProtector bWithA = bWithAProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("hub-certificate-rotation-v1");
        Assert.AreEqual("ring-a-payload", bWithA.Unprotect(protectedByA));
    }

    [TestMethod]
    public void Plaintext_preexisting_key_ring_is_rejected()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        WriteRsaCertificate(
            fixture.CurrentCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Chummer Hub Plaintext Ring Rejection Test");
        string plaintextKeyPath = Path.Combine(
            fixture.KeysPath,
            $"key-{Guid.NewGuid():D}.xml");
        File.WriteAllText(
            plaintextKeyPath,
            "<key><descriptor><masterKey><value>plaintext-key-material</value></masterKey></descriptor></key>");
        File.SetUnixFileMode(plaintextKeyPath, PrivateFileMode);
        IConfiguration configuration = fixture.CreateCurrentConfiguration(
            fixture.CurrentCertificatePath,
            CertificatePassword);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                configuration,
                fixture.Environment));
    }

    [TestMethod]
    public void Key_ring_directory_must_be_private_and_must_not_be_a_symbolic_link()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        WriteRsaCertificate(
            fixture.CurrentCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Chummer Hub Key Ring Directory Test");

        File.SetUnixFileMode(
            fixture.KeysPath,
            PrivateDirectoryMode | UnixFileMode.GroupRead);
        IConfiguration insecureConfiguration = fixture.CreateCurrentConfiguration(
            fixture.CurrentCertificatePath,
            CertificatePassword);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                insecureConfiguration,
                fixture.Environment));

        File.SetUnixFileMode(fixture.KeysPath, PrivateDirectoryMode);
        string symbolicKeysPath = Path.Combine(fixture.ExternalRoot, "keys-link");
        Directory.CreateSymbolicLink(symbolicKeysPath, fixture.KeysPath);
        IConfiguration symbolicConfiguration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                [HubDataProtection.KeysPathConfigurationKey] = symbolicKeysPath,
                [HubDataProtection.CertificatePathConfigurationKey] = fixture.CurrentCertificatePath,
                [HubDataProtection.CertificatePasswordConfigurationKey] = CertificatePassword
            });
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                symbolicConfiguration,
                fixture.Environment));
    }

    [TestMethod]
    public void Pinned_key_ring_survives_writable_ancestor_swap_during_host_startup()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        WriteRsaCertificate(
            fixture.CurrentCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Chummer Hub Pinned Key Ring Swap Test");
        IConfiguration configuration = fixture.CreateCurrentConfiguration(
            fixture.CurrentCertificatePath,
            CertificatePassword);
        ServiceCollection services = new();
        string pinnedKeysPath = Path.Combine(fixture.ExternalRoot, "keys-pinned");
        using var keyRingPinned = new ManualResetEventSlim(initialState: false);
        Task ancestorSwap = Task.Run(() =>
        {
            keyRingPinned.Wait();
            Directory.Move(fixture.KeysPath, pinnedKeysPath);
            Directory.CreateDirectory(fixture.KeysPath);
            File.SetUnixFileMode(fixture.KeysPath, PrivateDirectoryMode);
        });

        try
        {
            HubDataProtection.Configure(services, configuration, fixture.Environment);
        }
        finally
        {
            keyRingPinned.Set();
        }
        ancestorSwap.GetAwaiter().GetResult();

        using ServiceProvider provider = services.BuildServiceProvider();
        HubDataProtection.VerifyOperational(provider, configuration);

        Assert.HasCount(0, Directory.GetFileSystemEntries(fixture.KeysPath));
        Assert.IsTrue(Directory.GetFiles(
                pinnedKeysPath,
                "key-*.xml",
                SearchOption.TopDirectoryOnly).Length > 0,
            "The framework must persist its encrypted key through the pinned directory descriptor.");
    }

    [TestMethod]
    public void Key_ring_rejects_unexpected_and_symbolic_link_xml_entries()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        WriteRsaCertificate(
            fixture.CurrentCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Chummer Hub Key Ring Entry Test");
        IConfiguration configuration = fixture.CreateCurrentConfiguration(
            fixture.CurrentCertificatePath,
            CertificatePassword);

        string unexpectedPath = Path.Combine(fixture.KeysPath, "unexpected.xml");
        File.WriteAllText(unexpectedPath, "<unexpected />");
        File.SetUnixFileMode(unexpectedPath, PrivateFileMode);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                configuration,
                fixture.Environment));
        File.Delete(unexpectedPath);

        string outsideKeyPath = Path.Combine(fixture.ExternalRoot, "outside-key.xml");
        File.WriteAllText(outsideKeyPath, "<key />");
        File.SetUnixFileMode(outsideKeyPath, PrivateFileMode);
        string symbolicKeyPath = Path.Combine(
            fixture.KeysPath,
            $"key-{Guid.NewGuid():D}.xml");
        File.CreateSymbolicLink(symbolicKeyPath, outsideKeyPath);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                configuration,
                fixture.Environment));
    }

    [TestMethod]
    public async Task Certificate_symbolic_links_and_fifos_are_rejected_without_blocking()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        WriteRsaCertificate(
            fixture.CurrentCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Chummer Hub Pinned Certificate Test");

        string symbolicCertificatePath = fixture.CertificatePath("current-link.p12");
        File.CreateSymbolicLink(symbolicCertificatePath, fixture.CurrentCertificatePath);
        IConfiguration symbolicConfiguration = fixture.CreateCurrentConfiguration(
            symbolicCertificatePath,
            CertificatePassword);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                symbolicConfiguration,
                fixture.Environment));

        string fifoPath = fixture.CertificatePath("certificate-fifo.p12");
        Assert.AreEqual(0, CreateFifo(fifoPath, Convert.ToUInt32("600", 8)));
        IConfiguration fifoConfiguration = fixture.CreateCurrentConfiguration(
            fifoPath,
            CertificatePassword);
        await Task.Run(() =>
                Assert.ThrowsExactly<InvalidOperationException>(() =>
                    HubDataProtection.Configure(
                        new ServiceCollection(),
                        fifoConfiguration,
                        fixture.Environment)))
            .WaitAsync(TimeSpan.FromSeconds(2));

        IConfiguration deviceConfiguration = fixture.CreateCurrentConfiguration(
            "/dev/null",
            CertificatePassword);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                deviceConfiguration,
                fixture.Environment));
    }

    [TestMethod]
    public async Task Key_ring_fifo_is_rejected_without_blocking()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        WriteRsaCertificate(
            fixture.CurrentCertificatePath,
            keySize: 3072,
            password: CertificatePassword,
            subjectName: "CN=Chummer Hub Key Ring FIFO Test");
        string fifoPath = Path.Combine(fixture.KeysPath, "key-fifo.xml");
        Assert.AreEqual(0, CreateFifo(fifoPath, Convert.ToUInt32("600", 8)));
        IConfiguration configuration = fixture.CreateCurrentConfiguration(
            fixture.CurrentCertificatePath,
            CertificatePassword);

        await Task.Run(() =>
                Assert.ThrowsExactly<InvalidOperationException>(() =>
                    HubDataProtection.Configure(
                        new ServiceCollection(),
                        configuration,
                        fixture.Environment)))
            .WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void Certificate_size_boundary_is_exact_and_oversize_is_rejected_before_allocation()
    {
        RequireLinux();
        using var fixture = new HubDataProtectionFixture();
        const long maximumCertificateBytes = 16L * 1024 * 1024;
        string boundaryPath = fixture.CertificatePath("boundary.p12");
        using (FileStream stream = File.Create(boundaryPath))
            stream.SetLength(maximumCertificateBytes);
        File.SetUnixFileMode(boundaryPath, PrivateFileMode);
        IConfiguration boundaryConfiguration = fixture.CreateCurrentConfiguration(
            boundaryPath,
            CertificatePassword);

        InvalidOperationException boundaryFailure = Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                boundaryConfiguration,
                fixture.Environment));
        StringAssert.Contains(boundaryFailure.Message, "structurally valid PKCS#12");

        string oversizePath = fixture.CertificatePath("oversize.p12");
        using (FileStream stream = File.Create(oversizePath))
            stream.SetLength(maximumCertificateBytes + 1);
        File.SetUnixFileMode(oversizePath, PrivateFileMode);
        IConfiguration oversizeConfiguration = fixture.CreateCurrentConfiguration(
            oversizePath,
            CertificatePassword);

        InvalidOperationException oversizeFailure = Assert.ThrowsExactly<InvalidOperationException>(() =>
            HubDataProtection.Configure(
                new ServiceCollection(),
                oversizeConfiguration,
                fixture.Environment));
        StringAssert.Contains(oversizeFailure.Message, "between 1 and 16777216 bytes");
    }

    private static ServiceProvider CreateProvider(
        HubDataProtectionFixture fixture,
        IConfiguration configuration)
    {
        ServiceCollection services = new();
        HubDataProtection.Configure(services, configuration, fixture.Environment);
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(
        IEnumerable<KeyValuePair<string, string?>> values)
    {
        // Production obtains password values from AddKeyPerFile. An in-memory
        // provider exercises the same IConfiguration keys without writing a
        // second plaintext copy of the password into each test fixture.
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void WriteRsaCertificate(
        string path,
        int keySize,
        string password,
        string subjectName)
    {
        using RSA rsa = RSA.Create(keySize);
        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyEncipherment,
            critical: true));
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));
        WritePkcs12(path, certificate, password);
    }

    private static void WriteEcdsaCertificate(string path, string password)
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var request = new CertificateRequest(
            "CN=ECDSA Chummer Hub Data Protection Test",
            ecdsa,
            HashAlgorithmName.SHA384);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));
        WritePkcs12(path, certificate, password);
    }

    private static void WritePkcs12(
        string path,
        X509Certificate2 certificate,
        string password)
    {
        byte[] pkcs12 = certificate.Export(X509ContentType.Pkcs12, password);
        try
        {
            File.WriteAllBytes(path, pkcs12);
            File.SetUnixFileMode(path, PrivateFileMode);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs12);
        }
    }

    private static void WriteOpenSslUnprotectedPkcs12(
        HubDataProtectionFixture fixture,
        string outputPath,
        bool omitMac)
    {
        string keyPath = fixture.CertificatePath("openssl-nomac.key");
        string certificatePath = fixture.CertificatePath("openssl-nomac.crt");
        string passwordPath = fixture.CertificatePath("openssl-password.txt");
        File.WriteAllText(passwordPath, CertificatePassword);
        File.SetUnixFileMode(passwordPath, PrivateFileMode);

        RunOpenSsl(
            fixture.CertificatesPath,
            "req", "-x509", "-newkey", "rsa:3072", "-sha256", "-nodes",
            "-subj", "/CN=Chummer Hub OpenSSL no-MAC rejection test",
            "-keyout", keyPath,
            "-out", certificatePath,
            "-days", "1");
        if (omitMac)
        {
            RunOpenSsl(
                fixture.CertificatesPath,
                "pkcs12", "-export",
                "-nomac", "-keypbe", "NONE", "-certpbe", "NONE",
                "-inkey", keyPath,
                "-in", certificatePath,
                "-out", outputPath,
                "-passout", $"file:{passwordPath}");
        }
        else
        {
            RunOpenSsl(
                fixture.CertificatesPath,
                "pkcs12", "-export",
                "-keypbe", "NONE", "-certpbe", "NONE",
                "-inkey", keyPath,
                "-in", certificatePath,
                "-out", outputPath,
                "-passout", $"file:{passwordPath}");
        }
        File.SetUnixFileMode(outputPath, PrivateFileMode);
    }

    private static void RunOpenSsl(string workingDirectory, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "openssl",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (string argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        Assert.IsTrue(process.Start(), "OpenSSL did not start for PKCS#12 fixture generation.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(standardOutput, standardError);
        Assert.AreEqual(
            0,
            process.ExitCode,
            $"OpenSSL fixture generation failed (stdout sha256={Digest(standardOutput.Result)}, stderr sha256={Digest(standardError.Result)}).");
    }

    private static string Digest(string value)
        => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static void RequireLinux()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Inconclusive("Hub certificate pinning and Unix-mode contracts require Linux.");
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int CreateFifo(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        uint mode);

    private sealed class HubDataProtectionFixture : IDisposable
    {
        internal HubDataProtectionFixture()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "chummer-hub-data-protection-tests",
                Guid.NewGuid().ToString("N"));
            ContentRoot = Path.Combine(Root, "content");
            ExternalRoot = Path.Combine(Root, "external");
            KeysPath = Path.Combine(ExternalRoot, "keys");
            CertificatesPath = Path.Combine(ExternalRoot, "certificates");
            Directory.CreateDirectory(ContentRoot);
            Directory.CreateDirectory(KeysPath);
            Directory.CreateDirectory(CertificatesPath);
            File.SetUnixFileMode(ContentRoot, PrivateDirectoryMode);
            File.SetUnixFileMode(ExternalRoot, PrivateDirectoryMode);
            File.SetUnixFileMode(KeysPath, PrivateDirectoryMode);
            File.SetUnixFileMode(CertificatesPath, PrivateDirectoryMode);
            Environment = new TestHostEnvironment(ContentRoot);
        }

        internal string Root { get; }

        internal string ContentRoot { get; }

        internal string ExternalRoot { get; }

        internal string KeysPath { get; }

        internal string CertificatesPath { get; }

        internal string CurrentCertificatePath => CertificatePath("current.p12");

        internal string PreviousCertificatePath => CertificatePath("previous.p12");

        internal IHostEnvironment Environment { get; }

        internal string CertificatePath(string fileName)
            => Path.Combine(CertificatesPath, fileName);

        internal IConfiguration CreateCurrentConfiguration(
            string currentCertificatePath,
            string currentCertificatePassword)
            => CreateConfiguration(new Dictionary<string, string?>
            {
                [HubDataProtection.KeysPathConfigurationKey] = KeysPath,
                [HubDataProtection.CertificatePathConfigurationKey] = currentCertificatePath,
                [HubDataProtection.CertificatePasswordConfigurationKey] = currentCertificatePassword
            });

        internal IConfiguration CreateRotationConfiguration(
            string currentCertificatePath,
            string currentCertificatePassword,
            string previousCertificatePath,
            string previousCertificatePassword)
            => CreateConfiguration(new Dictionary<string, string?>
            {
                [HubDataProtection.KeysPathConfigurationKey] = KeysPath,
                [HubDataProtection.CertificatePathConfigurationKey] = currentCertificatePath,
                [HubDataProtection.CertificatePasswordConfigurationKey] = currentCertificatePassword,
                [HubDataProtection.PreviousCertificatePathConfigurationKey] = previousCertificatePath,
                [HubDataProtection.PreviousCertificatePasswordConfigurationKey] = previousCertificatePassword
            });

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Chummer.Hub.Web.Tests";

        public string ContentRootPath { get; set; } = contentRoot;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
