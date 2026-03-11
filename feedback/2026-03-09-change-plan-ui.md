# Chummer Presentation - Change Plan

Date: 2026-03-09

## Priority

- Stop claiming ownership of the mobile/session shell in the design docs.
- Remove `Chummer.Session.Web` and dedicated play-shell ownership after parity lands in `chummer-play`.
- Keep workbench/browser/desktop UX here.
- Consume `Chummer.Engine.Contracts` and `Chummer.Ui.Kit` via package boundaries instead of duplicate/shared source.

## Exit direction

Presentation should own workbench/browser/desktop UX and shared UI consumption only, not the dedicated play shell.
