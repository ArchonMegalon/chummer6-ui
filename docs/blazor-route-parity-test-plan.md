# Blazor route and parity test plan

This test plan defines the checks that should prove the promoted Blazor web client behaves like a Chummer desktop client in the browser. It does not replace runtime tests; it defines what those tests must cover.

## Route rendering tests

Required checks:

- `/app` renders the classic Chummer shell.
- `/online` renders the classic Chummer shell as an alias.
- `/online` exposes `data-canonical-route="app"`.
- `/online` exposes `data-route-alias="online"`.
- `/workbench` exposes compatibility posture.
- `/preview` remains preview/debug posture.
- Promoted app routes do not show preview/diagnostic banner chrome.
- Promoted app routes expose `data-route-surface="public-app"`.

## Public home entry tests

Required checks:

- Public hero primary action is `Explore Chummer Online`.
- Public app CTAs route to `app?command=character_roster`.
- Opening the public entry activates the roster workflow.
- Roster workflow updates the titlebar, browser title, toolbar, status strip, shell metadata, and roster pane.

## Desktop shell visual posture tests

Required checks:

- Shell contains titlebar, menu, toolstrip, roster pane, dossier pane, inspector pane, and status strip.
- Top-level promoted route does not render as a modern dashboard/card gallery.
- Menu flyouts use classic desktop menu structure.
- Toolbar actions expose shortcut metadata.
- Dossier tabs expose `aria-current` for active workflow.
- Secondary dossier section list mirrors active workflow.
- Status strip reflects workflow, ruleset, validation, privacy, deployment, and output posture.

## Roster hierarchy tests

Required checks:

- Roster tree exposes `role="tree"`.
- Roster nodes expose stable node IDs, parent IDs, order values, and node kinds.
- Folder nodes are droppable and draggable.
- Runner nodes are draggable.
- Invalid folder moves are rejected by the move engine.
- Keyboard commands and drag/drop use the same move engine.
- Persisted tree restores selected node, parent/order state, and expansion state.
- Analytics events never include runner or folder labels.

## Output workflow tests

Required checks:

- Save command sets `data-active-workflow="save"` and `data-output-workflow="save"`.
- Print command sets `data-active-workflow="print"` and `data-output-workflow="print"`.
- Export command sets `data-active-workflow="export"` and `data-output-workflow="export"`.
- Output target is `local-dossier`, `print-view`, or `download-package` as appropriate.
- File menu and toolbar current states match the active output workflow.
- Output state transitions through requested, ready, or error.

## Auth and return-route tests

Required checks:

- Anonymous promoted route access redirects to login when auth is enabled.
- Return route preserves path and query.
- `/app?command=character_roster` returns to the roster workflow after login.
- `/online` either preserves alias or canonicalizes to `/app` while preserving query state.
- `/workbench` is not used as the normal user return route.
- Owner/session metadata remains coarse and privacy-safe.

## Rybbit analytics tests

Required checks:

- Analytics uses allowlisted shell metadata only.
- Session replay is disabled.
- Autocapture is disabled for the Chummer shell.
- Full URLs and query strings are not sent.
- Runner names, owner IDs, dossier payloads, XML, inventory, notes, and file paths are not sent.
- Route/workflow events include only normalized token values.

## Docker/self-host tests

Required checks:

- Docker/self-host shell exposes `data-self-hostable="true"`.
- Container target is `docker`.
- Base path deployment preserves `/app`, `/online`, `/workbench`, and `/preview` semantics.
- Analytics is disabled by default unless configured.
- Chummer.run-specific defaults are configurable for self-hosting.

## Shared statistics tests

Required checks:

- Blazor renders pending statistics without inventing percentile values.
- Percentile bands come from shared calculation results.
- Recommendation state comes from shared calculation results.
- Risk model outputs include threshold and confidence state.
- Avalonia and Blazor consume the same shared result DTO.
- UI never sends exact private character stats to analytics.

## Completion gate

The Blazor web client should not be called parity-complete until these checks are implemented and passing against the rendered app/runtime behavior.
