namespace Chummer.Presentation.Overview;

internal static class BuildGhostAlicePacketLoader
{
    internal static async Task<DesktopDialogState> BindCurrentWorkspacePacketAsync(
        DesktopDialogState dialog,
        string? workspaceId,
        string locale,
        Func<string, string, IReadOnlyList<string>, string, CancellationToken, Task<string?>>? loadPacketAsync,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        if (string.IsNullOrWhiteSpace(workspaceId) || loadPacketAsync is null)
        {
            return dialog;
        }

        try
        {
            string? packetJson = await loadPacketAsync(
                workspaceId.Trim(),
                locale,
                BuildGhostAlicePresentation.SupportedContractLocales,
                BuildGhostAlicePresentation.GetDeterministicFallbackText(locale),
                ct).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(packetJson)
                ? dialog
                : BuildGhostAlicePresentation.BindPacket(dialog, packetJson);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Packet loading is advisory. The dialog keeps the explicit,
            // localized deterministic fallback and never invents a payload.
            return dialog;
        }
    }
}
