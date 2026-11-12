using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Chummer.Desktop.Runtime;
using Microsoft.Win32.SafeHandles;

namespace Chummer.Avalonia;

// Only browser-return activation is dispatched to one process. Ordinary
// launches remain free to open another workspace window.
internal sealed class DesktopInstallLinkActivation : IDisposable
{
    private readonly FileStream _lease;
    private readonly CancellationTokenSource _stop = new();
    private readonly NamedPipeServerStream _pipe;
    private readonly Task _listener;

    private DesktopInstallLinkActivation(FileStream lease, string pipeName, Func<string, CancellationToken, Task<bool>> receive)
    {
        _lease = lease;
        _pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _listener = ListenAsync(receive);
    }

    internal static DesktopInstallLinkActivation? TryRegister(string headId, Func<string, CancellationToken, Task<bool>> receive)
    {
        string path = DesktopInstallLinkingRuntime.GetBrowserApprovalActivationLockPath(headId);
        FileStream lease;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            lease = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        try
        {
            return new DesktopInstallLinkActivation(lease, PipeName(path), receive);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static async Task<bool> TryForwardAsync(string headId, string callbackUri)
    {
        // Fail closed before contacting an existing window, then the receiver
        // rechecks currentness on the UI thread before displaying anything.
        if (!DesktopInstallLinkingRuntime.IsCurrentBrowserApprovalReturn(
                DesktopInstallLinkingRuntime.LoadOrCreateState(headId), callbackUri))
            return false;
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(20));
        try
        {
            using NamedPipeClientStream pipe = new(".",
                PipeName(DesktopInstallLinkingRuntime.GetBrowserApprovalActivationLockPath(headId)),
                PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);
            if (OperatingSystem.IsWindows() && GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint processId))
                AllowSetForegroundWindow(processId);
            byte[] bytes = Encoding.UTF8.GetBytes(callbackUri);
            await pipe.WriteAsync(BitConverter.GetBytes(bytes.Length), deadline.Token).ConfigureAwait(false);
            await pipe.WriteAsync(bytes, deadline.Token).ConfigureAwait(false);
            byte[] reply = new byte[1];
            await pipe.ReadExactlyAsync(reply, deadline.Token).ConfigureAwait(false);
            return reply[0] == 1;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
        {
            // Delivery may have happened: do not replay the message or create a
            // competing claim owner after an acknowledgement was lost.
            return false;
        }
    }

    private async Task ListenAsync(Func<string, CancellationToken, Task<bool>> receive)
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await _pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(20));
                byte[] header = new byte[4];
                await _pipe.ReadExactlyAsync(header, deadline.Token).ConfigureAwait(false);
                int length = BitConverter.ToInt32(header);
                if (length is <= 0 or > 2048)
                    continue;
                byte[] bytes = new byte[length];
                await _pipe.ReadExactlyAsync(bytes, deadline.Token).ConfigureAwait(false);
                string uri = new UTF8Encoding(false, true).GetString(bytes);
                bool accepted = await receive(uri, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
                await _pipe.WriteAsync(new byte[] { accepted ? (byte)1 : (byte)0 }, deadline.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException
                or DecoderFallbackException or UnauthorizedAccessException or InvalidOperationException
                or CryptographicException or System.Text.Json.JsonException)
            {
                // No callback data or exception text belongs in logs.
            }
            finally
            {
                try
                {
                    if (!_stop.IsCancellationRequested && _pipe.IsConnected) _pipe.Disconnect();
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
            }
        }
    }

    private static string PipeName(string path)
        => "chummer-install-return-" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(OperatingSystem.IsWindows() ? Path.GetFullPath(path).ToUpperInvariant() : Path.GetFullPath(path))));

    public void Dispose()
    {
        _stop.Cancel();
        _pipe.Dispose();
        _lease.Dispose();
        // The listener owns no state after cancellation; observing completion
        // also prevents disposal failures from becoming unobserved exceptions.
        _ = _listener.ContinueWith(completed =>
        {
            _ = completed.Exception;
            _stop.Dispose();
        }, TaskScheduler.Default);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
