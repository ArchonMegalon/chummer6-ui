# WIN-006 Controller-Followup Evidence (Re-Audit Handoff Items 1 and 2)

- **Date:** 2026-11-02
- **Worker:** friday-windows (single active session per `WINDOWS_REMOTE.md`)
- **Verification SDK:** Windows sandbox SDK 10.0.103 (`C:\Users\tibor\AppData\Local\Chummer\friday\tasks\WIN-001\sdk`)
- **Branch:** `windows/beta-ai-finish-gate-20261102` (rebased onto main `a6741129b`, work order per PR #505)
- **Work order:** `docs/agents/WINDOWS_SCOPE.md` (Friday audit publication, PR #505), item 2

## Handoff item 2 — "Gate the incomplete Finish-with-AI action"

> Handoff text: the affordance must be gated dark until the flow is qualified;
> locate every menu, command, shortcut, and alternate entry point; the gate can
> ship as a small independent fix.

### Entry-point audit (complete)

| Entry point | Location | Action taken |
|---|---|---|
| Wizard button | `CharacterCreationWizardControl.axaml` (FinishWithAiButton, `IsVisible="False"` by default) | Visibility now flows through one choke point `RefreshFinishWithAiAvailability()` |
| Click | `FinishWithAiButton_OnClick` | Guard: raises only when the gate is enabled |
| Window handler | `MainWindow.CreationWizard.cs` → `CreationWizard_OnFinishWithAiRequested` | Added `_creationWizardAiFinishFeatureEnabled` lock, seeded once at wizard init from `CharacterCreationAiFinishGatePolicy.IsSessionEnabled()` |
| Journey leg | `DesktopMouseFirstJourneyRunner` AI-finish leg | Calls `wizard.SetFinishWithAiFeatureEnabled(true)` before actuating the button when `CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH=1` |
| Menu / command / shortcut | — | None exist (verified by repo-wide grep: the wizard button is the sole actuation surface) |

### Gate design (mirrors the existing `CharacterCreationWizardBuildGhostPolicy` idiom)

- New policy type `CharacterCreationAiFinishGatePolicy` in
  `Chummer.Presentation/Overview/CharacterCreationWizardDesktopSession.cs`:
  `IsSessionEnabled()` reads `CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH=1` (case-insensitive);
  `SessionEnvVariable` is the stable contract.
- Control: `_finishWithAiFeatureEnabled` (default **off**);
  all three former direct `IsVisible` writers (`SetFinishWithAiAvailability`,
  `SetFinishWithAiBusy(false)` bypass, `SetFinishWithAiResult` bypass) now funnel
  through `RefreshFinishWithAiAvailability()` — **no code path can resurface the
  button while the gate is closed**. Click guard swallows gated clicks.
- Window: `_creationWizardAiFinishFeatureEnabled` read **once** at wizard init;
  the handler additionally checks the Build Ghost preference so behavior is
  unchanged for opted-in operators.
- Journey leg: the operator opt-in env var flips the control gate programmatically
  prior to actuation, so automated acceptance of the still-qualifying flow keeps
  working (the WIN-002 ceremony's SR5 priority journey is unaffected — its env
  var is a different variable).

### Tests (first-ever coverage for this affordance) — all passing

`Chummer.Tests/Presentation/CharacterCreationAiFinishGateAvaloniaTests.cs`
(4 headless tests, focused-compile mode, MSTest 4.0.2 on .NET 10.0.3):

1. `Finish_with_ai_button_is_dark_until_the_feature_gate_is_enabled` — default-dark, gated click swallowed
2. `Finish_with_ai_button_projects_once_the_feature_gate_is_enabled` — opt-in surfaces the affordance; click raises
3. `...busy_and_result_writers_cannot_bypass_the_gate` — busy/result writers cannot resurface a gated button; disable re-hides instantly
4. `Ai_finish_gate_session_env_contract_is_stable` — env var name pinned by test

### Verification transcript

| Check | Result |
|---|---|
| Focused build (Presentation + Avalonia + focused tests) | **0 errors** |
| Gate tests (headless, MSTest-MTP exe) | **4/4 pass** (2.5s) |
| Pinned source tests (`Chummer.Desktop.Runtime.Tests`, 174 tests incl. compliance text pins) | **172/174** — 2 pre-existing env failures **proven on pristine handoff tip** via stash baseline (staged Linux `.deb` on Windows). Zero attribution to this branch. |
| Curated CI include list (`Chummer.Tests.csproj`) | Test wired in beside `CharacterCreationContactsAvaloniaTests.cs` |
| Pre-existing rot fixed en route | 3 raw-string compile breaks in `GearAttackSwapParityTests.cs`, `GearSleazeSwapParityTests.cs`, `PrototypeTranshumanParityTests.cs` (CS9006/CS9007, unreachable on Windows full-compile) |

### Pre-existing test debt catalogued (item 8 backlog, not addressed here)

- `Chummer.Tests` Windows full-compile: 1230 errors (CS9006/CS9007 raw strings, CS0535 stub drift vs `IChummerClient`, missing `Chummer.Infrastructure.Browser`).
- `Chummer.Desktop.Runtime.Tests` on Windows: the 2 update-runtime tests stage a Linux `.deb` and try to execute it on Windows; CI is Linux-only for this project — environmental, files predate the handoff.


## Re-audit handoff item 1 - "Remove the full diagnostic environment dump" (immediate P1)

Implemented 2026-11-02 against main `6f2135211`.

### Defect and fix

`scripts/win004-installer-update-recovery-journey.ps1` leg 5 (native start)
enumerated `$psi.EnvironmentVariables` name/value pairs into
`start-failure-env.txt` on app-exit - the child inherits the harness
environment, so workspace credentials could reach plaintext diagnostic
evidence. Replaced with `scripts/win004-start-failure-diagnostics.ps1`
(`Write-StartFailureEnvironmentReport`): an allowlist-only collector writing
`start-failure-environment.json`. Values are recorded only for non-secret
update-mode keys (`CHUMMER_DESKTOP_UPDATE_ENABLED/_MODE/_AUTO_APPLY`);
machine-specific paths (`CHUMMER_CORE_CONTENT_ROOT`,
`CHUMMER_DESKTOP_UPDATE_MANIFEST`, `DOTNET_ROOT` - the SDK/Core overrides the
handoff names) record presence/absence only; every other key, credential names
included, contributes solely to `unlistedEnvironmentKeyCount`. Diagnostics
remain fail-soft (never mask the startup error).

### Indirect-path audit (complete)

- C# surface (`Chummer.Desktop.Installer`, `Chummer.Desktop.Runtime`,
  `Chummer.Avalonia`): no environment enumeration anywhere (only
  `ExpandEnvironmentVariables` path expansion).
- Other scripts: no `GetEnvironmentVariables` / `Get-ChildItem env:` usage.
- Receipt paths (`Write-Leg`/`Fail-Leg`): record only leg status and exception
  text (child stderr/stdout) - no environment material.

### Existing-artifact exposure disposition

Searched: repo tree, full git history (`git log --all -- '*start-failure-env*'`),
and local evidence roots (`artifacts/`, Friday task directories).
**No `start-failure-env.txt` exists anywhere** - the start leg never failed
natively, so the dump never executed. Disposition: zero artifacts, zero
exposure, **no credential rotation required**.

### Focused tests (all passing)

`tests/test_win004_start_failure_diag_privacy.py` (11 tests, pytest; dynamic
class runs the collector through a real PowerShell host, static class pins the
journey failure path):

1. Sentinel credential (`GH_TOKEN=<harmless value>`) supplied to the collector:
   its **value appears in no generated artifact** and its name appears in no
   report.
2. Safe update-mode values recorded; machine paths recorded as presence-only
   (values asserted absent from the report bytes).
3. Unlisted keys collapse to a count; absent allowlist keys render null/false.
4. Static pins: the journey script contains no
   `EnvironmentVariables.GetEnumerator` / `Get-ChildItem env:` /
   `GetEnvironmentVariables` primitive and no `start-failure-env.txt` path;
   it must call the collector; the collector has no flat name=value dump and
   no wildcard allowlist entries.

Wired into CI: `pull-request-ci.yml` split-preseal contract-test step runs the
suite (pwsh is preinstalled on ubuntu runners, so the dynamic class executes
there). Recipe allowlist extended by 2 paths
(`scripts/win004-start-failure-diagnostics.ps1`,
`tests/test_win004_start_failure_diag_privacy.py`) under this provenance block.
