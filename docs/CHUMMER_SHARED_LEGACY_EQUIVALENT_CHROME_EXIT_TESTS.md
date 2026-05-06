# Shared Legacy-Equivalent Chrome Exit Tests

Core rule: if the promoted desktop surface already maps to a legacy Chummer function, the shared SR5, SR4, and SR6 lanes must not add helper chrome that changes the user’s mouse-first rhythm or invites rereading.

This gate sits on top of the ruleset-specific muscle-memory inventories:

- Chummer5A promoted desktop head
- Chummer4-backed SR4 promoted surface
- SR6 shared promoted desktop surface

The gate layers are:

1. Inventory prerequisite review
- The gate must require the passing runtime inventory receipts from Chummer5A, SR4, and SR6.
- If the shared surface is not captured at runtime, extra chrome is not allowed to hide behind source-only proofs.

2. Runtime chrome-strip review
- Every visible text sample, action caption, label, and tooltip from those receipts is scanned for forbidden runtime chrome.
- review framing is forbidden on the shared parity surface.
- Runner Summary, Build Lab, Browse Workspace, NPC Persona Studio, Contact Graph, and Downtime Planner are forbidden unless they are explicitly promoted into the parity oracle.
- The shared runtime chrome scan is intentionally generic: if the same function already existed in legacy Chummer, the promoted surface should default to the least additional chrome possible.

3. Verify wiring review
- The gate must run from `scripts/ai/verify.sh` so shared legacy-equivalent chrome drift fails the standard exit path.
