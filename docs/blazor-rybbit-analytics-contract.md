# Blazor Rybbit analytics contract

Rybbit analytics for Chummer Online must remain route/workflow-only. It can help understand app navigation and deployment posture, but it must never collect character contents, owner identity, dossier payloads, or private gameplay data.

## Scope

This contract applies to the promoted Blazor app routes and compatibility route:

- `/app`
- `/online`
- `/workbench`

The canonical promoted route is `/app`. `/online` is an alias. `/workbench` is compatibility posture and should not be promoted as the default user app route.

## Allowed event names

Allowed event names should describe app navigation and shell workflow only:

- `chummer_app_opened`
- `chummer_workflow_changed`
- `chummer_route_alias_opened`
- `chummer_output_requested`
- `chummer_roster_command_requested`
- `chummer_roster_node_moved`
- `chummer_roster_folder_created`
- `chummer_roster_tree_restored`
- `chummer_auth_gate_required`
- `chummer_self_host_entry_viewed`
- `chummer_statistics_panel_viewed`

Event names must not include runner names, campaign names, owner IDs, file names, or free-form user text.

## Allowed properties

Allowed properties are stable shell metadata keys only:

- `route_family`
- `route_segment`
- `route_surface`
- `canonical_route`
- `route_alias`
- `active_workflow`
- `command`
- `tab`
- `control`
- `dialog_action`
- `fixture`
- `output_workflow`
- `output_state`
- `output_target`
- `auth_gate`
- `session_state`
- `owner_scope`
- `owner_state`
- `privacy_mode`
- `analytics_scope`
- `hosting_mode`
- `deployment_target`
- `self_hostable`
- `container_target`
- `client_kind`
- `parity_target`
- `calculation_boundary`
- `statistics_result`
- `percentile_band`
- `recommendation_state`

Properties must be normalized tokens, not raw UI text.

## Forbidden properties

Never send these values to Rybbit:

- Runner names.
- Character names.
- Player names.
- Owner IDs.
- Email addresses.
- Account identifiers.
- Campaign names.
- File names.
- File paths.
- XML content.
- Dossier payloads.
- Inventory contents.
- Gear lists.
- Spell lists.
- Drug names from inventory.
- Free-form notes.
- Contact names.
- SIN/license values.
- Raw build statistics.
- Exact private attribute/skill values.
- Any unnormalized query string.
- Any full URL containing private query data.

## Shell metadata source

Analytics code should read from the classic shell root attributes and map attribute names to snake_case properties.

Allowed source attributes include:

- `data-route-family`
- `data-route-segment`
- `data-route-surface`
- `data-canonical-route`
- `data-route-alias`
- `data-active-workflow`
- `data-command`
- `data-tab`
- `data-control`
- `data-dialog-action`
- `data-fixture`
- `data-output-workflow`
- `data-output-state`
- `data-output-target`
- `data-auth-gate`
- `data-session-state`
- `data-owner-scope`
- `data-owner-state`
- `data-privacy-mode`
- `data-analytics-scope`
- `data-hosting-mode`
- `data-deployment-target`
- `data-self-hostable`
- `data-container-target`
- `data-client-kind`
- `data-parity-target`
- `data-calculation-boundary`
- `data-statistics-result`
- `data-percentile-band`
- `data-recommendation-state`

Do not read visible text, links, nested roster labels, or full href values for analytics properties.

## Roster analytics

Roster events may report only structural action metadata:

Allowed:

- Command key, such as `new-folder` or `move-selection`.
- Node kind, such as `folder` or `runner`.
- Move mode, such as `into-folder`, `before-node`, or `after-node`.
- Result state, such as `success`, `blocked`, or `cancelled`.
- Schema version.

Forbidden:

- Node labels.
- Runner names.
- Folder names.
- Full roster tree payload.
- Persisted node IDs if they encode user/private labels.

## Statistics analytics

Statistics events may report only coarse result posture:

Allowed:

- `statistics_result`
- `percentile_band`
- `recommendation_state`
- `risk_model`
- `calculation_boundary`

Forbidden:

- Exact private character values.
- Exact inventory contents.
- Spell names from the user's character.
- Drug names from inventory.
- Full recommendation explanation text.
- Exact risk calculation inputs.

## Required safeguards

- Analytics scope must be `route-workflow-only` before sending app shell events.
- Session replay must remain disabled unless a separate privacy review explicitly allows it.
- Autocapture must remain disabled for the Chummer shell.
- Query strings must not be forwarded wholesale.
- Properties must be allowlisted, not denylisted.
- Unknown attributes must be ignored by default.

## Implementation checklist

1. Read the shell root by `[data-chummer-classic-shell="true"]`.
2. Build analytics properties from the allowlist only.
3. Drop any property whose value is empty, raw, too long, or not a normalized token.
4. Send only route/workflow events.
5. Keep session replay and autocapture disabled.
6. Add tests or checks that forbidden strings are not sent.
