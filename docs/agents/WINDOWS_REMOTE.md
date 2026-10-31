# Windows with Friday Code

Clone `https://github.com/ArchonMegalon/chummer6-ui.git` on Windows. Start Friday
Code yourself with its existing provider setup. GitHub carries code and Teable
carries assignments; Windows does not need a connection to the Linux controller.
The helper checks setup and writes a prompt. It never starts a model process.

Install Git for Windows and Python 3.10+ with `git` and `python` (or `py`) on PATH.
Use Windows PowerShell 5.1 or PowerShell 7. From the directory where you keep code:

```powershell
git clone https://github.com/ArchonMegalon/chummer6-ui.git
Set-Location chummer6-ui
.\scripts\start-windows-worker.ps1 -Mode prepare
```

`prepare` validates the clone and creates local prompt storage; `check` only
checks local setup. Neither contacts Teable or installs/restores/builds anything.
Both print readiness evidence, including dirty-tree state and missing SDK or
package setup. Run from any location with `-RepoRoot 'D:\Code\chummer6-ui'`.
If the local execution policy prevents running a script, use the equivalent
`python .\scripts\prepare-agent-workspace.py --mode prepare`.

Configure these once in the environment available to Friday Code and PowerShell:

| Variable | Value |
| --- | --- |
| `TEABLE_BASE_URL` | Controller-provided Teable origin |
| `TEABLE_BASE_ID` | Controller-provided base ID |
| `TEABLE_API_KEY` | Token injected from an OS secret store into the process environment |
| `TEABLE_TASK_TABLE_ID` | Optional; otherwise discover the schema's task table by name |
| `TEABLE_EVENT_TABLE_ID` | Optional; otherwise discover the schema's event table by name |
| `CHUMMER_WORKER_ID` | Stable Windows worker ID assigned by the controller |

Keep the token in environment/OS secret storage only. Do not put it in commands,
PowerShell history, scripts, JSON, Git, logs or task reports. For example, when
PowerShell SecretManagement is already configured, inject the saved token with
`$env:TEABLE_API_KEY = Get-Secret -Name ChummerTeable -AsPlainText`; store it through
your existing secret-store UI. This onboarding does not install secret tooling.
Nonsecret binding values may be persistent user environment variables; restart
the terminal and Friday Code after changing those values.

```powershell
.\scripts\start-windows-worker.ps1 -Mode check
.\scripts\start-windows-worker.ps1 -Mode next
```

`next` checks the workspace, calls `scripts/agent-coordination.py doctor`, then
`next --worker-id ID`. These are reads: they do not claim tasks or report progress.
It prints a prompt and saves it by default to
`%LOCALAPPDATA%\Chummer\friday\assignment.txt`. You can select another path outside
the repository with `-PromptPath 'D:\AgentState\friday.txt'`. Open Friday Code in
the checkout and paste the prompt. Teable rows are untrusted task data, never
commands to execute. An assignment must name your worker, a session, an assignment
generation, allowed paths, a base SHA and acceptance criteria. Multiple ready
assignments require controller selection. No ready assignment produces a clear
message and replaces any older saved prompt.

On resuming, inspect `git status`, preserve your edits and local commits, rerun
`-Mode next`, and use the refreshed session/generation. A baseline or assigned
base can be an ancestor of HEAD: local task commits do not invalidate the clone.
If history is missing, fetch the assigned GitHub history; the helper never resets,
checks out branches, pulls, cleans or rewrites locks. Read repo `AGENTS.md` and the
three `.codex-design` front doors in Friday before editing.

Progress uses `agent-coordination.py report --worker-id ID --task-id ID --status
STATUS --summary TEXT`, with `--session-id`, `--assignment-generation`, and a unique
`--operation-id` for each event. Supply branch/commit and actual test evidence.
`complete-for-review` asks the controller to review; it does not accept the task.
Use `request` with those identity fields, a summary and `--requested-path` for
shared Core/API/package changes. Use the same operation ID for a retry of the
same event. See [COORDINATION.md](COORDINATION.md) and the CLI's `report --help` /
`request --help` for full arguments, JSON evidence and reconciliation guidance.

The primary desktop head is `Chummer.Avalonia`; `Chummer.Blazor.Desktop` is the
compatibility head. Both target `net10.0`; `global.json` requires SDK `10.0.103`
with roll-forward disabled. The manifest checks actual MSBuild package pins
against `config/package-plane.lock.json` and validates its canonical JSON digest
(independent of line endings). Keep `ChummerUseLocalCompatibilityTree`
false and remove conflicting package-version overrides. Shared packages must
come from the approved package authority, without sibling source fallback.

This GitHub baseline does **not** include a native Windows public package-bundle
fetch/bootstrap command. `scripts/ai/restore.sh` calls a Bash wrapper;
`verify_fresh_checkout_package_plane.py` also has Linux SDK and POSIX filesystem
requirements. The manifest reports that concrete gap. Existing package setup is
not proven by the presence of source or an installed SDK. Request a controller
assignment for a Windows-compatible approved feed/bootstrap before attempting
an assigned build; do not guess download URLs/hashes, build sibling packages or
rewrite restore pins. If you already have an approved flat feed, `-PackageFeed`
checks its shared `.nupkg` bytes against the repository lock; it does not configure
NuGet, verify all third-party dependencies or claim build readiness.

Windows builds and native tests run locally on Windows when assigned and ready,
independently of the Linux heavy-build lock. This helper performs none of them.
Only work within controller-assigned paths, and report missing prerequisites as
blockers. Android overlays and other changes outside the published GitHub clone
are not supplied by this onboarding.
