# Native Windows Essential adaptation

Reference inspected 2026-11-23: https://essential-typeui-01b26b54.vercel.app/components

This is a native Avalonia adaptation, not an embedded website. No Android styles or provider/auth behavior are changed.

## Component mapping

| Verified Essential reference | Native implementation |
|---|---|
| Brand #256EFF, medium #5288FF, strong #1A5CE6 | Primary action normal/hover/pressed states; selected/focus semantic brushes |
| White primary/secondary surfaces, #f7f7f7 tertiary, #e0e0e0 border | Light shell semantic resources |
| 8px controls; 16px panels/cards | `EssentialControls.axaml` buttons/inputs/selects/cards, `DesktopShellTheme.CreateSurface` |
| 4px spacing grid; 12/16px input/action insets | Shared control styles; dense desktop menus/status retain compact native spacing |
| Body small 14px, h5 18px | Actions/inputs and section headings; toolbar/menu fonts retain desktop density |
| Primary / secondary / quiet actions | Existing `.primary` / default / `.quiet` semantic classes retained |
| Disabled, hover, pressed, keyboard focus, error states | Native pseudoclasses, Fluent template interaction and explicit focus/error tokens |
| Form inputs and selects | Existing native TextBox/ComboBox behavior, keyboard navigation and automation IDs retained |

Dark uses semantic dark equivalents rather than applying light surfaces globally. Full gallery-component coverage and all-window visual acceptance must be assessed separately; this layer is not evidence that every surface has been reviewed.

## Appearance behavior

The existing persisted `DesktopPreferenceState.Theme` is the single source of truth. Windows settings offer Light, Dark and System (Windows). Existing legacy values remain available and render as before (dark); they are not silently rewritten. System uses Avalonia `ThemeVariant.Default`, which follows the platform theme while running. The preference is loaded before the first desktop window and applied after accepted preference updates. No windows, workspaces or input controls are recreated to switch appearance.

Programmatic input brushes and local Fluent resource aliases follow resource changes. Resource alias updates must guard against writing the identical brush into their own notification scope, or they recurse. The first failing regression log is retained outside source as evidence.
