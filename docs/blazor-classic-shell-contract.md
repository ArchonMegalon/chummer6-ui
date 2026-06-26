# Blazor classic Chummer shell contract

The promoted Blazor app routes (`/app` and `/online`) render the classic Chummer desktop shell. `/app` is canonical; `/online` is an alias. The compatibility route (`/workbench`) may render the same shell while retaining compatibility/diagnostic material outside the promoted surface.

This shell must behave like another Chummer desktop client in the web, not like a marketing page or disconnected control gallery.

## Route and surface contract

The shell root exposes route metadata for tests, analytics, auth gating, and canonicalization:

- `data-route-family`: broad route family, for example `app`, `online-alias`, `compatibility`, or `preview`.
- `data-route-segment`: exact route segment, for example `app`, `online`, `workbench`, or `preview`.
- `data-route-surface`: UX surface, for example `public-app`, `compatibility`, or `preview-tools`.
- `data-canonical-route`: canonical route segment. `/online` must keep this as `app`.
- `data-route-alias`: alias segment, or `none`.

Promoted app routes must suppress preview or diagnostic chrome and show the classic shell as the primary visible surface.

## Workflow and deep-link contract

The shell root exposes normalized deep-link state:

- `data-active-workflow`: active workflow key, such as `character-roster`, `profile`, `build-lab`, `gear`, `combat`, `magic`, `matrix`, `contacts`, `career`, `save`, `print`, `export`, or `dossier`.
- `data-command`: normalized command query value, or `none`.
- `data-tab`: normalized tab query value, or `none`.
- `data-control`: normalized control query value, or `none`.
- `data-dialog-action`: normalized dialog action query value, or `none`.
- `data-fixture`: normalized fixture query value, or `none`.
- `data-legacy-runner`: normalized legacy runner query value, or `none`.

The desktop titlebar, browser title, tabs, toolbar, menu, and status strip should reflect the active workflow consistently.

## Roster hierarchy contract

The character roster is a user-organizable tree. It must support custom directories and arbitrary hierarchy without hardcoding campaign structure.

The roster tree root exposes:

- `data-roster-tree="custom-directories"`
- `data-roster-schema-version="1"`
- `data-roster-storage-scope="workspace"`
- `data-roster-persistence-key="chummer-online-roster-tree"`
- `data-roster-reorder-mode="nested"`
- `data-roster-selected-node`: selected roster node ID, or `none`.

Roster nodes expose:

- `data-roster-node-id`: stable node ID.
- `data-roster-parent-id`: stable parent node ID, or `root`.
- `data-roster-order`: sibling ordering value.
- `data-tree-kind`: `folder` or `runner`.
- `data-roster-drop-zone`: stable drop target key for folders.
- `data-roster-accepts-children="true"` for folders.
- `data-roster-draggable-kind`: `folder` or `runner`.

Folders and runners may be draggable. Runtime drag/drop must update parent/order state through shared roster behavior rather than scraping visible labels.

## Output contract

Save, Print, and Export are active workflows and also output workflows.

The shell root exposes:

- `data-output-workflow`: `save`, `print`, `export`, or `none`.
- `data-output-state`: `idle`, `requested`, `ready`, or `error`.
- `data-output-target`: `local-dossier`, `print-view`, `download-package`, or `none`.

Future output dialogs should update these fields instead of adding one-off visual states.

## Auth, owner, privacy, and analytics contract

The shell root exposes auth and ownership posture without personal data:

- `data-auth-gate`: for example `login-if-anonymous` or `none`.
- `data-session-state`: for example `local-preview`, `authenticated`, or `anonymous`.
- `data-login-target`: login target key, or `none`.
- `data-auth-return-policy`: for example `preserve-route-and-query` or `none`.
- `data-owner-scope`: for example `user` or `local`.
- `data-owner-state`: for example `local-preview`, `authenticated`, or `anonymous`.
- `data-privacy-mode`: for example `local-first`.
- `data-analytics-scope`: `route-workflow-only` for Rybbit-safe route/workflow telemetry.

Telemetry must not include runner names, dossier payloads, XML, owner identifiers, file paths, or character contents.

## Deployment contract

The shell root exposes hosting posture:

- `data-hosting-mode`: for example `hosted-or-self-hosted`.
- `data-deployment-target`: for example `chummer-run` or `compatibility`.
- `data-self-hostable`: `true` when Docker self-hosting is supported.
- `data-container-target`: for example `docker`.

## Calculation and statistics contract

Blazor must render character statistics and recommendations from reusable shared calculation logic. It must not become the owner of character math.

The shell root exposes:

- `data-calculation-owner="shared-chummer-core"`
- `data-statistics-runtime="reusable-by-avalonia"`
- `data-calculation-boundary="shared-engine-only"`
- `data-result-consumer="blazor-renders-shared-results"`
- `data-character-statistics="enabled"`
- `data-statistics-scope="anonymized-build-comparisons"`
- `data-recommendation-mode="explainable-local-inputs"`
- `data-recommendation-inputs="spells-inventory-drugs-gear-qualities"`
- `data-risk-model="damage-threshold-probability"`
- `data-statistics-result`: for example `pending-shared-calculation`.
- `data-percentile-band`: for example `pending` or `top-3`.
- `data-recommendation-state`: for example `available-after-calculation` or `ready`.

Example future result: a character may be shown as top percentile for an initiative metric only when shared logic proves the percentile and can explain spell/drug/gear/risk assumptions.

## Shortcut contract

Toolbar, menu, and roster commands expose stable shortcut metadata:

- `data-chummer-shortcut` and `aria-keyshortcuts` for main Chummer commands.
- `data-roster-shortcut` and `aria-keyshortcuts` for roster organization commands.

Shortcut handling must bind to command IDs and attributes, not visible text.

## Token normalization

Shell data tokens are normalized to lowercase letters, digits, and hyphens. Empty values become `none`. Tokens are capped to a stable maximum length.
