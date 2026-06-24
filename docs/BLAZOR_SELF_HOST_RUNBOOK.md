# Browser Workbench Self-Host Runbook

Purpose: run `Chummer.Blazor` as the browser-hosted desktop-equivalent workbench behind `Chummer.Portal`, with the same route and owner-propagation posture expected from `chummer.run`.

This runbook is for operators who want the web client to behave like another Chummer desktop head, except delivered through the browser and self-hosted in Docker.

Documentation map:

1. `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md` is the browser-client docs index.
2. `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md` is the primary product-shape and parity contract.
3. `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md` defines the separate hosted `chummer.run` execution-proof lane.
4. `docs/WORKBENCH_RELEASE_SIGNOFF.md` defines the release-truth posture that distinguishes self-host, hosted route-entry, and hosted execution proof.

## Target topology

The self-hosted browser stack is the `portal` Docker profile in [docker-compose.yml](/docker/chummercomplete/chummer-presentation/docker-compose.yml):

1. `chummer-api` owns character/application state APIs.
2. `chummer-blazor-portal` serves the browser workbench on `/blazor/`.
3. `chummer-hub-web-portal` serves the supporting hub surface on `/hub/`.
4. `chummer-avalonia-browser` remains available on `/avalonia/` as a compatibility/browser-hosted lane.
5. `chummer-portal` is the public edge, owner context boundary, downloads shelf, and reverse proxy.

Expected public routes:

1. `/`
2. `/blazor/`
3. `/blazor/workbench`
4. `/blazor/workbench?workspace=ws-1`
5. `/blazor/workbench?workspace=ws-1&command=save_character_as`
6. `/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download`
7. `/blazor/workbench?workspace=ws-1&command=print_character`
8. `/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add`
9. `/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add`
10. `/blazor/workbench?workspace=ws-1&tab=tab-calendar`
11. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry`
12. `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry&dialog_action=add`
13. `/blazor/preview`
14. `/blazor/preview?command=new_character`
15. `/blazor/preview?command=new_character_origin`
16. `/blazor/preview?command=open_character`
17. `/blazor/preview?command=open_for_printing`
18. `/blazor/preview?command=open_for_export`
19. `/blazor/preview?fixture=blue&command=save_character`
20. `/blazor/preview?fixture=blue&command=save_character_as`
21. `/blazor/preview?fixture=blue&command=print_character`
22. `/blazor/preview?fixture=blue&command=export_character&dialog_action=download`
23. `/downloads/`
24. `/downloads/releases.json`

Route intent:

1. `/blazor/` and `/blazor/workbench*` are the promoted browser-client routes.
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

The analytics adapter is intentionally limited to sanitized product metadata. It emits route family, command id, tab id, control id, dialog action id, and boolean fixture/workspace presence. It does not emit character names, aliases, owner ids, workspace ids, file names, document contents, XML, payloads, hashes, or generated dossier text. Keep session replay disabled for Chummer sites because the browser surface can contain user-authored character data.

Start from [self-hosted-browser-workbench.env.example](/docker/chummercomplete/chummer-presentation/docs/examples/self-hosted-browser-workbench.env.example) and override only what your environment actually needs.

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
3. `/blazor/` resolves into `/blazor/workbench`, so the default browser-head landing is the product-shaped workbench route instead of a proof-only label.
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

## Quick smoke commands

HTML/health checks:

```bash
curl -fsS http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/health
curl -fsSI http://127.0.0.1:${CHUMMER_PORTAL_PORT:-8091}/blazor/
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

The Playwright lane is the current proof-backed way to confirm that the portal edge, `/blazor/` to `/blazor/workbench` landing behavior, startup workbench, state-backed recent-work resume links, restored-session build-lab/section/action continuation lanes, restored-session result continuations, multiple restored actions with visible committed state changes, dialog deep links, seeded browser workflows, and seeded save/download/print/export result states still work together.

When the full portal harness is used through `bash scripts/e2e-portal.sh`, the self-hosted browser proof receipt is written to `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`.

## Production posture notes

1. Keep `/blazor/` behind `Chummer.Portal`; do not publish the raw Blazor service as the primary user entrypoint when you want parity with the hosted `chummer.run` model.
2. Mount real downloads storage into `chummer-portal` when the shelf is part of the same self-hosted installation.
3. Use `CHUMMER_PORTAL_DOWNLOADS_FALLBACK_URL` only when you intentionally want the portal to advertise an external shelf.
4. Treat `/blazor/preview` as a proof surface and `/blazor/workbench` as the product-shaped browser entrypoint while browser parity continues to mature.
5. If you wire session/coach/AI lanes, keep them proxied through `chummer-portal` so the browser and operator surface share one authority boundary.

## Failure signatures

1. `/blazor/` opens but reload/deep-link behavior breaks:
   `CHUMMER_BLAZOR_PATH_BASE` or `CHUMMER_PORTAL_BLAZOR_URL` is misaligned.
2. `/downloads/` renders but has no artifacts:
   inspect the downloads storage mount and `CHUMMER_PORTAL_RELEASES_FILE`.
3. Browser workbench loads without expected owner/session copy:
   inspect `CHUMMER_PORTAL_IMPLICIT_OWNER`, `CHUMMER_PORTAL_OWNER_SHARED_KEY`, and any session proxy configuration.
4. Portal root works but `/blazor/` does not:
   inspect `CHUMMER_PORTAL_BLAZOR_PROXY_URL` and `chummer-blazor-portal` container health.

## Relationship to other runbooks

1. Use [SELF_HOSTED_DOWNLOADS_RUNBOOK.md](/docker/chummercomplete/chummer-presentation/docs/SELF_HOSTED_DOWNLOADS_RUNBOOK.md) for publishing and verifying the downloads shelf itself.
2. Use [DESKTOP_RELEASE_PIPELINE.md](/docker/chummercomplete/chummer-presentation/docs/DESKTOP_RELEASE_PIPELINE.md) for native desktop packaging ownership and release boundaries.
3. Use [BLAZOR_WEB_CLIENT_PARITY_GOAL.md](/docker/chummercomplete/chummer-presentation/docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md) for the broader product target and acceptance bar.
4. Use [BLAZOR_WEB_CLIENT_DOCS_INDEX.md](/docker/chummercomplete/chummer-presentation/docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md) when you need the full web-client documentation map.
5. Use [BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md](/docker/chummercomplete/chummer-presentation/docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md) for the stricter hosted execution-proof contract that applies to `https://chummer.run/blazor/`.
