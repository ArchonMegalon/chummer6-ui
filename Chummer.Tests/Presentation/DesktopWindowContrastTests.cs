using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
[DoNotParallelize]
public sealed class DesktopWindowContrastTests
{
    private static readonly object HeadlessInitLock = new();
    private static bool _headlessInitialized;
    private const int HeadlessSessionAttempts = 3;

    [TestMethod]
    public void Install_linking_window_keeps_idle_input_controls_readable_in_dark_mode()
    {
        WithStandaloneInstallLinkingWindow(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleInputControlContrast(window, "install linking window dark mode", minimumVisibleInputControls: 1);
        });
    }

    [TestMethod]
    public void Report_issue_window_keeps_idle_input_controls_readable_in_dark_mode()
    {
        WithStandaloneReportIssueWindow(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleInputControlContrast(window, "report issue window dark mode", minimumVisibleInputControls: 4);
        });
    }

    private static void EnsureHeadlessPlatform()
    {
        lock (HeadlessInitLock)
        {
            if (_headlessInitialized)
            {
                return;
            }

            _headlessInitialized = true;
        }
    }

    private static void WithStandaloneInstallLinkingWindow(Action<Window> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            Window window = new DesktopInstallLinkingWindow(CreateInstallLinkingStartupContext())
                            {
                                Width = 880,
                                Height = 540
                            };
                            window.Show();
                            PumpUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                session?.Dispose();
            }
        }

        throw new AssertFailedException("Avalonia install-linking headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static void WithStandaloneReportIssueWindow(Action<Window> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            ConstructorInfo constructor = typeof(DesktopReportIssueWindow).GetConstructor(
                                BindingFlags.Instance | BindingFlags.NonPublic,
                                binder: null,
                                [
                                    typeof(DesktopInstallLinkingState),
                                    typeof(DesktopUpdateClientStatus),
                                    typeof(DesktopPreferenceState)
                                ],
                                modifiers: null)
                                ?? throw new AssertFailedException("DesktopReportIssueWindow private constructor was not found.");

                            Window window = (Window)constructor.Invoke(
                                [
                                    CreateInstallState(status: "claimed"),
                                    CreateUpdateStatus(),
                                    DesktopPreferenceState.Default
                                ]);

                            window.Show();
                            PumpUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                session?.Dispose();
            }
        }

        throw new AssertFailedException("Avalonia report-issue headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static DesktopInstallLinkingStartupContext CreateInstallLinkingStartupContext()
    {
        return new DesktopInstallLinkingStartupContext(
            State: CreateInstallState(status: "guest") with
            {
                ClaimedAtUtc = null,
                LinkedEmail = null,
                LastClaimCode = "RUNNER-CLAIM-42"
            },
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "fresh_install");
    }

    private static DesktopInstallLinkingState CreateInstallState(string status)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopInstallLinkingState(
            InstallationId: "install-contrast-test",
            HeadId: "avalonia",
            ApplicationVersion: "run-test",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            Status: status,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 1,
            LastStartedAtUtc: now,
            ClaimedAtUtc: string.Equals(status, "claimed", StringComparison.Ordinal) ? now : null,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public",
            PrivateKey: "private",
            GrantToken: "grant-token");
    }

    private static DesktopUpdateClientStatus CreateUpdateStatus()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopUpdateClientStatus(
            HeadId: "avalonia",
            InstalledVersion: "run-test",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: true,
            AutoApply: true,
            ManifestLocation: "/tmp/chummer-release.json",
            LastCheckedAtUtc: now,
            LastManifestVersion: "run-test",
            LastManifestPublishedAtUtc: now,
            LastError: null,
            Status: "current",
            RecommendedAction: "Continue.");
    }

    private static void AssertVisibleInputControlContrast(Control root, string context, int minimumVisibleInputControls)
    {
        Control[] inputControls = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.IsVisible)
            .Where(static control => control is ComboBox or ListBox or TextBox or NumericUpDown)
            .ToArray();

        Assert.IsTrue(
            inputControls.Length >= minimumVisibleInputControls,
            $"{context} should expose enough themed input controls for a meaningful non-hover readability check.");

        foreach (Control control in inputControls)
        {
            (IBrush? foregroundBrush, IBrush? backgroundBrush) = control switch
            {
                ComboBox comboBox => (comboBox.Foreground, comboBox.Background),
                ListBox listBox => (listBox.Foreground, listBox.Background),
                TextBox textBox => (textBox.Foreground, textBox.Background),
                NumericUpDown numericUpDown => (numericUpDown.Foreground, numericUpDown.Background),
                _ => (null, null)
            };

            Color foreground = ResolveSolidColor(foregroundBrush, control, "foreground", context);
            Color background = ResolveSolidColor(backgroundBrush, control, "background", context);
            string controlName = string.IsNullOrWhiteSpace(control.Name) ? control.GetType().Name : control.Name!;
            AssertContrastAtLeast(foreground, background, 4.5d, $"{context} {controlName} non-hover text");
        }
    }

    private static Color ResolveSolidColor(IBrush? brush, Control control, string role, string context)
    {
        if (brush is ISolidColorBrush solidBrush)
        {
            return solidBrush.Color;
        }

        throw new AssertFailedException($"{context} {control.Name ?? control.GetType().Name} must expose a solid {role} brush.");
    }

    private static void AssertContrastAtLeast(Color foreground, Color background, double minimum, string context)
    {
        double ratio = ContrastRatio(foreground, background);
        Assert.IsTrue(ratio >= minimum, $"Expected {context} contrast to be at least {minimum:0.0}, but was {ratio:0.00}.");
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        double foregroundLuminance = RelativeLuminance(foreground);
        double backgroundLuminance = RelativeLuminance(background);
        double lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        double darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(double value)
        {
            double normalized = value / 255d;
            return normalized <= 0.03928d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        double red = Channel(color.R);
        double green = Channel(color.G);
        double blue = Channel(color.B);
        return (0.2126d * red) + (0.7152d * green) + (0.0722d * blue);
    }

    private static bool IsTransientHeadlessFailure(Exception ex)
    {
        string message = ex.ToString();
        return message.Contains("The visual is not attached to a visual tree", StringComparison.Ordinal)
            || message.Contains("Call from invalid thread", StringComparison.Ordinal)
            || message.Contains("Operation is not valid due to the current state of the object", StringComparison.Ordinal);
    }

    private static void PumpUi()
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class ContrastHeadlessAppBootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .ConfigureFonts(static fontManager => fontManager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "fonts:Inter#Inter"
                })
                .WithInterFont();
        }
    }

    private sealed class ThemeScope : IDisposable
    {
        private readonly ThemeVariant _priorAppTheme;
        private readonly ThemeVariant _priorWindowTheme;
        private readonly Window _window;

        private ThemeScope(Window window)
        {
            _window = window;
            _priorAppTheme = global::Avalonia.Application.Current?.RequestedThemeVariant ?? ThemeVariant.Light;
            _priorWindowTheme = window.RequestedThemeVariant;
        }

        public static ThemeScope Dark(Window window)
        {
            ThemeScope scope = new(window);
            if (global::Avalonia.Application.Current is not null)
            {
                global::Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }

            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.InvalidateVisual();
            PumpUi();
            return scope;
        }

        public void Dispose()
        {
            if (global::Avalonia.Application.Current is not null)
            {
                global::Avalonia.Application.Current.RequestedThemeVariant = _priorAppTheme;
            }

            _window.RequestedThemeVariant = _priorWindowTheme;
            _window.InvalidateVisual();
            PumpUi();
        }
    }
}
