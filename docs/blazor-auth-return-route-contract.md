# Blazor auth and return-route contract

The promoted Chummer Online web app should behave like a desktop client with a user session. It may redirect anonymous users to login, but it must preserve the exact route and workflow the user requested.

## Route posture

Promoted app routes:

- `/app`
- `/online` as an alias of `/app`

Compatibility route:

- `/workbench`

Preview/debug route:

- `/preview`

Only promoted app routes should use the normal user auth gate. Compatibility and preview routes may stay available for diagnostics and local testing unless a deployment explicitly locks them down.

## Shell metadata

The classic shell root exposes auth posture:

- `data-auth-gate="login-if-anonymous"`
- `data-login-target="login"`
- `data-auth-return-policy="preserve-route-and-query"`
- `data-session-state`
- `data-owner-scope`
- `data-owner-state`

Runtime auth code should read these hooks or equivalent route policy, not visible text.

## Anonymous user behavior

When an anonymous user opens a promoted app route:

1. Capture route path and query string.
2. Redirect to login target.
3. Preserve route and query as a return target.
4. After login, return to the original route and query.
5. Rehydrate active workflow from query state.

Example:

```text
/app?command=character_roster
-> /login?returnUrl=/app?command=character_roster
-> /app?command=character_roster
```

The returned app should open the classic shell and preserve the active workflow, title, toolbar/menu current states, shell data attributes, and status strip state.

## Alias behavior

If the user opens `/online`, the implementation may either:

- preserve `/online` through login while marking `data-canonical-route="app"`, or
- canonicalize to `/app` before or after login.

If canonicalizing, query parameters must be preserved.

Do not redirect `/online` to `/workbench`.

## Self-hosted auth modes

Self-hosted Docker deployments should support these postures:

- `public-local`: no auth gate for private/local installs.
- `single-owner`: one configured owner/admin.
- `external-auth`: reverse proxy, OIDC, or hosted identity provider.
- `hosted`: Chummer.run hosted auth behavior.

The shell metadata should still remain privacy-safe and should not expose owner identifiers.

## Owner state

Allowed owner/session states:

- `local-preview`
- `anonymous`
- `authenticated`
- `expired`
- `offline`

Owner metadata must remain coarse. Do not expose account IDs, email addresses, display names, or provider subject IDs in DOM attributes or analytics.

## Failure behavior

If login fails or the session expires:

- Keep the original return route available.
- Show a desktop-style status/error posture.
- Do not discard unsaved local dossier state without user confirmation.
- Do not leak private query payloads into analytics.

## Analytics constraints

Auth events may report only coarse state:

Allowed:

- `auth_gate`
- `session_state`
- `owner_scope`
- `owner_state`
- `route_surface`
- `active_workflow`

Forbidden:

- Owner ID.
- Email.
- Provider subject.
- Return URL as a raw string.
- Full query string.
- Character/dossier payload.

## Implementation checklist

1. Define promoted route auth policy for `/app` and `/online`.
2. Preserve route and query on login redirects.
3. Keep `/app` canonical and `/online` as alias.
4. Keep `/workbench` out of the normal user flow.
5. Update `data-session-state` and `data-owner-state` from real auth state.
6. Add tests for route/query preservation.
7. Add tests that forbidden owner/private values are not emitted to analytics.
