# Chummer App Self-Host Runbook

Purpose: run `Chummer.Blazor` as the browser-hosted desktop-equivalent workbench behind `Chummer.Portal`, with the same route and owner-propagation posture expected from `chummer.run`.

This runbook is for operators who want the web client to behave like another Chummer desktop head, except delivered through the browser and self-hosted in Docker.

Documentation map:

1. `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md` is the browser-client docs index.
2. `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md` is the primary product-shape and parity contract.
3. `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md` defines the separate hosted `chummer.run` execution-proof lane.
4. `docs/WORKBENCH_RELEASE_SIGNOFF.md` defines the release-truth posture that distinguishes self-host, hosted route-entry, and hosted execution proof.

## Target topology

The self-hosted browser stack is the `portal` Docker profile in [docker-compose.yml](../docker-compose.yml):

1. `chummer-api` owns character/application state APIs.
2. `chummer-blazor-portal` serves Chummer App on clean `/app` and `/blazor/`, with `/blazor/app` as the hosted app path, `/blazor/home` as the product/orientation route, and `/blazor/workbench` retained for explicit workbench/proof compatibility.
3. `chummer-hub-web-portal` serves the supporting hub surface on `/hub/`.
4. `chummer-avalonia-browser` remains available on `/avalonia/` as a compatibility/browser-hosted lane.
5. `chummer-portal` is the public edge, owner context boundary, downloads shelf, and reverse proxy.

Expected public routes:

1. `/`
2. `/blazor/`
3. `/blazor/home`
4. `/blazor/app`
5. `/blazor/workbench`
6. `/blazor/workbench?workspace=ws-1`
7. `/blazor/workbench?workspace=ws-1&command=save_character_as`
8. `/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download`
9. `/blazor/workbench?workspace=ws-1&command=print_character`
10. `/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add`
11. `/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add`
12. `/blazor/workbench?workspace=ws-1&tab=tab-calendar`
13. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry`
14. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry&dialog_action=add`
15. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry`
16. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry&dialog_action=apply`
17. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry`
18. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry&dialog_action=delete`
19. `/blazor/workbench?workspace=ws-1&tab=tab-info&control=open_notes`
20. `/blazor/workbench?workspace=ws-1&tab=tab-info&control=open_notes&dialog_action=save`
21. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=move_up`
22. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=move_down`
23. `/blazor/preview`
24. `/blazor/preview?command=new_character`
25. `/blazor/preview?command=new_character_origin`
26. `/blazor/preview?command=open_character`
27. `/blazor/preview?command=open_for_printing`
28. `/blazor/preview?command=open_for_export`
29. `/blazor/preview?fixture=blue&command=save_character`
30. `/blazor/preview?fixture=blue&command=save_character_as`
31. `/blazor/preview?fixture=blue&command=print_character`
32. `/blazor/preview?fixture=blue&command=export_character&dialog_action=download`
33. `/downloads/`
34. `/downloads/releases.json`

Route intent:

1. `/app` is the clean public Chummer App route, `/blazor/` and `/blazor/app` are hosted Blazor entry routes, `/blazor/home` is the explicit product/orientation page, and `/blazor/workbench*` remains the explicit proof-compatible compatibility route family for the same promoted browser client.
2. `/blazor/preview*` is retained as proof/supporting evidence and should not be treated as the primary user entrypoint.

## Minimum environment contract

Set these before bringing the stack up:

1. `CHUMMER_PORTAL_OWNER_SHARED_KEY`
2. `CHUMMER_PORTAL_IMPLICIT_OWNER`
3. `CHUMMER_API_KEY` when the API lane is protected
4. `CHUMMER_PORTAL_PORT`
5. `CHUMMER_API_PORT`

Recommended route defaults:

1. `CHUMMER_PORTAL_BLAZOR_URL=/blazor/`
2. `CHUMMER_PORTAL_HUB_URL=/hub/`
3. `CHUMMER_PORTAL_DOWNLOADS_URL=/downloads/`
4. `CHUMMER_BLAZOR_PATH_BASE=/blazor`

Optional connected-runtime lanes:

1. `CHUMMER_PORTAL_SESSION_URL`
2. `CHUMMER_PORTAL_SESSION_PROXY_URL`
3. `CHUMMER_PORTAL_COACH_URL`
4. `CHUMMER_PORTAL_COACH_PROXY_URL`
5. `CHUMMER_PORTAL_AI_PROXY_URL`
6. `CHUMMER_RUN_URL`

Connected-runtime posture:

1. `CHUMMER_PORTAL_SESSION_PROXY_URL` and `CHUMMER_PORTAL_COACH_PROXY_URL` are optional. Leave them empty unless those services are part of your installation.
2. When session or coach proxying is configured, `Chummer.Portal` forwards the same signed portal-owner header seam used by protected API/AI forwarding, assuming `CHUMMER_PORTAL_OWNER_SHARED_KEY` is set.
3. `CHUMMER_PORTAL_AI_PROXY_URL` remains rooted under `/api/ai/` and also uses the signed portal-owner header seam.
4. The posture receipt is `.codex-studio/published/BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json`, materialized by `scripts/ai/milestones/blazor-connected-runtime-posture-check.sh`.
5. This receipt proves routing and owner-context boundary posture. It does not claim full downstream session, coach, or AI workflow parity.
6. The `/blazor/workbench` and `/blazor/preview` proof shelf renders a connected-runtime posture card with `configured` or `off` state for session, coach, and AI lanes. The card must not expose proxy URLs or owner secrets.

Downloads shelf inputs:

1. `CHUMMER_PORTAL_RELEASES_FILE`
2. `CHUMMER_PORTAL_RELEASES_DIR`
3. `CHUMMER_PORTAL_DOWNLOADS_FALLBACK_URL` only when intentionally proxying to another shelf

Optional analytics inputs:

1. `CHUMMER_ANALYTICS_PROVIDER=none` keeps analytics disabled and is the self-host default.
2. `CHUMMER_ANALYTICS_PROVIDER=rybbit` enables the optional Rybbit browser analytics adapter.
3. `CHUMMER_RYBBIT_SITE_ID` is required when the Rybbit adapter is enabled.
4. `CHUMMER_RYBBIT_SCRIPT_URL` should point at your hosted or self-hosted Rybbit script, for example `https://analytics.example.com/api/script.js`.
5. `CHUMMER_RYBBIT_BASE_URL` may be used instead of `CHUMMER_RYBBIT_SCRIPT_URL`; the Blazor shell resolves it to `/api/script.js`.

Hosted `chummer.run` may run with `CHUMMER_ANALYTICS_PROVIDER=rybbit` for product telemetry, but self-host Docker installs should remain `none` unless the operator intentionally configures a Rybbit endpoint.

The Blazor `/health` endpoint reports non-secret analytics policy fields: `selfHostDefault`, `hostedPublicEdge`, `sensitiveDataPolicy`, `sessionReplayPolicy`, and `autocapturePolicy`. The analytics adapter is intentionally limited to sanitized product metadata. It emits route family, command id, tab id, control id, dialog action id, boolean fixture/workspace presence, and explicit `route-workflow-metadata-only`, `session_replay=disabled`, and `autocapture=disabled` posture fields. It does not emit character names, aliases, owner ids, workspace ids, file names, document contents, XML, payloads, hashes, or generated dossier text. Keep session replay disabled and autocapture disabled for Chummer sites because the browser surface can contain user-authored character data.

Start from [self-hosted-browser-workbench.env.example](examples/self-hosted-browser-workbench.env.example) and override only what your environment actually needs.

## Boot the local self-host stack

From repository root:

```bash
docker compose --profile portal up -d --build \
  chummer-api \
  chummer-blazor-portal \
  chummer-hub-web-portal \
  chummer-avalonia-browser \
  chummer-portal
```

The default portal entrypoint will be `http://127.0.0.1:8091/`.

## What “desktop-equivalent in browser” means operationally

A healthy self-hosted stack should satisfy these operator checks:

1. `chummer-portal` is the only public entrypoint users need.
2. Browser reloads under `/blazor/` keep working because the Blazor path base stays `/blazor`.
3. `/blazor/` resolves into `/blazor/app`, so the default browser-head entry is Chummer App instead of a proof-named workbench route.
4. `/blazor/workbench` remains directly addressable when operators or docs want the explicit workbench path.
5. `/blazor/workbench?workspace=ws-1` can restore a seeded browser session from shared state, and the workbench route exposes state-backed recent-work resume links, restored-session build-lab/section continuation lanes, restored-session result continuations, multiple restored-session action continuations, and multiple restored actions that commit visible state changes instead of stopping at dialog launch.
6. Startup commands can be deep-linked directly:
   `new_character`, `new_character_origin`, `open_character`, `open_for_printing`, and `open_for_export` must open from the URL, not only by clicking inside the shell.
7. `/downloads/` and `/downloads/releases.json` stay aligned with the same portal edge users enter for the workbench.
8. Owner context stays explicit through the portal edge, even when session/coach/AI proxies are configured later.
9. Browser result states are visible from the route alone:
   when restored shared session state exists, `/blazor/workbench?workspace=ws-1&command=save_character_as`, `/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download`, and `/blazor/workbench?workspace=ws-1&command=print_character` should continue that live workspace directly into browser-visible download/export/print surfaces.
10. Seeded browser result states remain available as explicit proof lanes:
   `fixture=blue&command=save_character`, `fixture=blue&command=save_character_as`, `fixture=blue&command=print_character`, and `fixture=blue&command=export_character&dialog_action=download` must land on a browser-visible result surface that proves save/download/print/export dispatch actually occurred.

The portal root, downloads shelf, docs explorer, status, help, and contact recovery pages are also part of the product surface. They should keep the same polished Chummer App slate/amber/mint/blue visual language as clean `/app` and hosted `/blazor/app`, including restrained ambient glow, deep ink/surface contrast, warm gold primary calls to action, and mint focus/recovery affordances, so Docker self-host users do not experience support or installer recovery as a separate generic portal. The portal root route rail should remain labelled as `Chummer browser routes` with explicit hover/focus affordances and reduced-motion handling; help cards plus help, contact, and status exits should remain labelled recovery rails with pill-style keyboard-focus treatment, explicit hover/focus affordances, and reduced-motion guards.

## Quick smoke commands

HTML/health checks:

```bash
curl -fsS http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/health
curl -fsSI http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/
curl -fsS http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/home
curl -fsS http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/app
curl -fsS http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&command=save_character_as"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&command=print_character"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-calendar"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry&dialog_action=add"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry&dialog_action=apply"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry&dialog_action=delete"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-info&control=open_notes"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-info&control=open_notes&dialog_action=save"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=move_up"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=move_down"
curl -fsS http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview?command=new_character"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview?command=new_character_origin"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview?command=open_character"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview?command=open_for_printing"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview?command=open_for_export"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview?fixture=blue&command=save_character"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview?fixture=blue&command=save_character_as"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview?fixture=blue&command=print_character"
curl -fsS "http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/preview?fixture=blue&command=export_character&dialog_action=download"
curl -fsS http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/downloads/releases.json
```

Browser-proof lane:

```bash
CHUMMER_PORTAL_BASE_URL="http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}" \
node scripts/e2e-portal-playwright.cjs
```

The Playwright lane is the current proof-backed way to confirm that the portal edge, `/blazor/` to `/blazor/app` entry behavior, startup workbench, state-backed recent-work resume links, restored-session build-lab/section/action continuation lanes, restored-session result continuations, multiple restored actions with visible committed state changes, dialog deep links, seeded browser workflows, and seeded save/download/print/export result states still work together.

When the full portal harness is used through `bash scripts/e2e-portal.sh`, the self-hosted browser proof receipt is written to `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`.

The next receipt refresh is staged to declare the career/support route family in the receipt route list as well as exercising it through Playwright: `tab-calendar`, `create_entry`, `create_entry&dialog_action=add`, `edit_entry`, `edit_entry&dialog_action=apply`, `delete_entry`, `delete_entry&dialog_action=delete`, `open_notes`, `open_notes&dialog_action=save`, `move_up`, and `move_down`.

## Production posture notes

1. Keep `/blazor/` behind `Chummer.Portal`; do not publish the raw Blazor service as the primary user entrypoint when you want parity with the hosted `chummer.run` model.
2. Mount real downloads storage into `chummer-portal` when the shelf is part of the same self-hosted installation.
3. Use `CHUMMER_PORTAL_DOWNLOADS_FALLBACK_URL` only when you intentionally want the portal to advertise an external shelf.
4. Treat `/app` as the product-shaped Chummer App entrypoint, `/blazor/app` as the hosted app path, `/blazor/home` as the product/orientation page, `/blazor/workbench` as the explicit proof-compatible workbench route, and `/blazor/preview` as a proof surface while browser parity continues to mature.
5. If you wire session/coach/AI lanes, keep them proxied through `chummer-portal` so the browser and operator surface share one authority boundary.

## Failure signatures

1. `/blazor/` opens but reload/deep-link behavior breaks:
   `CHUMMER_BLAZOR_PATH_BASE` or `CHUMMER_PORTAL_BLAZOR_URL` is misaligned.
2. `/downloads/` renders but has no artifacts:
   inspect the downloads storage mount and `CHUMMER_PORTAL_RELEASES_FILE`.
3. Chummer App loads without expected owner/session copy:
   inspect `CHUMMER_PORTAL_IMPLICIT_OWNER`, `CHUMMER_PORTAL_OWNER_SHARED_KEY`, and any session proxy configuration.
4. Portal root works but `/blazor/` does not:
   inspect `CHUMMER_PORTAL_BLAZOR_PROXY_URL` and `chummer-blazor-portal` container health.

## Relationship to other runbooks

1. Use [SELF_HOSTED_DOWNLOADS_RUNBOOK.md](SELF_HOSTED_DOWNLOADS_RUNBOOK.md) for publishing and verifying the downloads shelf itself.
2. Use [DESKTOP_RELEASE_PIPELINE.md](DESKTOP_RELEASE_PIPELINE.md) for native desktop packaging ownership and release boundaries.
3. Use [BLAZOR_WEB_CLIENT_PARITY_GOAL.md](BLAZOR_WEB_CLIENT_PARITY_GOAL.md) for the broader product target and acceptance bar.
4. Use [BLAZOR_WEB_CLIENT_DOCS_INDEX.md](BLAZOR_WEB_CLIENT_DOCS_INDEX.md) when you need the full web-client documentation map.
5. Use [BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md](BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md) for the stricter hosted execution-proof contract that applies to `https://chummer.run/blazor/`.
