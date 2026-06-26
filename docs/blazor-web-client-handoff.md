# Blazor web client implementation handoff

This handoff summarizes the current Blazor/Chummer Online web-client work. It is not a completion claim. The current state has UI shell work and contracts, but runtime behavior still needs implementation and verification.

## Current direction

The promoted web client should behave like another Chummer desktop client in the browser.

- Canonical app route: `/app`
- Alias app route: `/online`
- Compatibility route: `/workbench`
- Preview/debug route: `/preview`

The promoted app route should open directly into the classic desktop shell and suppress preview or diagnostic chrome.

## Primary files touched

Blazor shell/UI:

- `Chummer.Blazor/Components/Pages/Preview.razor`
- `Chummer.Blazor/Components/Pages/Preview.razor.css`
- `Chummer.Blazor/Components/Pages/Home.razor`
- `Chummer.Blazor/Components/App.razor`

Docs added/updated:

- `docs/blazor-web-client-design-index.md`
- `docs/blazor-classic-shell-contract.md`
- `docs/blazor-classic-shell-implementation-checklist.md`
- `docs/blazor-roster-hierarchy-contract.md`
- `docs/blazor-rybbit-analytics-contract.md`
- `docs/blazor-docker-self-host-contract.md`
- `docs/blazor-auth-return-route-contract.md`
- `docs/blazor-route-parity-test-plan.md`
- `docs/shared-character-statistics-contract.md`

## Current shell state

`Preview.razor` now registers the classic shell for:

- `/preview`
- `/app`
- `/online`
- `/workbench`

Promoted app routes use the classic desktop shell with Chummer-like:

- titlebar
- menu/flyouts
- toolstrip
- roster tree
- dossier tabs
- dossier form/grid surface
- inspector panel
- status strip

The shell root exposes extensive stable metadata for:

- route family, segment, alias, canonical route, and surface
- active workflow
- command/tab/control/dialog/fixture/legacy runner deep links
- output workflow/state/target
- workspace/runner/roster selection
- dossier state/storage
- ruleset/validation
- privacy/analytics
- hosting/Docker/self-host posture
- auth/session/owner posture
- web-desktop parity posture
- shared calculation/statistics posture

## Public home state

`Home.razor` routes public app CTAs to:

```text
app?command=character_roster
```

The visible copy has been shifted away from preview or diagnostic language and toward Chummer Online product workflow language.

## Important caveat

The current work is intentionally not verified in this handoff. The shell contains many contract hooks and CSS/markup changes, but runtime build, routing, rendering, and browser behavior must still be validated before claiming completion.

## Recommended next implementation order

1. Validate the Blazor project builds.
2. Render `/app?command=character_roster` locally and inspect first paint.
3. Fix any Razor/CSS issues introduced by the shell markup changes.
4. Add route tests for `/app`, `/online`, `/workbench`, and `/preview`.
5. Implement roster move engine from `docs/blazor-roster-hierarchy-contract.md`.
6. Persist roster hierarchy per workspace/owner scope.
7. Wire auth return-route behavior from `docs/blazor-auth-return-route-contract.md`.
8. Wire Save/Print/Export output state transitions.
9. Implement Rybbit allowlist mapping from shell metadata only.
10. Define shared character statistics DTOs outside Blazor.
11. Render shared statistics results in Blazor and Avalonia from the same DTO.

## Do not regress

- Do not make `/workbench` the normal public app route.
- Do not show preview or diagnostic chrome above `/app` or `/online`.
- Do not send private character data to Rybbit.
- Do not calculate character statistics in Razor.
- Do not store roster hierarchy by scraping visible labels.
- Do not hardcode Chummer.run-only behavior into self-host paths.
- Do not replace the desktop shell with generic web-dashboard cards.

## Validation still required

Before marking the broader goal complete, verify against:

- `docs/blazor-route-parity-test-plan.md`
- `docs/blazor-classic-shell-implementation-checklist.md`
- actual rendered app behavior
- route/auth/output/runtime tests
- privacy/analytics checks
