namespace Chummer.Desktop.Runtime;

public sealed record DesktopStartupCompanionProjection(
    string Headline,
    string Body,
    string BoundaryNote,
    string VoiceStatus,
    string PrimaryActionLabel,
    string SecondaryActionLabel,
    bool VoiceModeEnabled,
    bool IsMacBootstrapGremlin);

public static class DesktopStartupCompanionRuntime
{
    private const string Headline = "You made it. If you said something, I couldn't hear you.";
    private const string BoundaryNote = "Hard boundary: no cross-app observation";
    private const string VoiceModeDisabled = "Voice mode is off. Default posture is text-only until you opt in.";
    private const string VoiceModeEnabled = "Voice mode is on. Push-to-talk stays inside Chummer until you ask for more.";
    private const string EnableVoiceMode = "Enable voice mode";
    private const string KeepTextOnly = "Keep text only";

    public static DesktopStartupCompanionProjection CreateProjection(
        DesktopInstallLinkingState state,
        bool voiceModeEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(state);

        bool isMacBootstrapGremlin = string.Equals(state.Platform, "macos", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state.Platform, "osx", StringComparison.OrdinalIgnoreCase);

        string body = isMacBootstrapGremlin
            ? "Mac bootstrap gremlin is handled. Chummer knows how this copy arrived, and it stays honest about the first-run route."
            : "Chummer knows how this install arrived and keeps first-run help grounded in that route instead of guessing.";

        return new DesktopStartupCompanionProjection(
            Headline,
            body,
            BoundaryNote,
            voiceModeEnabled ? VoiceModeEnabled : VoiceModeDisabled,
            EnableVoiceMode,
            KeepTextOnly,
            voiceModeEnabled,
            isMacBootstrapGremlin);
    }
}
