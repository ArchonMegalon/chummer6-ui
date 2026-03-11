# External Tools Plane

Project Chummer has an explicit External Tools Plane.

This plane exists to integrate owned third-party capabilities without allowing any third-party capability to become canonical Chummer truth.

## Rules

1. External tools always sit behind Chummer-owned adapters.
2. External tools may assist, project, notify, visualize, render, or archive.
3. External tools may not own:
   - rules truth
   - reducer truth
   - runtime truth
   - session truth
   - approval truth
   - registry truth
   - artifact truth
   - memory/canon truth
4. No client repo may access third-party tools directly.
5. External-provider-assisted outputs that re-enter Chummer must carry Chummer-side provenance and receipts.
6. `chummer.run-services` owns orchestration-side integrations.
7. `chummer-media-factory` owns render/archive integrations.
8. `chummer-design` owns external-tools policy and rollout governance.

## Ownership by repo

### `chummer.run-services`

- reasoning providers
- approval bridges
- docs/help bridges
- survey bridges
- automation bridges
- research/eval tooling

### `chummer-media-factory`

- document render adapters
- preview/thumbnail adapters
- image/video adapters
- route visualization adapters
- cold-archive adapters

### `chummer-hub-registry`

- references to promoted reusable template/style/help/preview artifacts only

## Non-goals

- no third-party tool is a required hop for live session relay
- no third-party tool holds canonical approval state
- no third-party tool owns Chummer media manifests
- no third-party tool bypasses Chummer moderation or canonization
