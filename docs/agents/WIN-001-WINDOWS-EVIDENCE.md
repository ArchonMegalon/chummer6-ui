# WIN-001 Windows evidence — 2026-10-31

## Assignment and scope

- Worker: `friday-windows`; session: `friday-windows-session-01`; generation: `1`.
- Branch: `windows/WIN-001-build-save-reopen`.
- Checkout tested: `b5add16a609ac92375a70472ea14c3e85448f56c`, a descendant of the assigned baseline.
- Host: Microsoft Windows 11 Pro, version `10.0.26200`, 64-bit.
- No Core/API/package pins, shared runtime, or presenter sources changed. No sibling-source fallback enabled.

## Executed setup and build

1. Loaded the operator's Windows DPAPI-protected Teable credential into a child process. Trimming the encrypted file's trailing newline was required before `ConvertTo-SecureString`. No credential values are included here.
2. Executed `scripts/start-windows-worker.ps1 -Mode next`; verified the live assignment, ownership, session, generation, allowed paths and acceptance.
3. Executed `scripts/start-windows-worker.ps1 -Mode setup -CacheRoot <external-cache> -Download`. All 14 locked Chummer packages verified successfully.
4. Installed SDK `10.0.103` under the external task cache. The Windows x64 archive was verified against the SHA-512 published in Microsoft's .NET 10 release metadata before extraction. Repository `global.json` was not changed.
5. Added that SDK to the child process's `DOTNET_ROOT` and `PATH`, then executed `scripts/start-windows-worker.ps1 -Mode build -CacheRoot <external-cache>`.

Result: primary `Chummer.Avalonia/Chummer.Avalonia.csproj`, RID `win-x64`, restore and build both exited `0`. This also passed after the diagnostic changes in this branch. The build reported zero errors. The compatibility desktop head was not built.

Artifact: `Chummer.Avalonia/bin/Debug/net10.0/win-x64/Chummer.Avalonia.exe`.

External cache: `%LOCALAPPDATA%\Chummer\friday\tasks\WIN-001`.
Build evidence: `restore.log`, `build.log` and the build wrapper's JSON output. No binaries, downloaded packages or SDK files are committed.

## Executed native launch and journey

Launched the actual Windows executable with `--mouse-first-user-journey` and these existing runner inputs pointed into the external cache:

- `CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT`
- `CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET`
- `CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR`

The native app opened its New Runner dialog. The journey entered fields and attempted Create. This is the existing runner's synthetic Avalonia routed-pointer automation, not a claim of manual OS mouse interaction.

The final diagnostic run recorded:

```text
click dialog action create_character; delivered=False; enabled=True
click dialog action create_character; delivered=True; enabled=True
click dialog action create_character; delivered=True; enabled=True
failure dialog: dialog.new_character; presenter error: Authoritative character creation is unavailable (creation-bootstrap-source-context-unavailable).
```

The runner then timed out waiting for `complete_new_character_workflow`. Read-only inspection shows the current presenter uses bootstrap activation and explicitly rejects the legacy continuation, so that expectation also needs owner review once source context is available. This branch does not bypass bootstrap authority or revive that legacy route.

Final evidence directory: `<external-cache>\native-final-diagnostic\`, containing `receipt.json`, `failure.json`, `stdout.log`, `stderr.log` and screenshots including `journey-failure.png`. Earlier runs are retained in sibling diagnostic directories.

**Not verified:** successful character creation/import, save, process-close/reopen persistence, installer behavior, or complete native acceptance. The failed journey process exited; this is not a successful save/reopen result. No user character data was deleted or overwritten by cleanup.

## Scoped diagnostic improvement

`Chummer.Avalonia/DesktopMouseFirstJourneyRunner.cs` now records whether dialog button clicks were actually delivered and whether the button was enabled. On journey failure it records the presenter error and active dialog ID, and attempts a screenshot through the existing screenshot helper. A screenshot failure does not replace the original journey error. The diagnostic run above exercised these paths on Windows.

## Executed regression checks

- `python -m unittest discover -s tests -p test_prepare_agent_workspace.py -v`: **25 passed**.
- `python -m unittest discover -s tests -p test_windows_package_feed.py -v`: **18 methods run; six failing subcases** in `test_unsafe_members_are_rejected_including_unselected_and_nested_package_members`. The failing cases are backslash paths, reserved `NUL.txt`, trailing-dot/space names, case-colliding names, symlink entries and parent/file collisions in nested packages. Neither that test nor the intake implementation was changed in this branch; these failures are reported, not suppressed.
- `git diff --check`: passed (Git emitted only the checkout's LF-to-CRLF advisory).

## Controller dependency

Teable request operation `win001-request-source-context-20261031-01` was recorded successfully. It asks the controller for approved authoritative source context/import input or owner-routed shared bootstrap/journey repair. Shared paths named in that request are not authorization to edit them. WIN-001's full save/reopen acceptance remains blocked pending that response.
