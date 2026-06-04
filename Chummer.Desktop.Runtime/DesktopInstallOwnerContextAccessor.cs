using Chummer.Application.Owners;
using Chummer.Contracts.Owners;

namespace Chummer.Desktop.Runtime;

public sealed class DesktopInstallOwnerContextAccessor : IOwnerContextAccessor
{
    private readonly string _headId;

    public DesktopInstallOwnerContextAccessor(string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        _headId = headId.Trim();
    }

    public OwnerScope Current => DesktopInstallLinkingRuntime.ResolveOwnerScope(
        DesktopInstallLinkingRuntime.LoadOrCreateState(_headId));
}
