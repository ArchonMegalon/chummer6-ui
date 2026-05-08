# SR6 Shared Muscle-Memory Exit Tests

Core rule: SR6 does not have a separate legacy Chummer6 oracle, so the SR6 desktop lane must keep the same shared desktop posture as the promoted Chummer baseline wherever the same shell, dialog, panel, grid, menu, tooltip, and mouse route still exist.

This gate is fail-closing for the shared SR6 desktop surface:

- shared shell chrome
- shared menu roots
- shared promoted dialogs and utility forms
- shared workspace panels, grids, and quick actions
- shared tooltip and secondary-route posture

The gate layers are:

1. Shared baseline scope review
- The policy must explicitly state that SR6 is measured against the promoted desktop baseline rather than a separate Chummer6 oracle.
- The gate must require the SR6 workflow parity receipt to be passing so the SR6 muscle-memory slice cannot float free from the broader SR6 frontier proof.

2. Runtime inventory review
- A runtime inventory receipt must loop through every promoted shared shell, menu, workspace, dialog, and tooltip-bearing control after an SR6 runner is loaded.
- The runtime inventory must fail when widget class, label copy, layout zone, field order, action order, tooltip coverage, or mouse-route hints drift on the shared SR6 surface.
- Tooltip-only hosts must not be counted as right-click or flyout posture; the receipt must prove real secondary hosts or prove zero hosts on the promoted SR6 slice.

3. Shared baseline parity review
- The SR6 receipt must compare the active SR6 surface against the promoted desktop baseline and fail when shared controls change widget class, layout zone, action order, or mouse route.
- shared shell, workspace, dialog, and action routes must keep the same control rhythm so veteran mouse muscle memory still works.
- Middle click stays fail-closed on the SR6 shared lane until a real promoted middle-click route is explicitly proven.

4. Verify wiring review
- The gate must run from `scripts/ai/verify.sh` so SR6 shared muscle-memory drift fails the standard exit path.
