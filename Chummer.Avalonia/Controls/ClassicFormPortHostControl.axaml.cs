using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class ClassicFormPortHostControl : UserControl
{
    private readonly IReadOnlyDictionary<string, ClassicFormPortSurfaceControl> _ports;
    private readonly ContentControl? _portContentHost;
    private readonly TextBlock? _portTitleText;
    private readonly TextBlock? _portMetaText;

    public ClassicFormPortHostControl()
    {
        AvaloniaXamlLoader.Load(this);
        _portContentHost = this.FindControl<ContentControl>("PortContentHost");
        _portTitleText = this.FindControl<TextBlock>("PortTitleText");
        _portMetaText = this.FindControl<TextBlock>("PortMetaText");
        _ports = new Dictionary<string, ClassicFormPortSurfaceControl>(StringComparer.OrdinalIgnoreCase)
        {
            ["character_career"] = new CharacterCareerClassicPort(),
            ["character_create"] = new CharacterCreateClassicPort(),
            ["settings"] = new SettingsClassicPort(),
            ["master_index"] = new MasterIndexClassicPort(),
            ["gear"] = new GearClassicPort()
        };
    }

    public void SetState(SectionHostState state, string? selectedCommandId = null)
    {
        string? portId = ClassicModePolicy.TryResolveClassicPortId(state.SectionId, selectedCommandId);
        if (string.IsNullOrWhiteSpace(portId) || !_ports.TryGetValue(portId, out ClassicFormPortSurfaceControl? port))
        {
            IsVisible = false;
            if (_portContentHost is not null)
            {
                _portContentHost.Content = null;
            }
            return;
        }

        IsVisible = true;
        if (_portTitleText is not null)
        {
            _portTitleText.Text = port.SurfaceTitle;
        }

        if (_portMetaText is not null)
        {
            _portMetaText.Text = $"Classic Mode default. FormPort: {port.SurfaceId}. Legacy-first desktop posture.";
        }

        port.SetState(new ClassicFormPortState(
            port.SurfaceId,
            state.SectionId ?? selectedCommandId ?? string.Empty,
            state.ActiveTabId,
            state.ActiveActionId,
            state.Notice,
            state.PreviewJson,
            state.Rows,
            state.QuickActions,
            state.NavigationTabs,
            state.SectionActions));

        if (_portContentHost is not null)
        {
            _portContentHost.Content = port;
        }
    }
}

public sealed record ClassicFormPortState(
    string SurfaceId,
    string RuntimeSectionId,
    string? ActiveTabId,
    string? ActiveActionId,
    string Notice,
    string PreviewJson,
    IReadOnlyList<SectionRowDisplayItem> Rows,
    IReadOnlyList<SectionQuickActionDisplayItem> QuickActions,
    IReadOnlyList<NavigatorTabItem> NavigationTabs,
    IReadOnlyList<NavigatorSectionActionItem> SectionActions);
