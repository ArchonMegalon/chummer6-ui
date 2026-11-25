using System.Net.Http.Json;
using System.Text.Json;
using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Workspaces;

namespace Chummer.Desktop.Runtime;

public static class DesktopWorkspaceOriginBridge
{
    public static async Task EnrollAsync(string headId, FileWorkspaceStore store,
        IOwnerContextLeaseAccessor owners, OwnerContextStamp stamp, Guid originalOperation, CancellationToken ct = default)
    {
        using var transport = new Transport(headId, owners, stamp);
        await WorkspaceOriginEnrollmentClient.EnrollAsync(store.WorkspaceOriginTrust, owners, stamp, transport.InstallationId,
            originalOperation, transport, ct);
    }
    public static async Task<FileWorkspaceStore.OriginTrustDomain.VerifiedOriginEnrollment> AuthorizeHistoryAsync(
        string headId, FileWorkspaceStore store, IOwnerContextLeaseAccessor owners,
        OwnerContextStamp stamp, Guid operation, string exactHistoryDigest, CancellationToken ct = default)
    {
        using var transport = new Transport(headId, owners, stamp);
        await WorkspaceOriginEnrollmentClient.EnsureEnrolledAsync(store.WorkspaceOriginTrust, owners, stamp,
            transport.InstallationId, transport.GrantId, transport, ct);
        return await WorkspaceOriginEnrollmentClient.RefreshAsync(store.WorkspaceOriginTrust, owners, stamp, transport.InstallationId,
            operation, exactHistoryDigest, transport, ct);
    }
    public static async Task<WorkspaceOriginPublicationProof> PreparePublicationAsync(string headId,
        FileWorkspaceStore store, IOwnerContextLeaseAccessor owners, OwnerContextStamp stamp, Guid publicationOperation,
        ReadOnlyMemory<byte> bytes, WorkspaceRuleEnvironmentTransfer transfer, CancellationToken ct = default)
    {
        string history = store.InspectOriginHistoryDigest(bytes, transfer.Binding) ?? "";
        var verified = await AuthorizeHistoryAsync(headId, store, owners, stamp, Guid.NewGuid(), history, ct);
        return store.WorkspaceOriginTrust.CreatePublicationProof(owners, stamp, verified, publicationOperation, transfer, history);
    }

    public static async Task<WorkspaceOriginPublicationProof> AuthorizeRecoveryAsync(string headId,
        FileWorkspaceStore store, IOwnerContextLeaseAccessor owners, OwnerContextStamp stamp, Guid originalOperation,
        WorkspaceRuleEnvironmentTransfer originalTransfer, CancellationToken ct = default)
    {
        var verified = await AuthorizeHistoryAsync(headId, store, owners, stamp, Guid.NewGuid(), "", ct);
        return store.WorkspaceOriginTrust.CreatePublicationProof(owners, stamp, verified, originalOperation, originalTransfer, "");
    }

    private sealed class Transport : IWorkspaceOriginEnrollmentTransport, IDisposable
    {
        private readonly string _head;
        private readonly IOwnerContextLeaseAccessor _owners;
        private readonly OwnerContextStamp _stamp;
        private readonly DesktopOriginGrant _grant;
        private readonly HttpClient _client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            { BaseAddress = new Uri("https://chummer.run/"), Timeout = TimeSpan.FromSeconds(30) };
        internal string InstallationId => _grant.InstallationId;
        internal string GrantId => _grant.GrantId;
        internal Transport(string head, IOwnerContextLeaseAccessor owners, OwnerContextStamp stamp)
        {
            _head = head; _owners = owners; _stamp = stamp;
            _grant = DesktopInstallLinkingRuntime.CaptureWorkspaceOriginGrant(head, stamp);
            Check();
        }
        private void Check()
        {
            if (_owners.Capture() != _stamp
                || DesktopInstallLinkingRuntime.CaptureWorkspaceOriginGrant(_head, _stamp) != _grant)
                throw new UnauthorizedAccessException("origin-owner-or-grant-changed");
        }
        public async Task<T> SendAsync<T>(string action, WorkspaceOriginRequest request, WorkspaceOriginProof? proof, CancellationToken ct)
        {
            if (action is not ("begin" or "prove" or "recover" or "snapshot")) throw new InvalidDataException();
            Check();
            using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/install-linking/origins/" + action)
            { Content = JsonContent.Create(new { installationId = InstallationId, request, proof, accessToken = _grant.AccessToken },
                options: WorkspaceOriginProtocol.Json) };
            using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            Check();
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > WorkspaceOriginProtocol.MaximumWireBytes)
                throw new IOException("origin-authority-refused; reconcile original operation");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var body = new MemoryStream();
            byte[] buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) != 0)
            {
                if (body.Length + read > WorkspaceOriginProtocol.MaximumWireBytes) throw new InvalidDataException();
                body.Write(buffer, 0, read);
            }
            Check();
            return JsonSerializer.Deserialize<T>(body.ToArray(), WorkspaceOriginProtocol.Json) ?? throw new InvalidDataException();
        }
        public void Dispose() => _client.Dispose();
    }
}
