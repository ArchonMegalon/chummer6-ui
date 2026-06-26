# Blazor web client design index

This index points to the current Blazor/Chummer Online web-client design artifacts. Use it as the starting point for future implementation work.

## Primary direction

The promoted web app must behave like a Chummer desktop client running in the browser.

- Canonical route: `/app`
- Alias route: `/online`
- Compatibility route: `/workbench`
- Preview/debug route: `/preview`

The promoted route should open directly into the classic Chummer shell and must not expose diagnostic-route or noisy workbench chrome.

## Design and behavior artifacts

- [Blazor classic shell contract](blazor-classic-shell-contract.md)
- [Blazor classic shell implementation checklist](blazor-classic-shell-implementation-checklist.md)
- [Blazor web client implementation handoff](blazor-web-client-handoff.md)
- [Blazor web client implementation backlog](blazor-web-client-implementation-backlog.md)
- [Blazor auth and return-route contract](blazor-auth-return-route-contract.md)
- [Blazor Docker self-host contract](blazor-docker-self-host-contract.md)
- [Blazor roster hierarchy contract](blazor-roster-hierarchy-contract.md)
- [Blazor Rybbit analytics contract](blazor-rybbit-analytics-contract.md)
- [Blazor route and parity test plan](blazor-route-parity-test-plan.md)
- [Shared character statistics contract](shared-character-statistics-contract.md)

## Current shell commitments

The current shell direction includes:

- Chummer5A-like desktop chrome.
- Dense titlebar, menu, toolstrip, roster tree, dossier tabs, inspector, and status strip.
- Stable shell metadata for route/workflow/auth/output/deployment/privacy/statistics state.
- Roster tree metadata for custom directories, hierarchy, drag/drop, ordering, and persistence.
- Output workflow metadata for Save, Print, and Export.
- Auth/owner metadata for future login gating without exposing private owner data.
- Rybbit-safe route/workflow-only analytics posture.
- Docker/self-hosting posture.
- Shared statistics/calculation boundary for Blazor and Avalonia reuse.

## Implementation priority

1. Keep `/app` and `/online` visually and behaviorally aligned with the classic shell.
2. Implement roster hierarchy persistence and move engine.
3. Wire real auth gating and return-route preservation.
4. Wire Save/Print/Export dialogs to output metadata.
5. Connect Rybbit analytics only to allowed route/workflow metadata.
6. Implement shared character statistics DTOs and calculation services outside Blazor.
7. Render shared statistics/recommendations in Blazor and Avalonia from the same result model.

## Non-goals for the promoted app route

- Do not reintroduce preview/diagnostic cards above the shell.
- Do not present `/workbench` as the normal user route.
- Do not calculate character statistics in Razor components.
- Do not expose runner names, XML, owner identifiers, file paths, inventory payloads, or free-form notes in analytics metadata.
- Do not turn the shell back into generic web-dashboard controls.
