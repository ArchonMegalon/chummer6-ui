# Designer Feedback Split: presentation

Source: 2026-03-08 Chummer designer market scan. Distilled direction: community demand is ops-first, not AI-first. The product must reduce Shadowrun bookkeeping, reduce duplicate entry, support cross-device play, explain rules with provenance, preserve house-rule flexibility, and avoid hosted lock-in.

## Your part
presentation must deliver the actual play surfaces people keep asking for, separate from the deep builder.

Prioritize:
- a hard split between build mode and play mode
- a fast Player Play Shell for tablet/web/mobile: current pools, condition monitors, ammo, Edge, Karma/Nuyen, active effects, initiative slot, and subsystem panes when relevant
- a true GM Ops Board: initiative rail, stale-state indicators, condition/resource alerts, quick NPC controls, and one-tap operational actions
- Explain + Source panes that expand derived values into readable traces with provenance instead of opaque numbers
- offline-friendly, low-friction interaction with queued sync/resume rather than mandatory always-online behavior
- player-facing reveal/share surfaces and prep-friendly views that help the table run faster

Guardrails:
- do not drag the full builder UX into the play shell
- do not reimplement Shadowrun math in UI code
- do not make assistant chat the primary experience; operational cards and state views should lead
- optimize for sparse, reactive, during-play flows over feature-dense configuration screens

Product implication:
The marketable surface is not "AI GM chat". It is a better Shadowrun operating surface for players and GMs during live sessions.
