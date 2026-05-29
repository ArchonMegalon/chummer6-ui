using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class ClassicFormPortHostControl : UserControl
{
    private readonly IReadOnlyDictionary<string, ClassicFormPortSurfaceControl> _ports;
    private readonly ContentControl? _portContentHost;
    private readonly TextBlock? _portTitleText;
    private readonly TextBlock? _portMetaText;
    private readonly TextBlock? _portModeChipText;

    public ClassicFormPortHostControl()
    {
        AvaloniaXamlLoader.Load(this);
        _portContentHost = this.FindControl<ContentControl>("PortContentHost");
        _portTitleText = this.FindControl<TextBlock>("PortTitleText");
        _portMetaText = this.FindControl<TextBlock>("PortMetaText");
        _portModeChipText = this.FindControl<TextBlock>("PortModeChipText");
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
            _portMetaText.Text = $"Legacy form: {ResolveLegacyDesignerLabel(port.SurfaceId)}. Route: {state.SectionId ?? selectedCommandId ?? port.SurfaceId}.";
        }

        if (_portModeChipText is not null)
        {
            _portModeChipText.Text = "Classic Default";
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

    private static string ResolveLegacyDesignerLabel(string surfaceId)
        => surfaceId switch
        {
            "character_career" => "CharacterCareer.Designer.cs",
            "character_create" => "CharacterCreate.Designer.cs",
            "settings" => "EditGlobalSettings.Designer.cs",
            "master_index" => "MasterIndex.Designer.cs",
            "gear" => "SelectGear.Designer.cs",
            _ => surfaceId,
        };
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
