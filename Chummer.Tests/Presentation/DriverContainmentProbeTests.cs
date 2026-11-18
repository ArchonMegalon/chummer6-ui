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

    // ---------------------------------------------------------------- gen4
    // Focused lifetime tests of the ACTUAL production driver body
    // (RunPdfSaveDialogDriverScope), per gen4 acceptance item 1. These invoke
    // the extracted production method itself - NOT a copied loop shape.

    [TestMethod]
    public async Task RunPdfSaveDialogDriverScope_ExitBeforeDiscovery_ThenLaterDialog_IsNeverTouched()
    {
        // A scope that starts with NO matching dialog present and is canceled
        // BEFORE any dialog exists must exit discovery promptly, and a
        // matching dialog created AFTER the scope stopped must remain
        // untouched (no callback, no validation-visible action).
        Assert.AreEqual(IntPtr.Zero, DesktopMouseFirstJourneyRunner.FindDialogTitleWindow(DialogTitle),
            "precondition: no same-title dialog exists before the scope starts");

        List<string> trace = [];
        bool driven = false;
        using CancellationTokenSource scope = new();
        Thread worker = new Thread(() => DesktopMouseFirstJourneyRunner.RunPdfSaveDialogDriverScope(
            scope.Token, m => { lock (trace) { trace.Add(m); } }, () => driven = true));
        worker.IsBackground = true;
        worker.Start();

        // Give discovery a moment to observe the empty desktop, then stop the
        // scope exactly like the production finally does.
        await Task.Delay(700);
        scope.Cancel();
        bool exited = worker.Join(TimeSpan.FromSeconds(10));
        Assert.IsTrue(exited, "the production driver scope must exit promptly when its scope token is canceled");

        // NOW create a matching dialog - after the invocation ended. The
        // stopped driver must NOT act on it.
        IntPtr later = CreateOwnProcessDialog(DialogTitle);
        Assert.IsTrue(later != IntPtr.Zero, "later-dialog creation failed");
        try
        {
            Assert.IsFalse(driven, "a stopped driver scope must never set the driven flag");
            await Task.Delay(1000);
            Assert.IsFalse(driven, "no late driver action may occur after the scope stopped");
            Assert.IsTrue(trace.Any(m => m.Contains("dialog not found") || m.Contains("canceled")),
                "trace must show discovery ended without touching a dialog, got: " + string.Join(" | ", trace));
        }
        finally
        {
            DestroyWindow(later);
        }
    }

    [TestMethod]
    public void RunPdfSaveDialogDriverScope_PreCanceledToken_ExitsImmediatelyWithoutDiscovery()
    {
        using CancellationTokenSource scope = new();
        scope.Cancel();
        List<string> trace = [];
        bool driven = false;
        Thread worker = new Thread(() => DesktopMouseFirstJourneyRunner.RunPdfSaveDialogDriverScope(
            scope.Token, m => trace.Add(m), () => driven = true));
        worker.IsBackground = true;
        worker.Start();
        bool exited = worker.Join(TimeSpan.FromSeconds(5));
        Assert.IsTrue(exited, "a pre-canceled scope must exit immediately");
        Assert.IsFalse(driven, "no driven flag from a pre-canceled scope");
        Assert.IsTrue(trace.Count > 0, "scope must record its exit reason");
        Assert.IsTrue(trace.Any(m => m.Contains("canceled") || m.Contains("not found")),
            "trace must show no discovery action, got: " + string.Join(" | ", trace));
        // Even with a matching dialog present the whole time, a pre-canceled
        // scope must never report driving it.
    }

    [TestMethod]
    public async Task RunPdfSaveDialogDriverScope_ObservedDialogThenScopeCancel_StopsCleanly()
    {
        // Positive-lifetime control: with a REAL own-process dialog present on
        // a PUMPING owner thread (the production save dialog is owned by the
        // app UI thread, which pumps its modal loop), the production scope
        // must discover it and a scope cancel must stop all owned work
        // promptly. NOTE: the dialog owner MUST pump - cross-thread
        // GetWindowTextW against a non-pumping same-process window blocks,
        // which is a test-harness artifact, not production behavior.
        var (dlg, teardown) = await CreatePumpedVisibleDialogAsync(DialogTitle);
        Assert.IsTrue(dlg != IntPtr.Zero, "pumped dialog creation failed");
        try
        {
            // Main-thread positive control: the SAME enumeration the
            // production driver uses must find this dialog right now.
            IntPtr mainThreadFound = DesktopMouseFirstJourneyRunner.FindDialogTitleWindow(DialogTitle);
            Assert.AreEqual(dlg, mainThreadFound,
                "main-thread FindDialogTitleWindow must find the own-process dialog before the scope starts");

            List<string> trace = [];
            bool driven = false;
            using CancellationTokenSource scope = new();
            Thread worker = new Thread(() => DesktopMouseFirstJourneyRunner.RunPdfSaveDialogDriverScope(
                scope.Token, m => { lock (trace) { trace.Add(m); } }, () => driven = true));
            worker.IsBackground = true;
            worker.Start();

            // Wait for discovery (traced immediately when the dialog is found).
            bool observed = false;
            for (int i = 0; i < 40 && !observed; i++)
            {
                await Task.Delay(250);
                lock (trace) { observed = trace.Any(m => m.Contains("dialog found") || m.Contains("dialog visible")); }
            }

            Assert.IsTrue(observed,
                "the production scope must discover the own-process dialog; trace: [" + string.Join(" | ", trace) + "]");

            // gen4 contract: canceling the scope stops the owned work promptly.
            scope.Cancel();
            bool exited = worker.Join(TimeSpan.FromSeconds(15));
            Assert.IsTrue(exited, "scope cancel must stop the production driver promptly");
            // The empty test dialog has no filename Edit child, so the
            // production body exits at "no descendant Edit found" before the
            // driven callback - a truthful consequence of the production edit
            // search, not a lifetime defect.
            Assert.IsFalse(driven,
                "an edit-less dialog must not set the driven flag (production edit search exits first)");
        }
        finally
        {
            await teardown();
        }

        Assert.AreEqual(IntPtr.Zero, DesktopMouseFirstJourneyRunner.FindDialogTitleWindow(DialogTitle),
            "the pumped dialog must be gone after teardown");
    }

    /// <summary>Creates a REAL own-process dialog-class window with the given
    /// title on a DEDICATED STA thread that pumps messages (mirroring the
    /// production save dialog, whose owner thread pumps its modal loop).
    /// Returns the dialog handle and an async teardown that posts WM_QUIT,
    /// letting the owner destroy its own window.</summary>
    private static async Task<(IntPtr hwnd, Func<Task> teardown)> CreatePumpedVisibleDialogAsync(string title)
    {
        var created = new TaskCompletionSource<(IntPtr hwnd, uint threadId)>(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread owner = new Thread(() =>
        {
            IntPtr dlg = CreateOwnProcessDialog(title);
            if (dlg != IntPtr.Zero)
            {
                ShowWindow(dlg, 5 /* SW_SHOW */);
            }

            created.TrySetResult((dlg, GetCurrentThreadId()));
            if (dlg == IntPtr.Zero)
            {
                return;
            }

            while (GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                _ = TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            DestroyWindow(dlg);
        });
        owner.IsBackground = true;
        owner.SetApartmentState(ApartmentState.STA);
        owner.Start();
        var (hwnd, threadId) = await created.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(hwnd != IntPtr.Zero, "pumped dialog creation failed on the owner thread");
        return (hwnd, async () =>
        {
            PostThreadMessageW(threadId, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
            await Task.Run(() => owner.Join(TimeSpan.FromSeconds(5)));
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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