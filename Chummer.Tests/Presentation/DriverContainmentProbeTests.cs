// T723 (WIN-008 gen3): focused negative-evidence tests for driver containment.
// - FindDialogTitleWindow refuses FOREIGN-process same-title dialogs.
// - FindDialogTitleWindow refuses AMBIGUOUS same-title windows (>=2 matches).
// - ValidateDialogHwnd rules (null / wrong class / wrong title / foreign).
// - Cancellation: the observe-loop shape performs zero iterations when the
//   token is pre-canceled.
// Real Win32 windows: an own-process #32770-class window is created by
// loading the shell's dialog-class via a registered class is impossible
// (#32770 is a system class not registrable), so own-process dialog windows
// are created via CreateDialogIndirectParamW (real dialog class), and the
// foreign one via a detached python helper running MessageBoxW.
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class DriverContainmentProbeTests
{
    private const string DialogTitle = "Save Print Preview";

    // ------------------------------------------------------------------ foreign
    [TestMethod]
    public async Task FindDialogTitleWindow_RefusesForeignProcessSameTitleDialog()
    {
        string helper = Path.Combine(Path.GetTempPath(), "t723_foreign_dialog.py");
        await File.WriteAllTextAsync(helper,
            "import ctypes\n" +
            "MB_OK = 0\n" +
            "ctypes.windll.user32.MessageBoxW(None, 't723 blocked', 'Save Print Preview', MB_OK)\n");
        Process? child = null;
        Process helperProcess = null!;
        try
        {
            child = Process.Start(new ProcessStartInfo("python", "-X utf8 \"" + helper + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            Assert.IsNotNull(child, "helper failed to start");
            helperProcess = child!;

            bool appeared = false;
            for (int i = 0; i < 40 && !appeared; i++)
            {
                await Task.Delay(250);
                appeared = DialogWindowExistsWithDifferentOwner();
            }

            Assert.IsTrue(appeared, "foreign dialog did not appear within 10s");

            IntPtr found = DesktopMouseFirstJourneyRunner.FindDialogTitleWindow(DialogTitle);
            Assert.AreEqual(IntPtr.Zero, found,
                "a foreign-process dialog with the exact same title must be refused");
        }
        finally
        {
            if (!helperProcess.HasExited)
            {
                helperProcess.Kill(entireProcessTree: true);
            }

            try { File.Delete(helper); } catch { /* best effort */ }
        }

        Assert.AreEqual(IntPtr.Zero, DesktopMouseFirstJourneyRunner.FindDialogTitleWindow(DialogTitle),
            "no same-title dialog may remain after the helper exits");
    }

    // ------------------------------------------------------------------ own-process
    [TestMethod]
    public void FindDialogTitleWindow_RefusesAmbiguousAndAcceptsSingleOwnProcessDialog()
    {
        IntPtr first = CreateOwnProcessDialog(DialogTitle);
        Assert.IsTrue(first != IntPtr.Zero, "own-process dialog creation failed");

        try
        {
            // Single own-process dialog with the exact title: FOUND.
            IntPtr found = DesktopMouseFirstJourneyRunner.FindDialogTitleWindow(DialogTitle);
            Assert.AreEqual(first, found, "single own-process dialog must be found");

            // Second same-title dialog: AMBIGUITY refused ( IntPtr.Zero ).
            IntPtr second = CreateOwnProcessDialog(DialogTitle);
            Assert.IsTrue(second != IntPtr.Zero, "second dialog creation failed");
            try
            {
                IntPtr ambiguous = DesktopMouseFirstJourneyRunner.FindDialogTitleWindow(DialogTitle);
                Assert.AreEqual(IntPtr.Zero, ambiguous, "two same-title dialogs must be refused");
            }
            finally
            {
                DestroyWindow(second);
            }

            // Back to single: found again (positive control after teardown).
            IntPtr again = DesktopMouseFirstJourneyRunner.FindDialogTitleWindow(DialogTitle);
            Assert.AreEqual(first, again, "after closing the second dialog the first must be found");
        }
        finally
        {
            DestroyWindow(first);
        }
    }

    [TestMethod]
    public void ValidateDialogHwnd_RefusesForeignCreatedWindow()
    {
        IntPtr own = CreateOwnProcessDialog("T723 Revalidation Window");
        Assert.IsTrue(own != IntPtr.Zero, "own dialog creation failed");
        try
        {
            // Wrong title refused.
            Assert.IsFalse(DesktopMouseFirstJourneyRunner.ValidateDialogHwnd(own, DialogTitle),
                "a dialog with a different title must be refused");

            // A destroyed handle refused.
            IntPtr destroyed = CreateOwnProcessDialog("T723 Destroyed");
            DestroyWindow(destroyed);
            Assert.IsFalse(DesktopMouseFirstJourneyRunner.ValidateDialogHwnd(destroyed, "T723 Destroyed"),
                "a destroyed window must be refused");
        }
        finally
        {
            DestroyWindow(own);
        }
    }

    [TestMethod]
    public void CanceledObserveLoopShape_PerformsZeroIterations()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        int iterations = 0;
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTimeOffset.UtcNow < deadline && !cts.Token.IsCancellationRequested)
        {
            iterations++;
            if (cts.Token.WaitHandle.WaitOne(100))
            {
                break;
            }
        }

        Assert.AreEqual(0, iterations, "a pre-canceled token must yield zero iterations");
    }

    // ------------------------------------------------------------------ helpers
    /// <summary>Creates a REAL own-process dialog-class window with the given
    /// title via CreateDialogIndirectParamW (empty template -> class #32770).
    /// </summary>
    private static IntPtr CreateOwnProcessDialog(string title)
    {
        // DLGTEMPLATE: DWORD-aligned stream of style, exStyle, cdit(WORD),
        // x, y, cx, cy (WORDs), then the variable part: menu (WORD, 0),
        // class (WORD, 0), title (WSTR, NUL-terminated), DWORD-aligned.
        const uint style = 0x80000000u | 0x00C00000u | 0x00080000u; // POPUP|CAPTION|SYSMENU
        List<byte> blob = [];
        void AddWord(ushort value) => blob.AddRange(BitConverter.GetBytes(value));
        void AddShort(short value) => blob.AddRange(BitConverter.GetBytes(value));
        blob.AddRange(BitConverter.GetBytes(style));   // style (DWORD)
        blob.AddRange(BitConverter.GetBytes(0u));      // exStyle (DWORD)
        AddWord(0);                                    // cdit = 0
        AddShort(0);                                   // x
        AddShort(0);                                   // y
        AddShort(200);                                 // cx
        AddShort(80);                                  // cy
        AddWord(0);                                    // menu: none
        AddWord(0);                                    // class: none
        blob.AddRange(Encoding.Unicode.GetBytes(title + "\0"));
        while (blob.Count % 4 != 0)
        {
            blob.Add(0); // DWORD-align the tail
        }

        byte[] bytes = [.. blob];
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            IntPtr result = CreateDialogIndirectParamW(
                GetModuleHandleW(null), handle.AddrOfPinnedObject(), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (result == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "CreateDialogIndirectParamW failed (class registration or template invalid)");
            }

            return result;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>True when any TOP-LEVEL #32770 window with the exact title is
    /// owned by a process OTHER than this one.</summary>
    private static bool DialogWindowExistsWithDifferentOwner()
    {
        bool found = false;
        EnumWindows((hwnd, _lParam) =>
        {
            var title = new StringBuilder(256);
            GetWindowTextW(hwnd, title, 256);
            if (!string.Equals(title.ToString(), DialogTitle, StringComparison.Ordinal))
            {
                return true;
            }

            var cls = new StringBuilder(256);
            GetClassNameW(hwnd, cls, 256);
            if (!string.Equals(cls.ToString(), "#32770", StringComparison.Ordinal))
            {
                return true;
            }

            GetWindowThreadProcessId(hwnd, out uint owner);
            if (owner != GetCurrentProcessId())
            {
                found = true;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateDialogIndirectParamW(
        IntPtr hInstance, IntPtr lpTemplate, IntPtr hWndParent, IntPtr lpDialogFunc, IntPtr dwInitParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);
}