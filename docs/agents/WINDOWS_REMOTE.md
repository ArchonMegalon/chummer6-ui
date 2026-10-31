# Windows with Friday Code

Clone `https://github.com/ArchonMegalon/chummer6-ui.git` on Windows. Start Friday
Code yourself with its existing provider setup. GitHub carries code and Teable
carries assignments; Windows does not need a connection to the Linux controller.
The helper checks setup and writes a prompt. It never starts a model process.

Install Git for Windows and Python 3.10+ with `git` and `python` (or `py`) on PATH.
Use Windows PowerShell 5.1 or PowerShell 7. From the directory where you keep code:

For an assignment that permits pushing a branch, install GitHub CLI and check
`gh auth status --hostname github.com`. If the intended account is not already
signed in, run `gh auth login --hostname github.com --git-protocol https --web`
and let the operator complete the browser sign-in. Then run:

```powershell
gh auth setup-git --hostname github.com
gh api user --jq .login
gh api repos/ArchonMegalon/chummer6-ui --jq .permissions.push
```

Confirm the intended account and push permission before publishing assigned work.
Use the OS credential store; do not copy the controller's GitHub credentials,
print tokens, or use `--insecure-storage`. Push your own assigned branch and open
a draft PR against the assigned integration branch. Protected merges and releases
remain controller-owned. See the [GitHub CLI login documentation](https://cli.github.com/manual/gh_auth_login).

Clone the handoff branch:

```powershell
git clone --branch feat/remote-windows-coordination-20261031 --single-branch https://github.com/ArchonMegalon/chummer6-ui.git
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
| `TEABLE_BASE_URL` | `https://app.teable.ai` |
| `TEABLE_BASE_ID` | `bseYIXFCaMHdzOben0f` |
| `TEABLE_API_KEY` | Token injected from an OS secret store into the process environment |
| `TEABLE_TASK_TABLE_ID` | Optional; otherwise discover the schema's task table by name |
| `TEABLE_EVENT_TABLE_ID` | Optional; otherwise discover the schema's event table by name |
| `CHUMMER_WORKER_ID` | `friday-windows` (controller default; override with your assigned ID) |

Keep the token in environment/OS secret storage only. Do not put it in commands,
PowerShell history, scripts, JSON, Git, logs or task reports. For example, when
PowerShell SecretManagement is already configured, inject the saved token with
`$env:TEABLE_API_KEY = Get-Secret -Name ChummerTeable -AsPlainText`; store it through
your existing secret-store UI. This onboarding does not install secret tooling.
Nonsecret binding values may be persistent user environment variables; restart
the terminal and Friday Code after changing those values.

```powershell
$env:TEABLE_BASE_URL = 'https://app.teable.ai'
$env:TEABLE_BASE_ID = 'bseYIXFCaMHdzOben0f'
$env:CHUMMER_WORKER_ID = 'friday-windows'
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

For an assigned build, install the matching Windows SDK yourself, then use the
portable setup with a private cache for that task. The download manifest accounts
for all 14 byte-pinned Chummer packages. Downloads require the explicit switch:

```powershell
$cache = Join-Path $env:LOCALAPPDATA 'Chummer\friday\tasks\windows-desktop-001'
.\scripts\start-windows-worker.ps1 -Mode setup -CacheRoot $cache -Download
if ($LASTEXITCODE -ne 0) { throw 'Package setup failed; inspect the readiness error.' }
.\scripts\start-windows-worker.ps1 -Mode check -CacheRoot $cache
.\scripts\build-windows-desktop.ps1 -CacheRoot $cache
if ($LASTEXITCODE -ne 0) { throw 'Windows restore/build failed; inspect the reported local log.' }
```

`setup` without `-Download` uses verified cached assets or `-InputDir` exact local
packages. It verifies existing feeds and creates a task-local NuGet configuration
with Chummer-only local source mapping and official nuget.org for third-party
packages. `-PackageFeed` selects a different flat feed; pass the same cache/feed
to setup, check, next and build. The default cache is per checkout under
`%LOCALAPPDATA%\Chummer\friday\workspaces\<checkout-hash>`, outside Git.
Use a fresh task cache after a source-policy change; preserve old data until any
active operation is reconciled. No global NuGet configuration or SDK is changed.

The build helper defaults to `Chummer.Avalonia`, `win-x64`, Debug. Only an explicit
controller assignment should use `-Project compatibility` for
`Chummer.Blazor.Desktop`. It checks Windows, exact SDK selection, verified feed
and local NuGet configuration before restoring/building one project at a time.
It uses a per-checkout Windows lock and needs no Bash, Linux paths, flock,
Headroom setup or Linux build capacity. Friday Code keeps its existing provider.
See [WINDOWS_PACKAGE_INTAKE.md](WINDOWS_PACKAGE_INTAKE.md) for commands, logs,
cache isolation, interruption recovery and offline limits.

`prerequisites_ready` / `build_ready` describe verified local inputs. They do not
prove restore, compilation, application startup or runtime data. `next` still
works when prerequisites are missing. Build reports record attempted/succeeded
restore and build stages separately; native runtime acceptance requires Friday
Code on Windows to launch, open/import, save, close and reopen a character.
Only work within controller-assigned paths and report actual Windows evidence.
Android overlays and other changes outside this GitHub clone are not supplied
by this onboarding.
