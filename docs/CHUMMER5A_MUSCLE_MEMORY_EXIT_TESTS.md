# Chummer5A Muscle-Memory Exit Tests

Core rule: if Chummer5A already had the same user-facing function, Chummer6 must preserve the same mouse-driven result with the least possible relearning.

Scope is the full promoted desktop head, not a handful of showcase dialogs:

- every menu strip and toolbar route
- every dialog and utility form
- every workspace panel, grid, tab strip, list, tree, and preview pane
- every section action surface
- every popup menu, flyout, context menu, and tooltip

The exit stack is intentionally layered.

1. Inventory coverage
- Every `tab`, `workspaceAction`, and `desktopControl` from [PARITY_ORACLE.json](/docker/chummercomplete/chummer6-ui/docs/PARITY_ORACLE.json:1) is in scope.
- No dialog, panel, grid, popup, or tooltip is allowed to escape the parity program by omission.
- The runtime receipt must loop through every visible UI element on each captured surface and record control class, label text, tooltip text, layout zone, and mouse-route hints.
- Expected left/right dialog slot ownership and within-slot field order must survive the migration; shuffling fields inside a surviving dialog still breaks muscle memory.

2. Widget-class parity
- Equivalent controls must keep the same control class when the legacy function already existed.
- `ComboBox` must not silently degrade into `TextBox`.
- `ListBox`, `TreeView`, `CheckBox`, `NumericUpDown`, tab strip, button strip, tooltip host, and popup menu posture are all first-class parity signals.

3. Visible copy parity
- Labels, button captions, tab names, menu labels, grid headers, tooltip text, and context-menu text must match legacy naming after trivial punctuation normalization only.
- New synthetic labels are forbidden when legacy already named the same function.

4. Geography parity
- Buttons stay in the same action strip and the same left-to-right order.
- Controls stay in the same row/column rhythm.
- The same pane owns the same job: browse left, inspect right, commit in the familiar action strip.
- Bounds are compared as normalized geometry against the legacy container, not only as raw pixels.
- Expected left/right/hidden layout slots must still resolve to the same pane ownership at runtime.

5. Pointer-route parity
- Left click, double click, right click, middle click, and mouse-wheel behavior are all part of the contract.
- Popup menus, tooltips, and source-open affordances must react on the same pointer route as legacy when the same route existed there.
- If a control exposes a context menu or secondary flyout, the parity receipt must fail when the right-click route disappears or moves behind a different gesture.

6. Mouse-only macro replay
- A veteran should be able to repeat a legacy click path from memory and reach the same result without reading helper chrome.
- The hard proof is a replay receipt: recorded mouse-only traces over the legacy surface and the promoted surface must end in the same selection, mutation, popup, or dialog outcome.
- Seeded replay routes must include real legacy behaviors such as clicking the Master Index source link and double-tapping the Character Roster selection.

The runtime checker should loop through every UI element in scope, not only curated hero controls:

- every visible named control in the promoted visual tree
- every menu item shown after a popup menu opens
- every dialog field, action button, and grid header
- every tooltip and context-menu surface that can change the user’s mouse path

The current strict seed is the `SelectGear` category-selector regression because it is a clean example of the general rule: Chummer5A used a drop-down list, so the successor must not replace that with a free-form text box.
