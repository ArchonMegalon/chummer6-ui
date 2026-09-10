using System.Diagnostics.CodeAnalysis;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;

namespace Chummer.Desktop.Runtime;

public sealed class DesktopInstallOwnerContextAccessor : IOwnerContextLeaseAccessor
{
    private readonly string _headId;
    private readonly object _leaseSync = new();
    private DesktopInstallOwnerContextLease? _activeLease;

    public DesktopInstallOwnerContextAccessor(string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        _headId = headId.Trim();
    }

    public OwnerScope Current => Capture().Owner;

    public OwnerContextStamp Capture()
    {
        lock (_leaseSync)
        {
            if (_activeLease is not null && _activeLease.TryGetActiveStamp(out OwnerContextStamp activeStamp))
            {
                return activeStamp;
            }
        }

        return ToOwnerContextStamp(DesktopInstallLinkingRuntime.CaptureOwnerContext(_headId));
    }

    public bool TryAcquire(OwnerContextStamp expected, [NotNullWhen(true)] out IOwnerContextLease? lease)
    {
        lease = null;
        if (!expected.IsValid)
        {
            return false;
        }

        lock (_leaseSync)
        {
            if (_activeLease is not null)
            {
                return false;
            }
        }

        if (!DesktopInstallLinkingRuntime.TryAcquireOwnerContext(
                _headId,
                ToAuthorityStamp(expected),
                out IDisposable? authorityLock,
                out DesktopInstallOwnerAuthorityStamp admitted))
        {
            return false;
        }

        DesktopInstallOwnerContextLease acquired = new(ToOwnerContextStamp(admitted), authorityLock!, Release);
        lock (_leaseSync)
        {
            if (_activeLease is not null)
            {
                acquired.Dispose();
                return false;
            }

            _activeLease = acquired;
        }

        lease = acquired;
        return true;
    }

    private static OwnerContextStamp ToOwnerContextStamp(DesktopInstallOwnerAuthorityStamp stamp)
        => new(stamp.Owner, stamp.AuthorityInstanceId, stamp.TransitionRevision);

    private static DesktopInstallOwnerAuthorityStamp ToAuthorityStamp(OwnerContextStamp stamp)
        => new(stamp.Owner, stamp.AuthorityInstanceId, stamp.TransitionRevision);

    private void Release(DesktopInstallOwnerContextLease lease, IDisposable authorityLock)
    {
        lock (_leaseSync)
        {
            if (ReferenceEquals(_activeLease, lease))
            {
                _activeLease = null;
            }
        }

        authorityLock.Dispose();
    }

    private sealed class DesktopInstallOwnerContextLease : IOwnerContextLease
    {
        private readonly OwnerContextStamp _stamp;
        private readonly Action<DesktopInstallOwnerContextLease, IDisposable> _release;
        private IDisposable? _authorityLock;

        public DesktopInstallOwnerContextLease(
            OwnerContextStamp stamp,
            IDisposable authorityLock,
            Action<DesktopInstallOwnerContextLease, IDisposable> release)
        {
            _stamp = stamp;
            _authorityLock = authorityLock;
            _release = release;
        }

        public OwnerContextStamp Stamp
            => _authorityLock is null
                ? throw new ObjectDisposedException(nameof(DesktopInstallOwnerContextLease))
                : _stamp;

        public bool TryGetActiveStamp(out OwnerContextStamp stamp)
        {
            if (_authorityLock is null)
            {
                stamp = default;
                return false;
            }

            stamp = _stamp;
            return true;
        }

        public void Dispose()
        {
            IDisposable? authorityLock = Interlocked.Exchange(ref _authorityLock, null);
            if (authorityLock is not null)
            {
                _release(this, authorityLock);
            }
        }
    }
}
