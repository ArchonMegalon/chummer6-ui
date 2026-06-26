# Blazor classic shell implementation checklist

This checklist tracks the difference between the current shell contract and real runtime behavior. The shell already exposes stable metadata hooks; the remaining work is to bind those hooks to actual services, state, and shared calculation code.

## Route and entry behavior

- Keep `/app` as the canonical public Chummer Online route.
- Keep `/online` as an alias with `data-canonical-route="app"` and `data-route-alias="online"`.
- Keep `/workbench` as compatibility surface, not the primary user route.
- Public home CTAs should open the roster workflow at `app?command=character_roster`.
- Promoted app routes should suppress preview or diagnostic chrome and render the classic shell as the first visible surface.

## Roster hierarchy behavior

- Persist custom folder hierarchy using `data-roster-persistence-key` and schema version.
- Store node identity, parent identity, sibling order, node type, and expansion state.
- Support drag/drop for both folders and runners.
- Prevent invalid drops such as moving a folder into itself or a descendant.
- Implement New Folder, Move, Rename, and Inbox commands from `data-roster-command`.
- Preserve keyboard accessibility for tree selection and command shortcuts.
- Keep `data-roster-selected-node` synchronized between shell root and tree root.

## Dossier and workflow state

- Bind active workflow state to actual command/tab navigation, not only static sample state.
- Keep browser title, desktop titlebar, menu, toolbar, tabs, section strip, and status bar synchronized.
- Replace sample runner values with real workspace/dossier state when available.
- Keep `data-workspace` and `data-active-runner` privacy-safe and tokenized.

## Save, print, and export behavior

- Implement output dialogs keyed by `data-output-workflow`, `data-output-state`, and `data-output-target`.
- Save should update `data-dossier-state` through `unsaved`, `saving`, `saved`, and `error`.
- Print should open a print-safe view without leaking preview or diagnostic route language.
- Export should produce the requested package/download and update `data-dialog-action` when relevant.
- Output failures should be visible through the classic status strip.

## Auth and owner behavior

- Use `data-auth-gate="login-if-anonymous"` to wire promoted-route login gating.
- Preserve route and query during login redirects when `data-auth-return-policy="preserve-route-and-query"`.
- Move `data-session-state` from `local-preview` to `authenticated` or `anonymous` when real auth is connected.
- Keep owner metadata non-identifying; do not expose email, account ID, file path, XML, or runner payload in DOM attributes.

## Privacy and analytics behavior

- Rybbit or other analytics may use route/workflow-only metadata.
- Allowed telemetry scope: route family, route surface, route segment, canonical/alias posture, active workflow, command/tab/control keys, output workflow, deployment posture.
- Forbidden telemetry scope: runner names, owner identifiers, dossier contents, XML, file paths, inventory payload, character stats payload, or free-form user text.
- Keep `data-analytics-scope="route-workflow-only"` as the analytics contract for the shell.

## Docker and hosting behavior

- Keep Chummer.run hosted posture represented by `data-deployment-target="chummer-run"`.
- Keep self-host posture represented by `data-self-hostable="true"` and `data-container-target="docker"`.
- Docker documentation and installer surfaces should reference this route as the web desktop client, not a preview tool.
- Compatibility route must not be promoted as the normal hosted app route.

## Shared calculation and statistics behavior

- Character statistics calculations must live in shared Chummer core, not Blazor component code.
- Avalonia and Blazor should consume the same result model for percentiles, recommendation explanations, risk thresholds, and input assumptions.
- Use `data-calculation-boundary="shared-engine-only"` and `data-result-consumer="blazor-renders-shared-results"` as architectural constraints.
- Recommendation inputs may include spells, inventory, drugs, gear, qualities, and risk models only through shared logic.
- The UI should expose human-readable explanations for percentile/risk recommendations without inventing calculations in Razor.

## Visual fidelity behavior

- The promoted app should continue to look like a dense Chummer desktop client: titlebar, menu, toolstrip, roster tree, dossier tabs, property grids, inspector, status strip.
- Avoid modern dashboard cards for the core app shell.
- Keep compatibility content hidden from promoted app routes.
- Any new command surface should first fit the classic desktop vocabulary before adding web-specific treatment.
