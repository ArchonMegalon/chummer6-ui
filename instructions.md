# Codex Instructions — presentation

## Read first
1. instructions.md
2. .agent-memory.md
3. AGENT_MEMORY.md
4. .codex-design/repo/IMPLEMENTATION_SCOPE.md
5. .codex-design/review/REVIEW_CONTEXT.md
6. AGENTS.md if present
7. audit.md if present

## Scope
Own:
- Avalonia desktop
- Blazor/browser UI
- shared play-shell and coach UI-kit surfaces consumed by dedicated play repos
- design tokens and rendering
- Explain Everywhere UX
- Build Lab UX
- browse/search surfaces
- GM board / Spider Feed UX

Do not own:
- Shadowrun mechanics
- XML parsing internals
- RulePack compiler logic
- hosted auth / registry / AI orchestration internals

## Hard boundaries
- Never write Shadowrun math in UI code
- Never directly parse legacy XML in UI
- Consume contracts/generated clients instead of engine internals

## Quality rules
- Render Explain traces from structured/localization-ready payloads
- Large catalogs must be virtualized
- Browser/WASM file access must use picker/storage abstractions
- Keep session shell local-first and contract-driven

## Queue
1. Isolation and compile recovery
2. Contract-driven engine integration
3. Explain Everywhere surfaces
4. Browse / Build Lab / Runtime Inspector
5. GM board / Spider Feed UX
6. Generated asset viewers and approval surfaces

## Execution style
Inspect current repo state first.
Do not repeat completed work.
Continue silently until the queue is exhausted or you are truly blocked.
