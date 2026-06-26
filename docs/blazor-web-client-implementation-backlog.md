# Blazor web client implementation backlog

This backlog turns the Blazor web-client contracts into concrete implementation slices. It is ordered to protect route correctness, visual fidelity, privacy, and shared-runtime boundaries before adding deeper behavior.

## P0: Make the promoted shell verifiably render

Goal: `/app` and `/online` must render the classic Chummer shell without preview or diagnostic chrome.

Acceptance criteria:

- `/app?command=character_roster` renders a visible classic shell.
- `/online?command=character_roster` renders the same shell as an alias.
- `/workbench` remains compatibility posture.
- Browser title and shell title show active workflow.
- No preview/diagnostic banner appears on promoted routes.

Primary files:

- `Chummer.Blazor/Components/Pages/Preview.razor`
- `Chummer.Blazor/Components/Pages/Preview.razor.css`
- `Chummer.Blazor/Components/Pages/Home.razor`

## P1: Add route and shell tests

Goal: lock down app, alias, compatibility, and preview route posture.

Acceptance criteria:

- Tests assert `data-chummer-classic-shell="true"` on `/app` and `/online`.
- Tests assert `data-canonical-route="app"` on `/online`.
- Tests assert `data-route-surface="public-app"` on promoted routes.
- Tests assert `/workbench` is compatibility, not promoted public app.
- Tests assert public home CTAs route to `app?command=character_roster`.

Reference:

- `docs/blazor-route-parity-test-plan.md`

## P2: Implement roster hierarchy state model

Goal: replace static sample roster with a real model that can persist custom folders and runner placement.

Acceptance criteria:

- Roster nodes are modeled as a flat persisted node list.
- Folder and runner nodes expose stable IDs, parent IDs, order, and kind.
- Selected node is synchronized with shell metadata.
- Expanded folder state is preserved.

Reference:

- `docs/blazor-roster-hierarchy-contract.md`

## P3: Implement roster move engine

Goal: drag/drop and keyboard moves use the same validated operation.

Acceptance criteria:

- Move operation accepts source node, target parent, target order, and move mode.
- Invalid moves are rejected with visible blocked state.
- Folder-to-descendant moves are blocked.
- Runner-under-runner moves are blocked.
- Reordering preserves deterministic order.

Reference:

- `docs/blazor-roster-hierarchy-contract.md`

## P4: Wire auth return-route behavior

Goal: promoted app routes can require login without losing the requested workflow.

Acceptance criteria:

- Anonymous `/app?command=character_roster` redirects to login when auth is enabled.
- Return route preserves path and query.
- After login, shell restores active workflow.
- `/online` alias behavior remains canonical or preserved as configured.
- Owner/session shell metadata updates from real auth state.

Reference:

- `docs/blazor-auth-return-route-contract.md`

## P5: Wire output workflows

Goal: Save, Print, and Export become real desktop-style output flows.

Acceptance criteria:

- Save updates dossier state through saving/saved/error.
- Print opens a print-safe view.
- Export creates a download package or error state.
- Output dialogs use `data-output-workflow`, `data-output-state`, `data-output-target`, and `data-dialog-action`.
- Toolbar, File menu, status bar, and page title stay synchronized.

Reference:

- `docs/blazor-classic-shell-contract.md`

## P6: Implement Rybbit allowlist mapping

Goal: analytics may report route/workflow posture only.

Acceptance criteria:

- Analytics properties are built from an allowlist of shell metadata.
- Session replay remains disabled.
- Autocapture remains disabled for the shell.
- Full URLs and raw query strings are never sent.
- Forbidden private values are covered by tests/checks.

Reference:

- `docs/blazor-rybbit-analytics-contract.md`

## P7: Harden Docker/self-host deployment

Goal: the web client runs on Chummer.run and self-hosted Docker with the same route semantics.

Acceptance criteria:

- Base path deployment preserves `/app`, `/online`, `/workbench`, and `/preview` semantics.
- Analytics defaults to disabled unless configured.
- Rybbit settings are configurable.
- Auth mode is configurable for private/self-hosted deployments.
- No Chummer.run-only dependency is required for shell rendering.

Reference:

- `docs/blazor-docker-self-host-contract.md`

## P8: Define shared statistics DTOs

Goal: character statistics and recommendations are computed outside Blazor and reusable by Avalonia.

Acceptance criteria:

- DTOs live in a non-Blazor shared project.
- Result model includes metric results, percentile bands, cohort summary, recommendations, risks, evidence, and privacy level.
- Blazor renders pending/ready/error states from the DTO.
- Avalonia can consume the same DTO.

Reference:

- `docs/shared-character-statistics-contract.md`

## P9: Implement initial shared statistics calculations

Goal: produce the first real explainable percentile/risk recommendation.

Acceptance criteria:

- Shared logic computes at least one deterministic metric.
- Percentile band mapping is tested.
- Risk threshold probability is tested.
- Recommendation output includes assumptions, inputs, risk, and evidence IDs.
- Blazor does not invent percentile/probability values in Razor.

Reference:

- `docs/shared-character-statistics-contract.md`

## P10: Polish visual fidelity after behavior works

Goal: preserve Chummer desktop feel while replacing static placeholder data with real state.

Acceptance criteria:

- Classic titlebar/menu/toolstrip/roster/dossier/inspector/status strip remain visually coherent.
- No modern card-dashboard regressions in the promoted route.
- Mobile layout remains usable without abandoning desktop-client identity.
- Compatibility and diagnostic content stays out of promoted route first paint.

Reference:

- `docs/blazor-classic-shell-contract.md`
- `docs/blazor-classic-shell-implementation-checklist.md`
