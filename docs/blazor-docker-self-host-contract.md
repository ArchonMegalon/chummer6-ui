# Blazor Docker self-host contract

Chummer Online must run as a hosted app on Chummer.run and as a self-hosted Docker deployment. The web client should behave like another Chummer desktop client in the browser, regardless of whether it is hosted publicly or self-hosted privately.

## Route contract

Canonical hosted route:

- `/app`

Alias route:

- `/online`

Compatibility route:

- `/workbench`

Preview/debug route:

- `/preview`

Self-hosted deployments should preserve the same route semantics behind their configured base path.

## Required shell metadata

The classic shell root must expose deployment posture:

- `data-hosting-mode="hosted-or-self-hosted"`
- `data-deployment-target="chummer-run"` or self-host equivalent
- `data-self-hostable="true"`
- `data-container-target="docker"`
- `data-route-surface="public-app"` for promoted app routes
- `data-client-kind="web-desktop"`

Runtime code and tests should use these attributes instead of scraping copy.

## Container requirements

A self-hosted Docker deployment should provide:

- A single published HTTP front door for the Blazor web client.
- Configurable base path support.
- Configurable public origin.
- Configurable analytics provider switch.
- Configurable Rybbit site ID and script URL.
- Safe defaults with analytics disabled unless configured.
- No requirement for Chummer.run-only services for local app shell rendering.

## Configuration expectations

Expected environment/config keys should remain generic and deployment-friendly:

- Analytics provider key.
- Rybbit site ID key.
- Rybbit script URL/base URL key.
- Public base URL/origin key.
- Auth/login provider settings.
- Optional self-host owner/admin bootstrap settings.

Configuration must not hardcode Chummer.run except where documenting hosted defaults.

## Auth posture

Promoted app routes may use:

- `data-auth-gate="login-if-anonymous"`
- `data-login-target="login"`
- `data-auth-return-policy="preserve-route-and-query"`

Self-hosted deployments must be able to choose local auth, external auth, or disabled auth for private deployments, while keeping route/workflow behavior consistent.

## Privacy posture

Self-hosted deployments should default to local-first behavior:

- Do not send dossier payloads to analytics.
- Do not enable session replay by default.
- Do not enable autocapture for the Chummer shell.
- Do not expose runner names, XML, file paths, owner identifiers, inventory payloads, or notes in telemetry.

## Rybbit posture

Rybbit may be enabled only for route/workflow-only analytics.

Allowed:

- Route family.
- Route surface.
- Active workflow.
- Command/tab/control keys.
- Output workflow state.
- Deployment target.
- Self-host posture.

Forbidden:

- Character contents.
- Owner identity.
- Raw query strings.
- Dossier XML.
- Inventory and build payloads.

## Runtime parity expectations

The Docker app should support the same browser desktop workflow as Chummer.run:

- Classic shell first paint.
- Character roster workflow.
- Custom roster folder hierarchy.
- Save/Print/Export workflow surfaces.
- Auth return-route preservation when auth is enabled.
- Shared calculation/statistics rendering when the shared engine is available.

## Deployment tests to add later

- `/app` renders the promoted classic shell.
- `/online` renders the shell as an alias with canonical route `app`.
- `/workbench` remains compatibility posture.
- Shell exposes Docker/self-host metadata.
- Analytics stays disabled by default unless explicitly configured.
- Rybbit properties are allowlisted.
- Base path deployment preserves routes and hrefs.
- Login redirect preserves route and query when auth is enabled.
